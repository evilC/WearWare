using WearWare.Architecture.Playlist;
using WearWare.Architecture.QuickMedia;

namespace WearWare.Architecture.Mode
{
    public sealed class ModeHandler : IHostedService
    {
        private readonly SemaphoreSlim _transition = new SemaphoreSlim(1, 1);
        private readonly ModeManager _modeManager;
        private readonly PlaylistHandler _playlistHandler;
        private readonly QuickMediaHandler _quickMediaHandler;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<ModeHandler> _logger;
        private readonly string _logTag = "[MODEHANDLER]";
        private volatile bool _stopping;

        public event Action? StateChanged;
        public ModeState State => _modeManager.State;

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
            _lifetime = lifetime;
            _logger = logger;
            _modeManager.StateChanged += OnModeManagerStateChanged;
        }

        private void OnModeManagerStateChanged()
        {
            StateChanged?.Invoke();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{tag} starting.", _logTag);

            _lifetime.ApplicationStopping.Register(() =>
            {
                _stopping = true;
            });

            if (State.Mode == DisplayMode.QuickMedia)
            {
                _quickMediaHandler.Start();
            }
            else
            {
                _playlistHandler.Start();
            }

            return Task.CompletedTask;
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
                    _logger.LogInformation("{tag} Switched to Playlist mode.", _logTag);
                    return;
                }

                if (mode == DisplayMode.QuickMedia)
                {
                    _playlistHandler.Stop();
                    _quickMediaHandler.Start();
                    _modeManager.UpdateState(new ModeState(DisplayMode.QuickMedia, saveError, preferredMode));
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
