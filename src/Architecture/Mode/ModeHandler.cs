using WearWare.Architecture.Playlist;
using WearWare.Architecture.QuickMedia;
using WearWare.Architecture.Physics;

namespace WearWare.Architecture.Mode
{
    public sealed class ModeHandler : IHostedService
    {
        private readonly SemaphoreSlim _transition;
        private readonly ModeManager _modeManager;
        private readonly PlaylistHandler _playlistHandler;
        private readonly QuickMediaHandler _quickMediaHandler;
        private readonly PhysicsHandler _physicsHandler;
        private CancellationTokenSource? _physicsCancellation;
        private Task? _physicsTask;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<ModeHandler> _logger;
        private readonly string _logTag = "[MODEHANDLER]";
        private volatile bool _stopping;
        private volatile bool _started;

        public event Action? StateChanged;
        public ModeState State => _modeManager.State;

        /// <summary>
        /// Connects the mode coordinator to the playback handlers and their control requests.
        /// </summary>
        public ModeHandler(
            ModeManager modeManager,
            PlaylistHandler playlistHandler,
            QuickMediaHandler quickMediaHandler,
            PhysicsHandler physicsHandler,
            IHostApplicationLifetime lifetime,
            ILogger<ModeHandler> logger)
        {
            _modeManager = modeManager;
            _playlistHandler = playlistHandler;
            _quickMediaHandler = quickMediaHandler;
            _physicsHandler = physicsHandler;
            _transition = quickMediaHandler.TransitionGate;
            _lifetime = lifetime;
            _logger = logger;
            _modeManager.StateChanged += OnModeManagerStateChanged;
            _quickMediaHandler.ConfigureModeControl(RequestControlAsync, ReleaseControlAsync, () => _started && !_stopping);
        }

        /// <summary>
        /// Forwards mode state changes to subscribers.
        /// </summary>
        private void OnModeManagerStateChanged()
        {
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Starts playback for the persisted mode, restoring the interrupted mode if QM cannot resume.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{tag} starting.", _logTag);

            _lifetime.ApplicationStopping.Register(() =>
            {
                _stopping = true;
            });

            await _transition.WaitAsync(cancellationToken);
            try
            {
                if (State.Mode == DisplayMode.QuickMedia)
                {
                    if (!_quickMediaHandler.Resume())
                        await RestoreInterruptedModeAsync();
                }
                else if (State.Mode == DisplayMode.Physics)
                {
                    _quickMediaHandler.Deactivate();
                    _playlistHandler.Stop();
                    _ = RestorePhysicsAsync(_lifetime.ApplicationStopping);
                }
                else
                {
                    _quickMediaHandler.Deactivate();
                    _playlistHandler.Start();
                }

                _started = true;
            }
            finally
            {
                _transition.Release();
            }
        }

        /// <summary>
        /// Stops playback during application shutdown without clearing the saved QM item.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{tag} stopping.", _logTag);
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

        /// <summary>
        /// Validates and applies a requested display mode.
        /// </summary>
        public Task SetModeAsync(DisplayMode mode)
        {
            if (mode != DisplayMode.Playlist && mode != DisplayMode.QuickMedia && mode != DisplayMode.Physics)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
            return ChangeModeAsync(mode);
        }

        /// <summary>
        /// Grants QM control after stopping Playlist and recording the interrupted mode.
        /// The requesting handler holds the transition gate until it has applied the grant.
        /// </summary>
        private async Task<bool> RequestControlAsync(DisplayMode requestedMode)
        {
            if (requestedMode != DisplayMode.QuickMedia)
                throw new ArgumentOutOfRangeException(nameof(requestedMode));

            if (_stopping || !_started)
                return false;
            if (State.Mode == requestedMode)
                return true;

            var previousMode = State.Mode;
            var playlistWasRunning = false;
            try
            {
                playlistWasRunning = _playlistHandler.IsRunning();
                _playlistHandler.Stop();
                if (previousMode == DisplayMode.Physics)
                    await StopPhysicsAsync();
                if (await _modeManager.SetModeWithInterrupt(requestedMode))
                    return true;

                _logger.LogError("{tag} Could not grant control to {mode}.", _logTag, requestedMode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Failed to grant control to {mode}.", _logTag, requestedMode);
            }

            if (State.Mode == requestedMode && !_stopping)
                await RestoreInterruptedModeAsync();
            else if (!_stopping)
                await ResumeModeAsync(previousMode, playlistWasRunning);
            return false;
        }

        /// <summary>
        /// Restores the interrupted mode when the current mode yields control.
        /// </summary>
        private async Task ReleaseControlAsync(DisplayMode releasingMode)
        {
            if (!_stopping && State.Mode == releasingMode)
                await RestoreInterruptedModeAsync();
        }

        /// <summary>
        /// Restores the saved interrupted mode, falling back to Playlist if restoration fails.
        /// </summary>
        private async Task RestoreInterruptedModeAsync()
        {
            if (!await _modeManager.RestoreInterruptedMode())
            {
                _logger.LogError("{tag} Could not restore the interrupted mode configuration; returning to Playlist.", _logTag);
                _modeManager.UpdateState(State with
                {
                    Mode = DisplayMode.Playlist,
                    Message = "Could not restore the interrupted mode configuration. See the log for details."
                });
            }

            if (_stopping)
                return;

            if (State.Mode == DisplayMode.Playlist)
                _playlistHandler.Start();
            else if (State.Mode == DisplayMode.Physics &&
                !await TryStartPhysicsAsync(_lifetime.ApplicationStopping))
                StartPlaylistAfterPhysicsFailure("Physics is unavailable. Returned to Playlist.");
        }

        /// <summary>
        /// Persists a mode selection and coordinates the corresponding playback transition.
        /// </summary>
        private async Task ChangeModeAsync(DisplayMode requestedMode)
        {
            await _transition.WaitAsync();
            try
            {
                var previous = State;
                if (_stopping)
                {
                    return;
                }

                var mode = requestedMode;
                var preferredMode = previous.PreferredMode;
                string? saveError = null;

                if (_modeManager.SaveMode(requestedMode))
                {
                    preferredMode = requestedMode;
                }
                else
                {
                    saveError = "Could not save the selected mode. See the log for details.";
                }

                if (mode == previous.Mode)
                {
                    if (preferredMode != previous.PreferredMode || saveError != null)
                    {
                        _modeManager.UpdateState(previous with { Message = saveError, PreferredMode = preferredMode });
                    }
                    return;
                }

                if (mode == DisplayMode.Playlist)
                {
                    string? physicsError = null;
                    if (previous.Mode == DisplayMode.Physics)
                        physicsError = await StopPhysicsAsync();

                    _quickMediaHandler.Deactivate();
                    _playlistHandler.Start();
                    _modeManager.UpdateState(new ModeState(DisplayMode.Playlist, CombineMessages(physicsError, saveError), preferredMode));
                    _logger.LogInformation("{tag} Switched to Playlist mode.", _logTag);
                    return;
                }

                if (mode == DisplayMode.QuickMedia)
                {
                    if (previous.Mode == DisplayMode.Physics)
                        await StopPhysicsAsync();
                    _playlistHandler.Stop();
                    _modeManager.UpdateState(new ModeState(DisplayMode.QuickMedia, saveError, preferredMode));
                    if (!_quickMediaHandler.Resume())
                    {
                        await RestoreInterruptedModeAsync();
                        return;
                    }
                    _logger.LogInformation("{tag} Switched to QuickMedia mode.", _logTag);
                    return;
                }

                if (mode == DisplayMode.Physics)
                {
                    _quickMediaHandler.Deactivate();
                    _playlistHandler.Stop();
                    if (!await TryStartPhysicsAsync(_lifetime.ApplicationStopping))
                    {
                        var message = CombineMessages("Physics is unavailable. Returned to Playlist.", saveError)
                            ?? "Physics is unavailable. Returned to Playlist.";
                        StartPlaylistAfterPhysicsFailure(
                            message, preferredMode);
                        return;
                    }

                    _modeManager.UpdateState(new ModeState(DisplayMode.Physics, saveError, preferredMode));
                    _logger.LogInformation("{tag} Switched to Physics mode (Fluid).", _logTag);
                    return;
                }
            }
            finally
            {
                _transition.Release();
            }
        }

        private async Task<string?> StopPhysicsAsync()
        {
            var cancellation = _physicsCancellation;
            var task = _physicsTask;
            if (cancellation == null || task == null)
                return null;

            cancellation.Cancel();
            try
            {
                await task;
                return null;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Physics stopped unexpectedly.", _logTag);
                return "Physics failed. Returned to Playlist.";
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
                _logger.LogError(ex, "{tag} Could not start Physics.", _logTag);
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
                    if (!_stopping && State.Mode == DisplayMode.Physics &&
                        !await TryStartPhysicsAsync(cancellationToken))
                        StartPlaylistAfterPhysicsFailure("Physics is unavailable. Returned to Playlist.");
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
                _logger.LogError(ex, "{tag} Could not restore Physics mode.", _logTag);
                await _transition.WaitAsync();
                try
                {
                    if (!_stopping && State.Mode == DisplayMode.Physics)
                        StartPlaylistAfterPhysicsFailure("Physics is unavailable. Returned to Playlist.");
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

            _logger.LogError(failure, "{tag} Physics failed; returning to Playlist.", _logTag);
            await _transition.WaitAsync();
            try
            {
                if (!ReferenceEquals(_physicsTask, task) || _stopping)
                    return;

                _physicsTask = null;
                _physicsCancellation?.Dispose();
                _physicsCancellation = null;
                _playlistHandler.Start();
                _modeManager.UpdateState(State with { Mode = DisplayMode.Playlist, Message = "Physics failed. Returned to Playlist." });
            }
            finally
            {
                _transition.Release();
            }
        }

        private async Task ResumeModeAsync(DisplayMode mode, bool playlistWasRunning)
        {
            if (mode == DisplayMode.Physics)
            {
                if (!await TryStartPhysicsAsync(_lifetime.ApplicationStopping))
                    StartPlaylistAfterPhysicsFailure("Physics is unavailable. Returned to Playlist.");
            }
            else if (playlistWasRunning || mode == DisplayMode.Playlist)
            {
                _playlistHandler.Start();
            }
        }

        private void StartPlaylistAfterPhysicsFailure(string message, DisplayMode? preferredMode = null)
        {
            if (_stopping)
                return;

            _playlistHandler.Start();
            _modeManager.UpdateState(State with
            {
                Mode = DisplayMode.Playlist,
                Message = message,
                PreferredMode = preferredMode ?? State.PreferredMode
            });
        }

        private static string? CombineMessages(string? first, string? second)
        {
            if (first == null)
                return second;
            return second == null ? first : $"{first} {second}";
        }
    }
}
