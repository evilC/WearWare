using WearWare.Common.Media;
using WearWare.Architecture.Playlist;

namespace WearWare.Services.MediaController
{
    public class MediaControllerService
    {
        public event Action? StateChanged;
        private readonly IMediaPlayer _mediaPlayer;
        private readonly object _controlLock = new object();
        private volatile bool _running = false;
        private bool _suspended;
        private bool _playlistRequested;
        private PlaylistItems? _playlist;
        private PlayableItem? _currentItem = null;
        private bool _currentItemIsQuickMedia = false;
        private PlayableItem? _quickMediaItem = null;
        // Requesting play of a quickmedia item in LOOP or DURATION mode whilst another quickmedia item is playing in FOREVER mode...
        // ... causes the FOREVER item to be interrupted temporarily. We store the interrupted item here so we can resume it afterwards.
        private PlayableItem? _interruptedQuickMediaForeverItem = null;
        // Task variables START
        private Task? _playTask;
        private CancellationTokenSource? _cts;
        private readonly ILogger<MediaControllerService> _logger;
        private readonly string _logTag = "[MEDIACONTROLLER]";

        public MediaControllerService(ILogger<MediaControllerService> logger, IMediaPlayer mediaPlayer)
        {
            _logger = logger;
            _mediaPlayer = mediaPlayer;
            _logger.LogInformation("{tag} initialized.", _logTag);
        }

        /// <summary>
        /// Loads a new playlist into the MediaControllerService.
        /// Used by the PlaylistService to set the current playlist.
        /// </summary>
        public void LoadPlaylist(PlaylistItems playlist)
        {
            lock (_controlLock)
            {
                _playlist = playlist;
            }
        }

        /// <summary>
        /// Checks if the MediaControllerService is currently running (playing).
        /// Used by other services to check playback status.
        /// </summary>
        public bool IsRunning()
        {
            return _running;
        }

        /// <summary>
        /// Gets the current playlist.
        /// Used by the PlaylistService to determine which playlist is currently loaded.
        /// </summary>
        public PlaylistItems? GetCurrentPlaylist()
        {
            return _playlist;
        }

        /// <summary>
        /// Gets the current playing item.
        /// Used by the QuickMediaService to check if a Quick Media item is currently playing
        /// </summary>
        public bool IsCurrentItem(PlayableItem item)
        {
            return _currentItem == item;
        }

        /// <summary>
        /// Starts playback of the current playlist or quick media item.
        /// If already playing, does nothing.
        /// If No playlist or quick media item is loaded, logs a warning and does nothing.
        /// </summary>
        public void Start()
        {
            lock (_controlLock)
            {
                _playlistRequested = true;
                if (!_suspended)
                {
                    StartPlayback();
                }
            }
        }

        private void StartPlayback()
        {
            if (_running)
            {
                _logger.LogWarning("{tag} Start called but playback is already running.", _logTag);
                return;
            }
            if ((_playlist == null || _playlist.GetNextElegibleItemIndex() == null) && _quickMediaItem == null)
            {
                _logger.LogWarning("{tag} No playlist or quick media item loaded, cannot start playback.", _logTag);
                return;
            }

            if (_playTask == null || _playTask.IsCompleted)
            {
                try {
                    _cts = new CancellationTokenSource();
                    var ct = _cts.Token;
                    _running = true;
                    _playTask = Task.Run(() => PlayLoop(ct));
                }
                catch (Exception ex)
                {
                    _running = false;
                    _cts?.Dispose();
                    _cts = null;
                    _logger.LogError(ex, "{tag} Exception starting PlayLoop task: {message}", _logTag, ex.Message);
                }
            }
        }

        /// <summary>
        /// Stops playback of the current playlist or quick media item.
        /// </summary>
        public void Stop()
        {
            lock (_controlLock)
            {
                _playlistRequested = false;
                StopPlayback();
            }
        }

        /// <summary>
        /// Suspends playback, typically for entering a different mode such as Physics.
        /// </summary>
        internal void Suspend()
        {
            lock (_controlLock)
            {
                _suspended = true;
                if (_currentItemIsQuickMedia && _currentItem?.PlayMode == PlayMode.Forever)
                {
                    // If Suspended while playing a Forever quick media item, remember it to resume later.
                    _interruptedQuickMediaForeverItem = _currentItem;
                }
                StopPlayback();
                _quickMediaItem = null;
            }
        }

        /// <summary>
        /// Resumes playback after being suspended.
        /// </summary>
        internal void Resume()
        {
            lock (_controlLock)
            {
                _suspended = false;
                if (_playlistRequested)
                {
                    StartPlayback();
                }
            }
        }

        private void StopPlayback()
        {
            _running = false;
            _currentItem = null;
            // Signal cancellation to the play loop/task
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            // Wait for the play task to complete so callers can be sure cleanup has run.
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
        }


        /// <summary>>
        /// Main playback loop. Runs in a separate task.
        /// Plays items from the current playlist, or a quick media item if set.
        /// </summary>
        private void PlayLoop(CancellationToken ct)
        {
            try
            {
                var resuming = true;
                while ((_playlist != null || _quickMediaItem != null || _interruptedQuickMediaForeverItem != null) && _running)
                {
                    PlayableItem? item;
                    if (_quickMediaItem == null && _interruptedQuickMediaForeverItem != null)
                    {
                        // Overriding quickmedia item finished, resume overridden quickmedia item
                        item = _interruptedQuickMediaForeverItem;
                        _currentItemIsQuickMedia = true;
                        _interruptedQuickMediaForeverItem = null; // Reset after resuming
                    }
                    else if (_quickMediaItem != null)
                    {
                        // Quickmedia item is next to play
                        item = _quickMediaItem;
                        _currentItemIsQuickMedia = true;
                        _quickMediaItem = null; // Reset after playing
                    }
                    else
                    {
                        _currentItemIsQuickMedia = false;
                        // Null forgiving operator seems to be needed here, else VSCode moans. We checked above...
                        // If _playlist was null, then _quickMediaItem would have been used instead.
                        var currentItem = _playlist!.GetCurrentItem();
                        if (resuming && currentItem != null)
                        {
                            item = currentItem;
                        }
                        else
                        {
                            item = _playlist!.MoveNext();
                        }
                        resuming = false;
                    }
                    if (item == null)
                    {
                        break;  // No more items in playlist
                    }
                    _currentItem = item;
                    var success = _mediaPlayer.PlayFseq(item, ct);
                    if (!success && !ct.IsCancellationRequested)
                    {
                        if (_currentItemIsQuickMedia){
                            _logger.LogError("{tag} Playback failed for quickmedia item {item}.", _logTag, item.Name);
                        }
                        else
                        {
                            _logger.LogError("{tag} Playback failed for playlist item {item}. Disabling item.", _logTag, item.Name);
                            // Playback failed - disable item.
                            item.Enabled = false;
                            // Notify any listeners (eg PlaylistService and QuickMediaService) that state has changed
                            // They will then raise their own StateChanged events to update UIs etc.
                            StateChanged?.Invoke();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Exception in PlayLoop: {message}", _logTag, ex.Message);
            }
            finally
            {
                _running = false;
            }
        }

        /// <summary>
        /// Plays a Quick Media item immediately.
        /// Used by the QuickMediaService when a Quick Media button is pressed.
        /// If the item to be played is a Quick Media item and is already playing in FOREVER mode, stops it.
        /// </summary>
        internal void PlayQuickMedia(PlayableItem item)
        {
            lock (_controlLock)
            {
                if (_suspended)
                {
                    return;
                }
                if (item.PlayMode == PlayMode.Forever)
                {
                    // Requested item to play in FOREVER mode
                    if (item == _currentItem)
                    {
                        // Currently playing this item, so stop it
                        _quickMediaItem = null;
                    }
                    else
                    {
                        // Request play of new FOREVER quickmedia item - replace existing one
                        _quickMediaItem = item;
                    }
                }
                else
                {
                    // Requested play of item in LOOP or DURATION mode
                    if (_currentItemIsQuickMedia && _currentItem != null && _currentItem.PlayMode == PlayMode.Forever)
                    {
                        // Currently playing a different quickmedia item in FOREVER mode.
                        // Push it to _interruptedQuickMediaForeverItem so we can resume it later.
                        _interruptedQuickMediaForeverItem = _currentItem;
                    }
                    _quickMediaItem = item;
                }
                StopPlayback();
                StartPlayback();
            }
        }
    }
}