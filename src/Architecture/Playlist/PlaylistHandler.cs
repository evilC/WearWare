using WearWare.Common.Media;
using WearWare.Architecture.Playback;

namespace WearWare.Architecture.Playlist
{
    public class PlaylistHandler : IHandler, IHostedService
    {
        private readonly PlaylistManager _playlistManager;
        private readonly IMediaPlayer _mediaPlayer;
        private readonly ILogger<PlaylistHandler> _logger;
        private readonly string _logTag = "[PLAYLISTHANDLER]";

        private volatile bool _running = false;
        private Task? _playTask;
        private CancellationTokenSource? _cts;
        private bool _resuming = true;
        private HandlerState _state = HandlerState.Idle;

        public string Name => "Playlist";
        public HandlerState State => _state;
        public event Action? StateChanged;

        /// <summary>
        /// Connects the playlist handler to its manager and media player.
        /// </summary>
        public PlaylistHandler(
            PlaylistManager playlistManager,
            IMediaPlayer mediaPlayer,
            ILogger<PlaylistHandler> logger)
        {
            _playlistManager = playlistManager;
            _mediaPlayer = mediaPlayer;
            _logger = logger;
        }

        /// <summary>
        /// Loads playlists and selects the default if no active playlist is available.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{tag} starting.", _logTag);
            
            // Load all playlists from disk
            _playlistManager.LoadAllPlaylists();

            // Set active playlist to default if not already set
            if (_playlistManager.CurrentPlaylist == null)
            {
                if (!_playlistManager.SetActivePlaylist("default"))
                {
                    _logger.LogWarning("{tag} Failed to load/set default playlist on startup.", _logTag);
                    return;
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops playlist playback during application shutdown.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{tag} stopping.", _logTag);
            Stop();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Starts playback of the current playlist.
        /// </summary>
        internal void Start()
        {
            if (_running)
            {
                _logger.LogWarning("{tag} Start called but playback is already running.", _logTag);
                return;
            }

            if (_playlistManager.CurrentPlaylist == null || 
                _playlistManager.CurrentPlaylist.GetNextElegibleItemIndex() == null)
            {
                _logger.LogWarning("{tag} No valid playlist loaded, cannot start playback.", _logTag);
                SetState(HandlerState.Failed);
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;
                _running = true;
                _resuming = true;
                _playTask = Task.Run(() => PlayLoop(ct));
                SetState(HandlerState.Running);
                _logger.LogInformation("{tag} Playback started.", _logTag);
            }
            catch (Exception ex)
            {
                _running = false;
                _cts?.Dispose();
                _cts = null;
                SetState(HandlerState.Failed);
                _logger.LogError(ex, "{tag} Exception starting PlayLoop task: {message}", _logTag, ex.Message);
            }
        }

        /// <summary>
        /// Stops playback.
        /// </summary>
        internal void Stop()
        {
            if (_playTask == null)
            {
                return;
            }

            _running = false;
            _cts?.Cancel();

            try
            {
                _playTask?.Wait();
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                {
                    _logger.LogError(inner, "{tag} Exception during Stop: {message}", _logTag, inner.Message);
                }
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _playTask = null;
                _mediaPlayer.Clear();
            }

            SetState(HandlerState.Idle);
            _logger.LogInformation("{tag} Playback stopped.", _logTag);
        }

        internal void Clear() => _mediaPlayer.Clear();

        /// <summary>
        /// Updates the handler state and notifies subscribers when it changes.
        /// </summary>
        private void SetState(HandlerState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                StateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Returns whether playlist playback is running.
        /// </summary>
        public bool IsRunning() => _running;

        /// <summary>
        /// Main playback loop. Plays items from the current playlist in sequence.
        /// </summary>
        private void PlayLoop(CancellationToken ct)
        {
            try
            {
                while (_running && _playlistManager.CurrentPlaylist != null)
                {
                    PlayableItem? item;

                    if (_resuming)
                    {
                        // Resume from current item (after restart)
                        item = _playlistManager.GetCurrentItem();
                        _resuming = false;
                    }
                    else
                    {
                        // Move to next item
                        item = _playlistManager.GetNextItem();
                    }

                    if (item == null)
                    {
                        _logger.LogInformation("{tag} Reached end of playlist.", _logTag);
                        break;
                    }

                    _logger.LogInformation("{tag} Playing item: {item} (mode: {mode}, value: {value})",
                        _logTag, item.Name, item.PlayMode, item.PlayModeValue);

                    var success = _mediaPlayer.PlayFseq(item, ct);

                    if (!success && !ct.IsCancellationRequested)
                    {
                        _logger.LogError("{tag} Playback failed for item {item}. Disabling item.", _logTag, item.Name);
                        item.Enabled = false;
                        StateChanged?.Invoke();
                    }

                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("{tag} PlayLoop cancelled.", _logTag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Unhandled exception in PlayLoop: {message}", _logTag, ex.Message);
            }
            finally
            {
                _running = false;
            }
        }
    }
}
