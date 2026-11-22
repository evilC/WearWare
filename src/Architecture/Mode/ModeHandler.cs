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
            IHostApplicationLifetime lifetime,
            ILogger<ModeHandler> logger)
        {
            _modeManager = modeManager;
            _playlistHandler = playlistHandler;
            _quickMediaHandler = quickMediaHandler;
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
            if (mode != DisplayMode.Playlist && mode != DisplayMode.QuickMedia)
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

            var playlistWasRunning = false;
            try
            {
                playlistWasRunning = _playlistHandler.IsRunning();
                _playlistHandler.Stop();
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
            else if (playlistWasRunning && !_stopping)
                _playlistHandler.Start();
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

            if (State.Mode == DisplayMode.Playlist && !_stopping)
                _playlistHandler.Start();
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
                    _quickMediaHandler.Deactivate();
                    _playlistHandler.Start();
                    _modeManager.UpdateState(new ModeState(DisplayMode.Playlist, saveError, preferredMode));
                    _logger.LogInformation("{tag} Switched to Playlist mode.", _logTag);
                    return;
                }

                if (mode == DisplayMode.QuickMedia)
                {
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
            }
            finally
            {
                _transition.Release();
            }
        }
    }
}
