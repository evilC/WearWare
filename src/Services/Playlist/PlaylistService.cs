using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.Library;
using WearWare.Services.MediaController;

namespace WearWare.Services.Playlist
{
    public class PlaylistService
    {
        private Dictionary<string, PlaylistItems> _playlists = [];
        private readonly MediaControllerService _mediaController;
        private readonly StreamConverter.IStreamConverterService _streamConverterService;
        public event Action? StateChanged;
        private readonly ILogger<PlaylistService> _logger;
        private static readonly string _logTag = "[PLAYLISTSERV]";
        private readonly AppConfig _appConfig;

        public PlaylistService(ILogger<PlaylistService> logger, AppConfig appConfig, MediaControllerService mediaController, StreamConverter.IStreamConverterService streamConverterService)
        {
            _logger = logger;
            _appConfig = appConfig;
            _mediaController = mediaController;
            _mediaController.StateChanged += OnMediaControllerStateChanged;
            _streamConverterService = streamConverterService;
        }

        /// <summary>
        /// The MediaControllerService notified us that something changed
        /// This happens eg if an item failed to play and was disabled
        /// </summary>
        private void OnMediaControllerStateChanged()
        {
            // Notify the UI that something changed
            // This will cause the playlist item to show as disabled
            StateChanged?.Invoke();
        }

        public void Initialize()
        {
            Directory.CreateDirectory(PathConfig.PlaylistPath);
            _logger.LogInformation("{LogTag} Loading playlists from {PlaylistPath}...", _logTag, PathConfig.PlaylistPath);
            foreach (var dir in Directory.GetDirectories(PathConfig.PlaylistPath))
            {
                var playlistName = Path.GetFileName(dir);
                LoadPlaylist(playlistName);
            }
            _logger.LogInformation("{LogTag} Loaded playlists.", _logTag);
            // Load editing playlist if set
            if (string.IsNullOrEmpty(_appConfig.EditingPlaylist) || !_playlists.ContainsKey(_appConfig.EditingPlaylist))
            {
                _appConfig.EditingPlaylist = null; // Clear invalid editing playlist
            }
            if (string.IsNullOrEmpty(_appConfig.ActivePlaylist) || !_playlists.ContainsKey(_appConfig.ActivePlaylist))
            {
                _appConfig.ActivePlaylist = null; // Clear invalid active playlist
            }
            else
            {
                var activePlaylist = _playlists[_appConfig.ActivePlaylist];
                _logger.LogInformation("{LogTag} Starting MediaController with active playlist: {PlaylistName}", _logTag, _appConfig.ActivePlaylist);
                _mediaController.LoadPlaylist(activePlaylist);
                _mediaController.Start();
            }
        }

        /// <summary>
        /// Loads the specified playlist from disk
        /// </summary>
        /// <param name="playlistName"></param> The name of the playlist to load
        public void LoadPlaylist(string playlistName)
        {
            try
            {
                var deserializedPlaylist = PlaylistItems.Deserialize(playlistName);
                if (deserializedPlaylist == null)
                {
                    _logger.LogError("{LogTag} Failed to load playlist: {PlaylistName}", _logTag, playlistName);
                    return;
                }
                _playlists[playlistName] = deserializedPlaylist;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{LogTag} Exception loading playlist: {PlaylistName}", _logTag, playlistName);
                return;
            }
            // _playlistBeingEdited = _playlists[playlistName];

        }

        /// <summary>
        /// Gets the names of all loaded playlists
        /// </summary>
        /// <returns></returns>
        public List<string> GetPlaylistNames()
        {
            return _playlists.Keys.ToList();
        }

        /// <summary>
        /// Gets the specified playlist
        /// </summary>
        /// <param name="playlistName"></param>
        /// <returns></returns>
        public PlaylistItems? GetPlaylist(string playlistName)
        {
            if (_playlists.ContainsKey(playlistName))
            {
                return _playlists[playlistName];
            }
            return null;
        }

        /// <summary>
        /// Gets the currently active playlist name
        /// </summary>
        public string? GetActivePlaylistName()
        {
            return !string.IsNullOrEmpty(_appConfig.ActivePlaylist) && _playlists.ContainsKey(_appConfig.ActivePlaylist)
                ? _appConfig.ActivePlaylist
                : null;
        }

        /// <summary>
        /// Gets the playlist being edited
        /// </summary>
        /// <returns></returns>
        public PlaylistItems? GetPlaylistBeingEdited()
        {
            return !string.IsNullOrEmpty(_appConfig.EditingPlaylist) && _playlists.ContainsKey(_appConfig.EditingPlaylist)
                ? _playlists[_appConfig.EditingPlaylist]
                : null;
        }

        public bool PlaylistIsPlaying(PlaylistItems playlist)
        {
            var currentPlaylist = _mediaController.GetCurrentPlaylist();
            if (currentPlaylist == null) return false;
            return currentPlaylist == playlist && _mediaController.IsRunning();
        }

        /// <summary>
        /// Jumps to the specified item in the playlist being played
        /// </summary>
        /// <param name="playlist"></param> The playlist to jump in
        /// <param name="itemIndex"></param> The index of the item to jump to
        public void JumpToPlaylistItem(PlaylistItems playlist, int itemIndex)
        {
            if (!PlaylistIsPlaying(playlist))
            {
                return;
            }
            playlist.JumpToPlaylistIndex(itemIndex);
            _mediaController.Stop();
            _mediaController.Start();
        }

        /// <summary>
        /// Adds a new item to the playlist being edited
        /// </summary>
        /// <param name="playlist"></param> The playlist to add the item to
        /// <param name="insertIndex"></param> The index to insert the item at
        /// <param name="libraryItem"></param> The library item to add
        /// <param name="playMode"></param> The play mode for the item
        /// <param name="playModeValue"></param> The play mode value for the item
        public void AddPlaylistItem(PlaylistItems playlist, int insertIndex, LibraryItem libraryItem, PlayMode playMode, int playModeValue)
        {
            var restartMediaController = false;
            if (PlaylistIsPlaying(playlist))
            {
                // Currently editing playlist and media controller is running, need to restart after adding item
                _mediaController.Stop();
                restartMediaController = true;
            }
            // Create PlayableItem from LibraryItem
            var item = new PlayableItem(
                name: libraryItem.Name,
                // parentFolder: GetPlaylistPath(playlist),
                parentFolder: playlist.GetPlaylistPath(),
                mediaType: libraryItem.MediaType,
                sourceFileName: libraryItem.SourceFileName,
                playMode: playMode,
                playModeValue: playModeValue);
            
            // Copy file from library to playlist folder
            var sourcePath = Path.Combine(PathConfig.LibraryPath, item.SourceFileName);
            var destPath = Path.Combine(PathConfig.PlaylistPath, playlist.Name, item.SourceFileName);
            if (!File.Exists(destPath)){
                File.Copy(sourcePath, destPath, overwrite: true);
            }
            destPath = Path.Combine(PathConfig.PlaylistPath, playlist.Name, $"{item.Name}.stream");
            if (!File.Exists(destPath)){
                sourcePath = Path.Combine(PathConfig.LibraryPath, $"{item.Name}.stream");
                File.Copy(sourcePath, destPath, overwrite: true);
            }
            // ToDo: Check if playlist returns true
            playlist.AddItem(insertIndex, item);
            playlist.Serialize();
            if (restartMediaController)
            {
                _mediaController.Start();
            }
        }

        // ToDo: Should the bulk of this not be in PlaylistItems?
        /// <summary>
        /// Removes a playlist item from the playlist being edited
        /// </summary>
        /// <param name="playlist"></param> The playlist to remove the item from
        /// <param name="removedIndex"></param> The index of the item to remove
        /// <returns>True if the item was removed, false otherwise</returns>
        public bool RemovePlaylistItem(PlaylistItems playlist, int removedIndex)
        {
            var restartMediaController = false;
            if (PlaylistIsPlaying(playlist))
            {
                // Currently editing playlist and media controller is running, need to restart after removing item
                _mediaController.Stop();
                restartMediaController = true;
            }
            var item = playlist.GetPlaylistItems()[removedIndex];
            if (item == null)
            {
                // ToDo: log error
                return false;
            }
            // Check if any other items are using the same source file
            int dupeCheckIndex = 0;
            bool deleteFiles = true;
            foreach (var playlistItem in playlist.GetPlaylistItems())
            {
                if (dupeCheckIndex != removedIndex && playlistItem.SourceFileName == item.SourceFileName)
                {
                    // Another item is using the same source file, do not delete
                    deleteFiles = false;
                    break;
                }
                dupeCheckIndex++;
            }
            if (deleteFiles)
            {
                var path = playlist.GetPlaylistPath();
                File.Delete(Path.Combine(path, $"{item.Name}.stream"));
                File.Delete(Path.Combine(path, item.SourceFileName));
            }

            // Actually remove the item from the playlist
            var removed = playlist.RemoveItem(removedIndex);
            // ToDo: Check if removed
            playlist.Serialize();

            // Restart media controller if there are still items to play
            if (restartMediaController && playlist.GetCurrentItem() != null)
            {
                _mediaController.Start();
            }
            return removed;
        }

        /// <summary>
        /// Re-processes the specified playlist item using the provided matrix options.
        /// Overwrites the existing .stream in the playlist folder.
        /// </summary>
        public async Task<WearWare.Common.TaskResult> ReprocessPlaylistItem(PlaylistItems playlist, int itemIndex, WearWare.Services.MatrixConfig.LedMatrixOptionsConfig? options = null)
        {
            var item = playlist.GetPlaylistItems()[itemIndex];
            var folder = playlist.GetPlaylistPath();
            // var restartMediaController = false;
            // if (PlaylistIsPlaying(playlist))
            // {
            //     _mediaController.Stop();
            //     restartMediaController = true;
            // }
            try
            {
                var result = await _streamConverterService.ConvertToStream(folder, item.SourceFileName, folder, item.Name, options);
                if (result.ExitCode == 0)
                {
                    StateChanged?.Invoke();
                }
                return result;
            }
            finally
            {
                // if (restartMediaController && playlist.GetCurrentItem() != null)
                // {
                //     _mediaController.Start();
                // }
            }
        }

        /// <summary>
        /// Updates a playlist item that has been edited
        /// </summary>
        /// <param name="playlist"></param> The playlist containing the item that was edited
        /// <param name="itemIndex"></param> The index of the item that was edited
        /// <returns>True if the item was updated, false otherwise</returns>
        // ToDo: Do we need to notify MediaControllerService of the update?
        // ToDo: If item enabled state changed, we may need to start/stop MediaControllerService
        public bool PlaylistItemUpdated(PlaylistItems playlist, int itemIndex)
        {
            bool restartMediaController = false;
            if (PlaylistIsPlaying(playlist))
            {
                // Currently editing playlist and media controller is running, need to restart after removing item
                _mediaController.Stop();
                restartMediaController = true;
            }
            var item = playlist.GetPlaylistItems()[itemIndex];
            if (item == null)
            {
                // ToDo: log error
                return false;
            }
            if (!item.Enabled)
            {
                // If item is now disabled, and it is the current item, we need to move to the next item
                if (playlist.GetCurrentItem() == item)
                {
                    if (playlist.MoveNext() == null)
                    {
                        restartMediaController = false; // No more items to play
                    }
                }
            }
            playlist.Serialize();
            // Restart media controller if there are still items to play
            if (restartMediaController && playlist.GetCurrentItem() != null)
            {
                _mediaController.Start();
            }
            return true;
        }

        /// <summary>
        /// Called when the editing playlist is changed from the UI
        /// </summary>
        /// <param name="playlistName"></param>
        public void OnEditingPlaylistChanged(string? playlistName)
        {
            _appConfig.EditingPlaylist = playlistName;
            _appConfig.Serialize();
        }

        /// <summary>
        /// Sets the active playlist state (start/stop)
        /// </summary>
        /// <param name="newState">True to start, false to stop</param>
        /// <param name="playlistName">The playlist to start/stop</param>
        public void SetActivePlaylistState(bool newState, string? playlistName = null)
        {
            if (playlistName == "")
                playlistName = null;
            if (newState)
            {
                if (string.IsNullOrEmpty(playlistName) || !_playlists.ContainsKey(playlistName))
                {
                    _logger.LogWarning("{LogTag} Tried to start non-existent playlist: {PlaylistName}", _logTag, playlistName);
                    return;
                }
            }
            _appConfig.ActivePlaylist = playlistName;
            _appConfig.Serialize();
            
            if (newState)
            {
                var playlist = _playlists[playlistName!]; // Null-forgiving operator seems to be needed here, else VSCode moans. We checked above...
                _mediaController.Stop();
                _mediaController.LoadPlaylist(playlist);
                _mediaController.Start();
            }
            else
            {
                _mediaController.Stop();
            }
        }

        /// <summary>
        /// Adds a new playlist
        /// </summary>
        /// <param name="playlistName"></param> The name of the new playlist
        public void AddPlaylist(string playlistName)
        {
            if (string.IsNullOrEmpty(playlistName) || _playlists.ContainsKey(playlistName))
                return;
            var playlist = new PlaylistItems(playlistName, new PlaylistItemsConfig(), []);
            _playlists[playlistName] = playlist;
            // Create playlist folder
            var path = Path.Combine(PathConfig.PlaylistPath, playlistName);
            Directory.CreateDirectory(path);
            playlist.Serialize();
        }

        /// <summary>
        /// Deletes the specified playlist
        /// </summary>
        /// <param name="playlistName"></param> The name of the playlist to delete
        public void DeletePlaylist(string playlistName)
        {
            if (string.IsNullOrEmpty(playlistName) || !_playlists.ContainsKey(playlistName))
                return;
            // If the playlist is active, stop it
            if (_appConfig.ActivePlaylist == playlistName)
            {
                _mediaController.Stop();
                _appConfig.ActivePlaylist = null;
            }
            // If the playlist is being edited, clear it
            if (_appConfig.EditingPlaylist == playlistName)
            {
                _appConfig.EditingPlaylist = null;
            }
            _appConfig.Serialize();
            _playlists.Remove(playlistName);
            // Delete the playlist folder
            var path = Path.Combine(PathConfig.PlaylistPath, playlistName);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}