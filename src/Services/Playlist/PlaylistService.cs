using WearWare.Common.Media;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Config;
using WearWare.Services.MatrixConfig;
using WearWare.Services.MediaController;
using WearWare.Services.OperationProgress;
using WearWare.Services.StreamConverter;

namespace WearWare.Services.Playlist
{
    public class PlaylistService
    {
        private Dictionary<string, PlaylistItems> _playlists = [];
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
        public void AddPlaylistItem(PlaylistItems playlist,
            int insertIndex,
            PlayableItem libraryItem,
            PlayMode playMode,
            int playModeValue,
            int relativeBrightness,
            int currentBrightness)
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
                parentFolder: Path.Combine(PathConfig.PlaylistFolder, playlist.Name),
                mediaType: libraryItem.MediaType,
                sourceFileName: libraryItem.SourceFileName,
                playMode: playMode,
                playModeValue: playModeValue,
                relativeBrightness: relativeBrightness,
                currentBrightness: currentBrightness,
                _matrixConfigService.CloneOptions()
                );
            
            // Copy file from library to playlist folder
            var destPath = item.GetSourceFilePath();
            if (!File.Exists(destPath)){
                File.Copy(libraryItem.GetSourceFilePath(), destPath, overwrite: true);
            }
            destPath = item.GetStreamFilePath();
            if (!File.Exists(destPath)){
                File.Copy(libraryItem.GetStreamFilePath(), destPath, overwrite: true);
            }
            // ToDo: Check if playlist returns true
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

            if (formModel.OriginalItem.NeedsReConvert(formModel.UpdatedItem)){
                _operationProgress.ReportProgress(opId, "Converting stream...");
                // If the updated item needs re-conversion, do it now
                var readFrom = formModel.FormMode == EditPlayableItemFormMode.Add
                        ? PathConfig.LibraryPath                    // For ADD, source is library folder
                        : playlist.GetPlaylistAbsolutePath();       // For EDIT, source is playlist folder
                var writeTo = playlist.GetPlaylistAbsolutePath();   // For both ADD and EDIT, destination is playlist folder
                var result = await _streamConverterService.ConvertToStream(readFrom, formModel.UpdatedItem.SourceFileName, writeTo, formModel.UpdatedItem.Name, formModel.UpdatedItem.RelativeBrightness, formModel.UpdatedItem.MatrixOptions);
                if (result.ExitCode == 0)
                {
                    formModel.UpdatedItem.CurrentBrightness = result.ActualBrightness;
                }
                else
                {
                    // Re-convert failed - show an alert and do not save changes
                    //await JSRuntime.InvokeVoidAsync("alert", $"Re-conversion failed: {result.Error} - {result.Message}");
                    // ToDo: Show error to user
                    _operationProgress.CompleteOperation(opId, false, result.Message + "\n" + result.Error);
                    return;
                }
            }
            else if (formModel.FormMode == EditPlayableItemFormMode.Add)
            {
                _operationProgress.ReportProgress(opId, "Copying stream file...");
                // If in ADD mode but no re-convert needed, we still need to copy the .stream from library to playlist folder
                var copyFrom = formModel.OriginalItem.GetStreamFilePath();    // From library folder
                var copyTo = formModel.UpdatedItem.GetStreamFilePath();       // To playlist folder
                File.Copy(copyFrom, copyTo, overwrite: true);
            }
            if (formModel.FormMode == EditPlayableItemFormMode.Add)
            {
                _operationProgress.ReportProgress(opId, "copying source file...");
                // Copy source file from library to playlist folder
                var copyFrom = formModel.OriginalItem.GetSourceFilePath();    // From library folder
                var copyTo = formModel.UpdatedItem.GetSourceFilePath();       // To playlist folder
                if (!File.Exists(copyTo)){
                    File.Copy(copyFrom, copyTo, overwrite: true);
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

        /// <summary>
        /// Called when OK is clicked in the ReConvertAllPlayableItemsForm
        /// </summary>
        public async Task ReConvertAllItems(EditPlayableItemFormMode formMode, int relativeBrightness, LedMatrixOptionsConfig? options)
        {
            var playlist = GetPlaylistBeingEdited();
            if (playlist == null)
                return;
            var opId = await _operationProgress.StartOperation("Re-Converting all Playlist Items");
            int itemIndex = 0;
            foreach (var originalItem in playlist.GetPlaylistItems())
            {
                var item = originalItem.Clone();
                if (formMode == EditPlayableItemFormMode.ReConvertAllBrightness)
                {
                    item.RelativeBrightness = relativeBrightness;
                }
                else if (formMode == EditPlayableItemFormMode.ReConvertAllMatrix && options != null)
                {
                    item.MatrixOptions = options;
                }
                if (!originalItem.NeedsReConvert(item))
                {
                    continue;
                }
                _operationProgress.ReportProgress(opId, $"Re-Converting item {itemIndex + 1} of {playlist.GetPlaylistItems().Count}: {item.Name}");
                if (formMode == EditPlayableItemFormMode.Edit && options != null)
                {
                    item.MatrixOptions = options;
                }
                var folder = playlist.GetPlaylistAbsolutePath();
                var result = await _streamConverterService.ConvertToStream(
                    folder,
                    item.SourceFileName,
                    folder,
                    item.Name,
                    item.RelativeBrightness,
                    item.MatrixOptions);
                if (result.ExitCode != 0)
                {
                    // Re-convert failed - show an alert and do not save changes
                    _operationProgress.CompleteOperation(opId, false, result.Message + "\n" + result.Error);
                }
                item.CurrentBrightness = result.ActualBrightness;
                // Save updated metadata
                originalItem.UpdateFromClone(item);
                itemIndex++;
            }
            _operationProgress.CompleteOperation(opId, true, "Done");
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
                var path = playlist.GetPlaylistAbsolutePath();
                File.Delete(item.GetStreamFilePath());
                File.Delete(item.GetSourceFilePath());
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
        /// Called when the editing playlist is changed from the UI
        /// </summary>
        /// <param name="playlistName"></param>
        public void OnEditingPlaylistChanged(string? playlistName)
        {
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
    }
}