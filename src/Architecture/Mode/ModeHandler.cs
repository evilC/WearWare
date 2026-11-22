using WearWare.Architecture.Playlist;
using WearWare.Architecture.QuickMedia;
using WearWare.Services.Physics;

namespace WearWare.Architecture.Mode
{
    public sealed class ModeHandler : IHostedService
    {
        private readonly SemaphoreSlim _transition;
        private readonly ModeManager _modeManager;
        private readonly PlaylistManager _playlistManager;
        private readonly PlaylistHandler _playlistHandler;
        private readonly QuickMediaHandler _quickMediaHandler;
        private readonly PhysicsHandler _physicsHandler;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<ModeHandler> _logger;
        private CancellationTokenSource? _physicsCancellation;
        private Task? _physicsTask;
        private volatile bool _stopping;
        private volatile bool _started;

        public event Action? StateChanged;
        public ModeState State => _modeManager.State;

        public ModeHandler(
            ModeManager modeManager,
            PlaylistManager playlistManager,
            PlaylistHandler playlistHandler,
            QuickMediaHandler quickMediaHandler,
            PhysicsHandler physicsHandler,
            IHostApplicationLifetime lifetime,
            ILogger<ModeHandler> logger)
        {
            _modeManager = modeManager;
            _playlistManager = playlistManager;
            _playlistHandler = playlistHandler;
            _quickMediaHandler = quickMediaHandler;
            _physicsHandler = physicsHandler;
            _transition = quickMediaHandler.TransitionGate;
            _lifetime = lifetime;
            _logger = logger;
            _modeManager.StateChanged += () => StateChanged?.Invoke();
            _quickMediaHandler.ConfigureModeControl(RequestControlAsync, ReleaseControlAsync, () => _started && !_stopping);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _lifetime.ApplicationStopping.Register(() => _stopping = true);
            await _transition.WaitAsync(cancellationToken);
            try
            {
                if (_playlistManager.StateLoadError is { } playlistError)
                    _modeManager.UpdateState(State with { Message = CombineMessages(State.Message, playlistError) });
                if (State.InterruptingMode == DisplayMode.QuickMedia)
                {
                    if (!_quickMediaHandler.Resume())
                    {
                        ClearInterruption();
                        StartNormalModeOnStartup();
                    }
                }
                else
                {
                    _quickMediaHandler.Deactivate();
                    StartNormalModeOnStartup();
                }
                _started = true;
            }
            finally
            {
                _transition.Release();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _stopping = true;
            await _transition.WaitAsync(cancellationToken);
            try
            {
                _playlistHandler.Stop();
                _quickMediaHandler.Suspend();
                await StopPhysicsAsync();
            }
            finally
            {
                _transition.Release();
            }
        }

        public async Task SetModeAsync(DisplayMode mode)
        {
            if (mode != DisplayMode.Playlist && mode != DisplayMode.Physics)
                throw new ArgumentOutOfRangeException(nameof(mode));

            await _transition.WaitAsync();
            try
            {
                if (_stopping || !_started)
                    return;

                var previous = State;
                var next = previous with { CurrentMode = mode, Message = null };
                if (!_modeManager.SaveState(next))
                    next = next with { Message = "Could not save mode state. See the log for details." };

                if (previous.InterruptingMode != null)
                {
                    _modeManager.UpdateState(next);
                    return;
                }

                try
                {
                    if (previous.ActiveMode == DisplayMode.Physics)
                        await StopPhysicsAsync();
                    _playlistHandler.Stop();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MODEHANDLER] Could not stop the active mode.");
                    _modeManager.UpdateState(next with
                    {
                        Message = CombineMessages(next.Message, "Could not switch modes. See the log for details.")
                    });
                    await ResumeActualModeAsync(previous.ActiveMode);
                    return;
                }
                _modeManager.UpdateState(next);
                await StartNormalModeAsync();
            }
            finally
            {
                _transition.Release();
            }
        }

        public async Task SetPlaylistRunningAsync(bool running)
        {
            await _transition.WaitAsync();
            try
            {
                if (_stopping || !_started)
                    return;

                var saved = _playlistManager.SetPlaylistRunning(running);
                var next = State with
                {
                    Message = saved ? null : "Could not save Playlist state. See the log for details."
                };
                _modeManager.UpdateState(next);
                if (next.InterruptingMode == null && next.ActiveMode == DisplayMode.Playlist)
                {
                    if (running && !_playlistHandler.IsRunning())
                        _playlistHandler.Start();
                    else if (!running)
                    {
                        try
                        {
                            _playlistHandler.Stop();
                            ClearPlaylistDisplay();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[MODEHANDLER] Could not stop Playlist.");
                            _modeManager.UpdateState(State with
                            {
                                Message = CombineMessages(State.Message, "Could not stop Playlist cleanly. See the log for details.")
                            });
                        }
                    }
                }
            }
            finally
            {
                _transition.Release();
            }
        }

        public async Task SelectPlaylistAsync(string playlistName)
        {
            await _transition.WaitAsync();
            try
            {
                if (_stopping || !_started || _playlistManager.CurrentPlaylistName == playlistName)
                    return;
                var wasRunning = _playlistHandler.IsRunning();
                if (wasRunning)
                    _playlistHandler.Stop();
                _playlistManager.SetActivePlaylist(playlistName);
                if (wasRunning)
                    _playlistHandler.Start();
            }
            finally
            {
                _transition.Release();
            }
        }

        public async Task JumpToPlaylistItemAsync(int index)
        {
            await _transition.WaitAsync();
            try
            {
                if (_stopping || !_started)
                    return;
                if (_playlistManager.JumpToItem(index) && _playlistHandler.IsRunning())
                {
                    _playlistHandler.Stop();
                    _playlistHandler.Start();
                }
            }
            finally
            {
                _transition.Release();
            }
        }

        public async Task<bool> DeletePlaylistAsync(string playlistName)
        {
            await _transition.WaitAsync();
            try
            {
                if (_stopping || !_started)
                    return false;
                var wasRunning = _playlistHandler.IsRunning() && _playlistManager.CurrentPlaylistName == playlistName;
                if (wasRunning)
                    _playlistHandler.Stop();
                var deleted = _playlistManager.DeletePlaylist(playlistName);
                if (wasRunning && !deleted)
                    _playlistHandler.Start();
                return deleted;
            }
            finally
            {
                _transition.Release();
            }
        }

        // QuickMedia holds the transition gate while invoking these callbacks.
        private async Task<bool> RequestControlAsync(DisplayMode requestedMode)
        {
            if (requestedMode != DisplayMode.QuickMedia)
                throw new ArgumentOutOfRangeException(nameof(requestedMode));
            if (_stopping || !_started)
                return false;
            if (State.InterruptingMode == DisplayMode.QuickMedia)
                return true;

            var previous = State;
            try
            {
                _playlistHandler.Stop();
                if (previous.ActiveMode == DisplayMode.Physics)
                    await StopPhysicsAsync();

                var next = previous with
                {
                    InterruptingMode = DisplayMode.QuickMedia,
                    ActiveMode = DisplayMode.QuickMedia,
                    Message = null
                };
                if (_modeManager.SaveState(next))
                {
                    _modeManager.UpdateState(next);
                    return true;
                }
                ReportSaveFailure();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MODEHANDLER] Failed to grant control to QuickMedia.");
                if (State.InterruptingMode == DisplayMode.QuickMedia)
                    ClearInterruption();
            }
            await ResumeActualModeAsync(previous.ActiveMode);
            return false;
        }

        private async Task ReleaseControlAsync(DisplayMode releasingMode)
        {
            if (_stopping || State.InterruptingMode != releasingMode)
                return;
            ClearInterruption();
            await StartNormalModeAsync();
        }

        private void ClearInterruption()
        {
            var next = State with { InterruptingMode = null, ActiveMode = State.CurrentMode };
            if (!_modeManager.SaveState(next))
                next = next with { Message = "Could not save mode state. See the log for details." };
            _modeManager.UpdateState(next);
        }

        private void StartNormalModeOnStartup()
        {
            if (State.CurrentMode == DisplayMode.Physics)
                _ = RestorePhysicsAsync(_lifetime.ApplicationStopping);
            else
                StartPlaylist();
        }

        private async Task StartNormalModeAsync()
        {
            if (_stopping)
                return;
            if (State.CurrentMode == DisplayMode.Physics)
            {
                if (!await TryStartPhysicsAsync(_lifetime.ApplicationStopping))
                    StartPlaylistAfterPhysicsFailure();
                else
                    _modeManager.UpdateState(State with { ActiveMode = DisplayMode.Physics });
            }
            else
            {
                StartPlaylist();
            }
        }

        private async Task ResumeActualModeAsync(DisplayMode mode)
        {
            if (_stopping)
                return;
            if (mode == DisplayMode.Physics)
            {
                if (!await TryStartPhysicsAsync(_lifetime.ApplicationStopping))
                    StartPlaylistAfterPhysicsFailure();
            }
            else
            {
                StartPlaylist();
            }
        }

        private void StartPlaylist()
        {
            _modeManager.UpdateState(State with { ActiveMode = DisplayMode.Playlist });
            if (_playlistManager.PlaylistRunning)
                _playlistHandler.Start();
            else
                ClearPlaylistDisplay();
        }

        private void StartPlaylistAfterPhysicsFailure()
        {
            if (_stopping)
                return;
            const string physicsMessage = "Physics is unavailable. Returned to Playlist.";
            var message = State.Message == physicsMessage ? physicsMessage : CombineMessages(State.Message, physicsMessage);
            _modeManager.UpdateState(State with
            {
                ActiveMode = DisplayMode.Playlist,
                Message = message
            });
            if (_playlistManager.PlaylistRunning)
                _playlistHandler.Start();
            else
                ClearPlaylistDisplay();
        }

        private void ClearPlaylistDisplay()
        {
            try
            {
                _playlistHandler.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MODEHANDLER] Could not clear the display.");
                _modeManager.UpdateState(State with
                {
                    Message = CombineMessages(State.Message, "Could not clear the display. See the log for details.")
                });
            }
        }

        private static string CombineMessages(string? first, string second) =>
            first == null ? second : $"{first} {second}";

        private void ReportSaveFailure()
        {
            _modeManager.UpdateState(State with { Message = "Could not save mode state. See the log for details." });
        }

        private async Task StopPhysicsAsync()
        {
            var cancellation = _physicsCancellation;
            var task = _physicsTask;
            if (cancellation == null || task == null)
                return;

            cancellation.Cancel();
            try
            {
                await task;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MODEHANDLER] Physics stopped unexpectedly.");
            }
            finally
            {
                _physicsTask = null;
                _physicsCancellation = null;
                cancellation.Dispose();
            }
        }

        private async Task<bool> TryStartPhysicsAsync(CancellationToken cancellationToken)
        {
            if (_physicsTask != null)
                return true;

            CancellationTokenSource? cancellation = null;
            try
            {
                await _physicsHandler.WaitForImuAsync(cancellationToken);
                cancellation = new CancellationTokenSource();
                var token = cancellation.Token;
                var task = Task.Run(() => _physicsHandler.RunAsync(token));
                _physicsCancellation = cancellation;
                _physicsTask = task;
                _ = ObservePhysicsAsync(task, token);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancellation?.Dispose();
                return false;
            }
            catch (Exception ex)
            {
                cancellation?.Dispose();
                _logger.LogError(ex, "[MODEHANDLER] Could not start Physics.");
                return false;
            }
        }

        private async Task RestorePhysicsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _physicsHandler.WaitForImuAsync(cancellationToken);
                await _transition.WaitAsync(cancellationToken);
                try
                {
                    if (!_stopping && _physicsTask == null && State.InterruptingMode == null &&
                        State.CurrentMode == DisplayMode.Physics)
                        await StartNormalModeAsync();
                }
                finally
                {
                    _transition.Release();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MODEHANDLER] Could not restore Physics mode.");
                await _transition.WaitAsync();
                try
                {
                    if (!_stopping && _physicsTask == null && State.InterruptingMode == null &&
                        State.CurrentMode == DisplayMode.Physics)
                        StartPlaylistAfterPhysicsFailure();
                }
                finally
                {
                    _transition.Release();
                }
            }
        }

        private async Task ObservePhysicsAsync(Task task, CancellationToken cancellationToken)
        {
            Exception failure;
            try
            {
                await task;
                if (cancellationToken.IsCancellationRequested)
                    return;
                failure = new InvalidOperationException("Physics renderer stopped unexpectedly.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            _logger.LogError(failure, "[MODEHANDLER] Physics failed; returning to Playlist.");
            await _transition.WaitAsync();
            try
            {
                if (!ReferenceEquals(_physicsTask, task) || _stopping)
                    return;
                _physicsTask = null;
                _physicsCancellation?.Dispose();
                _physicsCancellation = null;
                StartPlaylistAfterPhysicsFailure();
            }
            finally
            {
                _transition.Release();
            }
        }
    }
}
