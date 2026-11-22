using WearWare.Architecture.Playlist;
using WearWare.Architecture.QuickMedia;

namespace WearWare.Architecture.Mode
{
    public sealed class ModeHandler : IHostedService
    {
        private readonly SemaphoreSlim _transition;
        private readonly ModeManager _modeManager;
        private readonly PlaylistHandler _playlistHandler;
        private readonly QuickMediaHandler _quickMediaHandler;
        private readonly QuickMediaManager _quickMediaManager;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<ModeHandler> _logger;
        private readonly string _logTag = "[MODEHANDLER]";
        private volatile bool _stopping;
        private volatile bool _started;

        public event Action? StateChanged;
        public ModeState State => _modeManager.State;

        public ModeHandler(
            ModeManager modeManager,
            PlaylistHandler playlistHandler,
            QuickMediaHandler quickMediaHandler,
            QuickMediaManager quickMediaManager,
            IHostApplicationLifetime lifetime,
            ILogger<ModeHandler> logger)
        {
            _modeManager = modeManager;
            _playlistHandler = playlistHandler;
            _quickMediaHandler = quickMediaHandler;
            _quickMediaManager = quickMediaManager;
            _transition = quickMediaManager.TransitionGate;
            _lifetime = lifetime;
            _logger = logger;
            _modeManager.StateChanged += OnModeManagerStateChanged;
            _quickMediaHandler.ButtonPressed += OnQuickMediaButtonPressed;
            _quickMediaHandler.PlaybackCompleted += OnQuickMediaPlaybackCompleted;
        }

        private void OnModeManagerStateChanged()
        {
            StateChanged?.Invoke();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{tag} starting.", _logTag);

            _lifetime.ApplicationStopping.Register(() =>
            {
                _stopping = true;
            });

            if (State.Mode == DisplayMode.QuickMedia)
            {
                if (!_quickMediaHandler.ResumeForever())
                    await RestoreAfterQuickMediaAsync();
            }
            else
            {
                _quickMediaManager.ClearActiveButton();
                _quickMediaManager.ClearForeverButton();
                _playlistHandler.Start();
            }

            _started = true;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{tag} stopping.", _logTag);
            _stopping = true;
            await _transition.WaitAsync(cancellationToken);
            try
            {
                _playlistHandler.Stop();
                _quickMediaHandler.Stop();
            }
            finally
            {
                _transition.Release();
            }
        }

        public Task SetModeAsync(DisplayMode mode)
        {
            if (mode != DisplayMode.Playlist && mode != DisplayMode.QuickMedia)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
            return ChangeModeAsync(mode);
        }

        private void OnQuickMediaButtonPressed(int buttonNumber)
        {
            _ = ActivateQuickMediaAsync(buttonNumber);
        }

        private async Task ActivateQuickMediaAsync(int buttonNumber)
        {
            await _transition.WaitAsync();
            var playlistWasRunning = false;
            try
            {
                if (_stopping || !_started)
                    return;

                if (!_quickMediaManager.GetQuickMediaButtonNumbers().Contains(buttonNumber) ||
                    _quickMediaManager.GetButtonItem(buttonNumber) == null)
                {
                    _logger.LogWarning("{tag} QuickMedia button {button} is unassigned or has no item.", _logTag, buttonNumber);
                    return;
                }

                if (State.Mode != DisplayMode.QuickMedia)
                {
                    playlistWasRunning = _playlistHandler.IsRunning();
                    _playlistHandler.Stop();

                    if (!await _modeManager.SetModeWithInterrupt(DisplayMode.QuickMedia))
                    {
                        _logger.LogError("{tag} Could not switch to QuickMedia.", _logTag);
                        await RecoverFromFailedActivationAsync(playlistWasRunning);
                        return;
                    }
                }

                if (!_quickMediaHandler.ActivateButton(buttonNumber))
                    await RestoreAfterQuickMediaAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Failed to activate QuickMedia button {button}.", _logTag, buttonNumber);
                if (!_stopping)
                {
                    try
                    {
                        await RecoverFromFailedActivationAsync(playlistWasRunning);
                    }
                    catch (Exception restoreException)
                    {
                        _logger.LogError(restoreException, "{tag} Failed to recover Playlist playback.", _logTag);
                    }
                }
            }
            finally
            {
                _transition.Release();
            }
        }

        private async Task RecoverFromFailedActivationAsync(bool playlistWasRunning)
        {
            _quickMediaHandler.Stop();
            if (State.Mode == DisplayMode.QuickMedia)
                await RestoreAfterQuickMediaAsync();
            else
            {
                _quickMediaManager.ClearActiveButton();
                _quickMediaManager.ClearForeverButton();
                if (playlistWasRunning && !_stopping)
                    _playlistHandler.Start();
            }
        }

        private void OnQuickMediaPlaybackCompleted(Task<bool> task, bool success)
        {
            _ = CompleteQuickMediaAsync(task, success);
        }

        private async Task CompleteQuickMediaAsync(Task<bool> task, bool success)
        {
            await _transition.WaitAsync();
            try
            {
                if (_stopping || State.Mode != DisplayMode.QuickMedia || !_quickMediaHandler.IsCurrentPlayback(task))
                    return;

                if (!_quickMediaHandler.CompletePlayback(success))
                    await RestoreAfterQuickMediaAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Failed to restore mode after QuickMedia.", _logTag);
            }
            finally
            {
                _transition.Release();
            }
        }

        private async Task RestoreAfterQuickMediaAsync()
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

            _quickMediaManager.ClearActiveButton();
            _quickMediaManager.ClearForeverButton();
            if (State.Mode == DisplayMode.Playlist && !_stopping)
                _playlistHandler.Start();
        }

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
                    _quickMediaHandler.Stop();
                    _playlistHandler.Start();
                    _modeManager.UpdateState(new ModeState(DisplayMode.Playlist, saveError, preferredMode));
                    _quickMediaManager.ClearActiveButton();
                    _quickMediaManager.ClearForeverButton();
                    _logger.LogInformation("{tag} Switched to Playlist mode.", _logTag);
                    return;
                }

                if (mode == DisplayMode.QuickMedia)
                {
                    _playlistHandler.Stop();
                    _modeManager.UpdateState(new ModeState(DisplayMode.QuickMedia, saveError, preferredMode));
                    if (!_quickMediaHandler.ResumeForever())
                    {
                        await RestoreAfterQuickMediaAsync();
                        return;
                    }
                    _logger.LogInformation("{tag} Switched to QuickMedia mode.", _logTag);
                    return;
                }
            }
            finally
            {
                _transition.Release();
            }
        }
    }
}
