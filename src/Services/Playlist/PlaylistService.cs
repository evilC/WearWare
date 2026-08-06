using WearWare.Common.Media;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Config;
using WearWare.Services.MatrixConfig;
using WearWare.Services.MediaController;
using WearWare.Services.OperationProgress;
using WearWare.Services.StreamConverter;
using WearWare.Utils;

namespace WearWare.Services.Playlist
{
    public class PlaylistService
    {
        private Dictionary<string, PlaylistItems> _playlists = new();
        private readonly MediaControllerService _mediaController;
        private readonly IStreamConverterService _streamConverterService;
        public event Action? StateChanged;
        private readonly ILogger<PlaylistService> _logger;
        private static readonly string _logTag = "[PLAYLISTSERV]";
        private readonly PlaylistsConfig _config;
        // Logger factory for injecting loggers for PlaylistItems
        private readonly ILoggerFactory _loggerFactory;
        private readonly MatrixConfigService _matrixConfigService;
        private readonly IOperationProgressService _operationProgress;

        public PlaylistService(
            ILogger<PlaylistService> logger,
            MediaControllerService mediaController,
            IStreamConverterService streamConverterService,
            MatrixConfigService matrixConfigService,
            IOperationProgressService operationProgress,
            ILoggerFactory loggerFactory
            )
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            var config = PlaylistsConfig.Deserialize();
            if (config == null)
            {
                _logger.LogWarning("{LogTag} Failed to load app config, creating default.", _logTag);
                config = new PlaylistsConfig();
                config.Serialize();
            }
            _config = config;
            _mediaController = mediaController;
            _mediaController.StateChanged += OnMediaControllerStateChanged;
            _streamConverterService = streamConverterService;
            _matrixConfigService = matrixConfigService;
            _operationProgress = operationProgress;
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
            var serialize = false;
            if (string.IsNullOrEmpty(_config.EditingPlaylist) || !_playlists.ContainsKey(_config.EditingPlaylist))
            {
                _logger.LogInformation("{LogTag} Non-existent editing playlist: {EditingPlaylist}, clearing", _logTag, _config.EditingPlaylist);
                _config.EditingPlaylist = null; // Clear invalid editing playlist
                serialize = true;
            }
            if (string.IsNullOrEmpty(_config.ActivePlaylist) || !_playlists.ContainsKey(_config.ActivePlaylist))
            {
                _logger.LogInformation("{LogTag} Non-existent active playlist: {ActivePlaylist}, clearing", _logTag, _config.ActivePlaylist);
                _config.ActivePlaylist = null; // Clear invalid active playlist
                serialize = true;
            }
            else
            {
                var activePlaylist = _playlists[_config.ActivePlaylist];
                _logger.LogInformation("{LogTag} Starting MediaController with active playlist: {PlaylistName}", _logTag, _config.ActivePlaylist);
                _mediaController.LoadPlaylist(activePlaylist);
                _mediaController.Start();
            }
            if (serialize)
            {
                // Bad config detected, re-save
                _config.Serialize();
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
                var deserializedPlaylist = PlaylistItems.Deserialize(_loggerFactory.CreateLogger<PlaylistItems>(), playlistName, _matrixConfigService);
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
            return !string.IsNullOrEmpty(_config.ActivePlaylist) && _playlists.ContainsKey(_config.ActivePlaylist)
                ? _config.ActivePlaylist
                : null;
        }

        /// <summary>
        /// Gets the playlist being edited
        /// </summary>
        /// <returns></returns>
        public PlaylistItems? GetPlaylistBeingEdited()
        {
            return !string.IsNullOrEmpty(_config.EditingPlaylist) && _playlists.ContainsKey(_config.EditingPlaylist)
                ? _playlists[_config.EditingPlaylist]
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
        public async Task AddPlaylistItem(PlaylistItems playlist,
            int insertIndex,
            PlayableItem libraryItem,
            PlayMode playMode,
            int playModeValue,
            int relativeBrightness)
        {
            var restartMediaController = false;
            if (PlaylistIsPlaying(playlist))
            {
                _mediaController.Stop();
                restartMediaController = true;
            }

            var item = new PlayableItem(
                name: libraryItem.Name,
                parentFolder: Path.Combine(PathConfig.PlaylistFolder, playlist.Name),
                mediaType: libraryItem.MediaType,
                sourceFileName: libraryItem.SourceFileName,
                playMode: playMode,
                playModeValue: playModeValue,
                relativeBrightness: relativeBrightness
            );

            var destPath = item.GetSourceFilePath();
            if (!File.Exists(destPath))
            {
                await FileUtils.CopyFileAsync(libraryItem.GetSourceFilePath(), destPath).ConfigureAwait(false);
            }

            destPath = item.GetFseqFilePath();
            if (!File.Exists(destPath))
            {
                await FileUtils.CopyFileAsync(libraryItem.GetFseqFilePath(), destPath).ConfigureAwait(false);
            }

            playlist.AddItem(insertIndex, item);
            playlist.Serialize();
            if (restartMediaController)
            {
                _mediaController.Start();
            }
        }

        /// <summary>
        /// Called when OK is clicked in the EditPlayableItemForm
        /// </summary>
        /// <param name="playlist"></param> The playlist being edited
        /// <param name="itemIndex"></param> The index of the item being edited
        /// This is not used any more, but keep for now.
        /// The equivalent method in QuickMediaService still uses it.
        /// <param name="originalItem"></param> The original item before editing
        /// <param name="updatedItem"></param> The updated item from the form
        /// <param name="formMode"></param> The mode of the form (ADD or EDIT)
        /// </summary>
        public async Task OnEditFormSubmit(PlaylistItems playlist, EditPlayableItemFormModel formModel)
        {
            // ToDo: Try / catch needed in here
            var opId = await _operationProgress.StartOperation("Updating Playlist Item");
            bool restartMediaController = false;
            if (PlaylistIsPlaying(playlist))
            {
                // Currently editing playlist and media controller is running, need to restart after removing item
                _mediaController.Stop();
                restartMediaController = true;
            }
            if (formModel.FormMode == EditPlayableItemFormMode.Add)
            {
                // In ADD mode, the originalItem is from the library, so we need to set the ParentFolder of updatedItem
                formModel.UpdatedItem.ParentFolder = playlist.GetPlaylistRelativePath();
            }

            if (formModel.FormMode == EditPlayableItemFormMode.Add)
            {
                _operationProgress.ReportProgress(opId, "Copying fseq file...");
                var copyFrom = formModel.OriginalItem.GetFseqFilePath();
                var copyTo = formModel.UpdatedItem.GetFseqFilePath();
                await FileUtils.CopyFileAsync(copyFrom, copyTo).ConfigureAwait(false);
            }
            if (formModel.FormMode == EditPlayableItemFormMode.Add)
            {
                _operationProgress.ReportProgress(opId, "copying source file...");
                // Copy source file from library to playlist folder
                var copyFrom = formModel.OriginalItem.GetSourceFilePath();    // From library folder
                var copyTo = formModel.UpdatedItem.GetSourceFilePath();       // To playlist folder
                if (!File.Exists(copyTo))
                {
                    await FileUtils.CopyFileAsync(copyFrom, copyTo).ConfigureAwait(false);
                }
            }
            _operationProgress.ReportProgress(opId, "Updating playlist...");
            if (formModel.FormMode == EditPlayableItemFormMode.Add)
            {
                // Add new item
                playlist.AddItem(formModel.ItemIndex, formModel.UpdatedItem);
            }
            else
            {
                formModel.OriginalItem.UpdateFromClone(formModel.UpdatedItem);
                if (!formModel.OriginalItem.Enabled)
                {
                    // If item is now disabled, and it is the current item, we need to move to the next item
                    if (playlist.GetCurrentItem() == formModel.OriginalItem)
                    {
                        if (playlist.MoveNext() == null)
                        {
                            restartMediaController = false; // No more items to play
                        }
                    }
                }
            }

            playlist.Serialize();
            // Restart media controller if there are still items to play
            if (restartMediaController && playlist.GetCurrentItem() != null)
            {
                _mediaController.Start();
            }
            _operationProgress.CompleteOperation(opId, true, "Done");
            // return true;
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
                return false;
            }

            playlist.RemoveItem(removedIndex);
            playlist.Serialize();

            if (restartMediaController && playlist.GetCurrentItem() != null)
            {
                _mediaController.Start();
            }

            return true;
        }

        /// <summary>
        /// Sets the playlist currently being edited
        /// </summary>
        /// <param name="playlistName"></param> The playlist to edit
        public void OnEditingPlaylistChanged(string? playlistName)
        {
            if (playlistName == "")
                playlistName = null;
            _config.EditingPlaylist = playlistName;
            _config.Serialize();
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
            _config.ActivePlaylist = playlistName;
            _config.Serialize();
            
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
            var playlist = new PlaylistItems(_loggerFactory.CreateLogger<PlaylistItems>(), playlistName, new PlaylistItemsConfig(), []);
            _playlists[playlistName] = playlist;
            // Create playlist folder
            var path = Path.Combine(PathConfig.PlaylistPath, playlistName);
            Directory.CreateDirectory(path);
            playlist.Serialize();
        }

        /// <summary>
        /// Copies an existing playlist to a new playlist
        /// </summary>
        /// <param name="oldName"></param> The name of the existing playlist
        /// <param name="newName"></param> The name of the new playlist
        /// </summary>
        internal async Task CopyPlaylist(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName) || !_playlists.ContainsKey(oldName) || _playlists.ContainsKey(newName))
                return;
            try
            {
                var opId = await _operationProgress.StartOperation($"Copying Playlist {oldName} to {newName}");
                var oldPlaylist = _playlists[oldName];
                _operationProgress.ReportProgress(opId, "Cloning playlist...");
                var newPlaylist = oldPlaylist.Clone(newName);
                _operationProgress.ReportProgress(opId, "Copying playlist files...");
                // Copy playlist folder
                var oldPath = Path.Combine(PathConfig.PlaylistPath, oldName);
                var newPath = Path.Combine(PathConfig.PlaylistPath, newName);
                Directory.CreateDirectory(newPath);
                foreach (var file in Directory.GetFiles(oldPath))
                {
                    _operationProgress.ReportProgress(opId, $"Copying file {Path.GetFileName(file)}...");
                    var destFile = Path.Combine(newPath, Path.GetFileName(file));
                    await FileUtils.CopyFileAsync(file, destFile);
                }
                _playlists[newName] = newPlaylist;
                _operationProgress.ReportProgress(opId, "Saving new playlist...");
                newPlaylist.Serialize();
                _operationProgress.CompleteOperation(opId, true, "Done");
            }
            catch (Exception ex)
            {
                if (_playlists.ContainsKey(newName))
                {
                    _playlists.Remove(newName);
                }
                _logger.LogError(ex, "{LogTag} Error copying playlist from {OldName} to {NewName}", _logTag, oldName, newName);
                return;
            }
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
            if (_config.ActivePlaylist == playlistName)
            {
                _mediaController.Stop();
                _config.ActivePlaylist = null;
            }
            // If the playlist is being edited, clear it
            if (_config.EditingPlaylist == playlistName)
            {
                _config.EditingPlaylist = null;
            }
            _config.Serialize();
            _playlists.Remove(playlistName);
            // Delete the playlist folder
            var path = Path.Combine(PathConfig.PlaylistPath, playlistName);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        // Uses shared FileUtils.CopyFileAsync from WearWare.Utils to perform async file copies.
    }
}