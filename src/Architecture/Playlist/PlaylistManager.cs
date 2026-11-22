using System.Text.Json;
using WearWare.Common.Media;
using WearWare.Services.OperationProgress;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Utils;

namespace WearWare.Architecture.Playlist
{
    public class PlaylistManager : IManager
    {
        private readonly ILogger<PlaylistManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _logTag = "[PLAYLISTMANAGER]";
        private static readonly string ConfigFilePath = Path.Combine(PathConfig.ConfigPath, "playlistsconfig.json");
        private static readonly string StateFilePath = Path.Combine(PathConfig.ConfigPath, "playlist-state.json");
        
        private Dictionary<string, PlaylistItems> _playlists = [];
        private string? _currentPlaylistName;
        private string? _editingPlaylistName;
        private PlaylistsConfig _config;
        private volatile bool _playlistRunning;

        public event Action? StateChanged;

        public bool PlaylistRunning => _playlistRunning;
        public string? StateLoadError { get; private set; }
        public string? CurrentPlaylistName => _currentPlaylistName;
        public PlaylistItems? CurrentPlaylist => _currentPlaylistName != null && _playlists.ContainsKey(_currentPlaylistName) 
            ? _playlists[_currentPlaylistName] 
            : null;

        public string? EditingPlaylistName => _editingPlaylistName;
        public PlaylistItems? EditingPlaylist => _editingPlaylistName != null && _playlists.ContainsKey(_editingPlaylistName) 
            ? _playlists[_editingPlaylistName] 
            : null;

        public PlaylistManager(ILogger<PlaylistManager> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _config = LoadConfig();
            _playlistRunning = LoadRunningState();
            _logger.LogInformation("{tag} initialized.", _logTag);
        }

        private bool LoadRunningState()
        {
            if (!File.Exists(StateFilePath))
                return true;

            try
            {
                var state = JsonUtils.FromJsonFile<PlaylistState>(StateFilePath)
                    ?? throw new JsonException("Playlist state contains null.");
                return state.Running ?? throw new JsonException("Playlist running state is missing.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError(ex, "{tag} Error loading playlist state from {path}. Using defaults.", _logTag, StateFilePath);
                StateLoadError = "Could not load Playlist state. See the log for details.";
                return true;
            }
        }

        internal bool SetPlaylistRunning(bool running)
        {
            _playlistRunning = running;
            var saved = true;
            try
            {
                JsonUtils.ToJsonFileAtomic(StateFilePath, new PlaylistState { Running = running }, _logger);
                StateLoadError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "{tag} Could not save Playlist state.", _logTag);
                saved = false;
            }
            StateChanged?.Invoke();
            return saved;
        }

        private sealed class PlaylistState
        {
            public bool? Running { get; set; }
        }

        private PlaylistsConfig LoadConfig()
        {
            if (!File.Exists(ConfigFilePath))
            {
                return new PlaylistsConfig();
            }

            try
            {
                return JsonUtils.FromJsonFile<PlaylistsConfig>(ConfigFilePath)
                    ?? throw new JsonException("Playlist configuration file contains null.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
            {
                _logger.LogError(ex, "{tag} Error loading playlist configuration from {path}. Using defaults.", _logTag, ConfigFilePath);
                return new PlaylistsConfig();
            }
        }

        private void SaveConfig()
        {
            Directory.CreateDirectory(PathConfig.ConfigPath);
            JsonUtils.ToJsonFile(ConfigFilePath, _config);
        }

        /// <summary>
        /// Loads all playlists from disk
        /// </summary>
        public void LoadAllPlaylists()
        {
            try
            {
                _playlists.Clear();
                _logger.LogInformation("{tag} Loading playlists from {path}...", _logTag, PathConfig.PlaylistPath);
                
                foreach (var dir in Directory.GetDirectories(PathConfig.PlaylistPath))
                {
                    var playlistName = Path.GetFileName(dir);
                    LoadPlaylist(playlistName);
                }

                _logger.LogInformation("{tag} Loaded {count} playlists.", _logTag, _playlists.Count);

                // Restore editing/active playlists if they still exist
                if (!string.IsNullOrEmpty(_config.EditingPlaylist) && _playlists.ContainsKey(_config.EditingPlaylist))
                {
                    _editingPlaylistName = _config.EditingPlaylist;
                }
                else
                {
                    _editingPlaylistName = null;
                }

                if (!string.IsNullOrEmpty(_config.ActivePlaylist) && _playlists.ContainsKey(_config.ActivePlaylist))
                {
                    _currentPlaylistName = _config.ActivePlaylist;
                }
                else
                {
                    _currentPlaylistName = null;
                }

                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error loading playlists.", _logTag);
            }
        }

        /// <summary>
        /// Loads a single playlist by name from disk
        /// </summary>
        public bool LoadPlaylist(string playlistName)
        {
            try
            {
                var playlistPath = Path.Combine(PathConfig.PlaylistPath, playlistName);
                if (!Directory.Exists(playlistPath))
                {
                    _logger.LogWarning("{tag} Playlist '{playlist}' not found.", _logTag, playlistName);
                    return false;
                }

                var playlistItemsLogger = _loggerFactory.CreateLogger<PlaylistItems>();
                var playlist = PlaylistItems.Deserialize(
                    playlistItemsLogger,
                    playlistName,
                    null!);

                if (playlist == null)
                {
                    _logger.LogWarning("{tag} Failed to deserialize playlist '{playlist}'.", _logTag, playlistName);
                    return false;
                }

                _playlists[playlistName] = playlist;
                _logger.LogInformation("{tag} Loaded playlist '{playlist}' with {itemCount} items.",
                    _logTag, playlistName, playlist.GetPlaylistItems().Count);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error loading playlist '{playlist}'.", _logTag, playlistName);
                return false;
            }
        }

        /// <summary>
        /// Sets the active playlist (currently playing)
        /// </summary>
        public bool SetActivePlaylist(string playlistName)
        {
            if (!_playlists.ContainsKey(playlistName))
            {
                _logger.LogWarning("{tag} Cannot set active playlist: '{playlist}' not found.", _logTag, playlistName);
                return false;
            }

            _currentPlaylistName = playlistName;
            _config.ActivePlaylist = playlistName;
            SaveConfig();
            _logger.LogInformation("{tag} Active playlist set to '{playlist}'.", _logTag, playlistName);
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Sets the editing playlist (UI editing context)
        /// </summary>
        public bool SetEditingPlaylist(string? playlistName)
        {
            if (playlistName != null && !_playlists.ContainsKey(playlistName))
            {
                _logger.LogWarning("{tag} Cannot set editing playlist: '{playlist}' not found.", _logTag, playlistName);
                return false;
            }

            _editingPlaylistName = playlistName;
            _config.EditingPlaylist = playlistName;
            SaveConfig();
            _logger.LogInformation("{tag} Editing playlist set to '{playlist}'.", _logTag, playlistName ?? "(none)");
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Gets all playlist names
        /// </summary>
        public List<string> GetPlaylistNames() => _playlists.Keys.ToList();

        /// <summary>
        /// Gets a specific playlist by name
        /// </summary>
        public PlaylistItems? GetPlaylist(string playlistName) =>
            _playlists.TryGetValue(playlistName, out var playlist) ? playlist : null;

        /// <summary>
        /// Gets the next item in the current playlist
        /// </summary>
        public PlayableItem? GetNextItem()
        {
            if (CurrentPlaylist == null) return null;
            return CurrentPlaylist.MoveNext();
        }

        /// <summary>
        /// Gets the current item without advancing
        /// </summary>
        public PlayableItem? GetCurrentItem()
        {
            if (CurrentPlaylist == null) return null;
            return CurrentPlaylist.GetCurrentItem();
        }

        /// <summary>
        /// Jumps to a specific item in the current playlist by index
        /// </summary>
        public bool JumpToItem(int index)
        {
            if (CurrentPlaylist == null) return false;
            return CurrentPlaylist.JumpToPlaylistIndex(index);
        }

        /// <summary>
        /// Gets all items in the current playlist
        /// </summary>
        public List<PlayableItem> GetAllItems()
        {
            if (CurrentPlaylist == null) return [];
            return CurrentPlaylist.GetPlaylistItems();
        }

        /// <summary>
        /// Deletes a playlist and clears state if it was active/editing
        /// </summary>
        public bool DeletePlaylist(string playlistName)
        {
            if (!_playlists.ContainsKey(playlistName))
            {
                _logger.LogWarning("{tag} Cannot delete playlist: '{playlist}' not found.", _logTag, playlistName);
                return false;
            }

            try
            {
                // If the playlist is active, clear it and update config
                if (_currentPlaylistName == playlistName)
                {
                    _currentPlaylistName = null;
                    _config.ActivePlaylist = null;
                }

                // If the playlist is being edited, clear it and update config
                if (_editingPlaylistName == playlistName)
                {
                    _editingPlaylistName = null;
                    _config.EditingPlaylist = null;
                }

                SaveConfig();
                _playlists.Remove(playlistName);

                // Delete the playlist folder from disk
                var path = Path.Combine(PathConfig.PlaylistPath, playlistName);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                _logger.LogInformation("{tag} Deleted playlist '{playlist}'.", _logTag, playlistName);
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error deleting playlist '{playlist}'.", _logTag, playlistName);
                return false;
            }
        }

        /// <summary>
        /// Reloads all playlists from disk (for when external operations add/modify playlists)
        /// </summary>
        public void ReloadPlaylists()
        {
            LoadAllPlaylists();
        }

        /// <summary>
        /// Adds a new empty playlist
        /// </summary>
        public bool AddPlaylist(string playlistName)
        {
            if (string.IsNullOrEmpty(playlistName) || _playlists.ContainsKey(playlistName))
            {
                _logger.LogWarning("{tag} Cannot add playlist: '{playlist}' already exists or name is empty.", _logTag, playlistName);
                return false;
            }

            try
            {
                var playlistItemsLogger = _loggerFactory.CreateLogger<PlaylistItems>();
                var playlist = new PlaylistItems(playlistItemsLogger, playlistName, new PlaylistItemsConfig(), []);
                _playlists[playlistName] = playlist;

                // Create playlist folder
                var path = Path.Combine(PathConfig.PlaylistPath, playlistName);
                Directory.CreateDirectory(path);
                playlist.Serialize();

                _logger.LogInformation("{tag} Added playlist '{playlist}'.", _logTag, playlistName);
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error adding playlist '{playlist}'.", _logTag, playlistName);
                return false;
            }
        }

        /// <summary>
        /// Copies an existing playlist to a new playlist
        /// </summary>
        public async Task<bool> CopyPlaylistAsync(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName) || 
                !_playlists.ContainsKey(oldName) || _playlists.ContainsKey(newName))
            {
                _logger.LogWarning("{tag} Cannot copy playlist: source not found or destination already exists.", _logTag);
                return false;
            }

            try
            {
                var oldPlaylist = _playlists[oldName];
                var newPlaylist = oldPlaylist.Clone(newName);

                // Copy playlist folder
                var oldPath = Path.Combine(PathConfig.PlaylistPath, oldName);
                var newPath = Path.Combine(PathConfig.PlaylistPath, newName);
                Directory.CreateDirectory(newPath);
                foreach (var file in Directory.GetFiles(oldPath))
                {
                    var destFile = Path.Combine(newPath, Path.GetFileName(file));
                    await FileUtils.CopyFileAsync(file, destFile);
                }

                _playlists[newName] = newPlaylist;
                newPlaylist.Serialize();

                _logger.LogInformation("{tag} Copied playlist from '{oldName}' to '{newName}'.", _logTag, oldName, newName);
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                if (_playlists.ContainsKey(newName))
                {
                    _playlists.Remove(newName);
                }
                _logger.LogError(ex, "{tag} Error copying playlist from '{oldName}' to '{newName}'.", _logTag, oldName, newName);
                return false;
            }
        }

        /// <summary>
        /// Adds or edits an item in the specified playlist
        /// </summary>
        public async Task<bool> UpsertPlaylistItemAsync(string playlistName, EditPlayableItemFormModel formModel, IOperationProgressService operationProgress)
        {
            if (!_playlists.ContainsKey(playlistName))
            {
                _logger.LogWarning("{tag} Cannot upsert item: playlist '{playlist}' not found.", _logTag, playlistName);
                return false;
            }

            try
            {
                var opId = await operationProgress.StartOperation("Updating Playlist Item");
                var playlist = _playlists[playlistName];

                if (formModel.FormMode == EditPlayableItemFormMode.Add)
                {
                    formModel.UpdatedItem.ParentFolder = playlist.GetPlaylistRelativePath();
                }

                if (formModel.FormMode == EditPlayableItemFormMode.Add)
                {
                    operationProgress.ReportProgress(opId, "Copying fseq file...");
                    var copyFrom = formModel.OriginalItem.GetFseqFilePath();
                    var copyTo = formModel.UpdatedItem.GetFseqFilePath();
                    await FileUtils.CopyFileAsync(copyFrom, copyTo).ConfigureAwait(false);
                }

                if (formModel.FormMode == EditPlayableItemFormMode.Add)
                {
                    operationProgress.ReportProgress(opId, "Copying source file...");
                    var copyFrom = formModel.OriginalItem.GetSourceFilePath();
                    var copyTo = formModel.UpdatedItem.GetSourceFilePath();
                    if (!File.Exists(copyTo))
                    {
                        await FileUtils.CopyFileAsync(copyFrom, copyTo).ConfigureAwait(false);
                    }
                }

                operationProgress.ReportProgress(opId, "Updating playlist...");
                if (formModel.FormMode == EditPlayableItemFormMode.Add)
                {
                    playlist.AddItem(formModel.ItemIndex, formModel.UpdatedItem);
                }
                else
                {
                    formModel.OriginalItem.UpdateFromClone(formModel.UpdatedItem);
                    if (!formModel.OriginalItem.Enabled)
                    {
                        if (playlist.GetCurrentItem() == formModel.OriginalItem)
                        {
                            playlist.MoveNext();
                        }
                    }
                }

                playlist.Serialize();
                operationProgress.CompleteOperation(opId, true, "Done");

                StateChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error upserting playlist item in '{playlist}'.", _logTag, playlistName);
                return false;
            }
        }

        /// <summary>
        /// Removes an item from the specified playlist
        /// </summary>
        public bool RemovePlaylistItem(string playlistName, int itemIndex)
        {
            if (!_playlists.ContainsKey(playlistName))
            {
                _logger.LogWarning("{tag} Cannot remove item: playlist '{playlist}' not found.", _logTag, playlistName);
                return false;
            }

            try
            {
                var playlist = _playlists[playlistName];
                var items = playlist.GetPlaylistItems();

                if (itemIndex < 0 || itemIndex >= items.Count)
                {
                    _logger.LogWarning("{tag} Cannot remove item: index {index} out of range.", _logTag, itemIndex);
                    return false;
                }

                playlist.RemoveItem(itemIndex);
                playlist.Serialize();

                _logger.LogInformation("{tag} Removed item {index} from playlist '{playlist}'.", _logTag, itemIndex, playlistName);
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error removing item from playlist '{playlist}'.", _logTag, playlistName);
                return false;
            }
        }

        /// <summary>
        /// Jumps to an item in a specific playlist (supports non-active playlists)
        /// </summary>
        public bool JumpToItemInPlaylist(string playlistName, int index)
        {
            if (!_playlists.ContainsKey(playlistName))
            {
                _logger.LogWarning("{tag} Cannot jump to item: playlist '{playlist}' not found.", _logTag, playlistName);
                return false;
            }

            var playlist = _playlists[playlistName];
            return playlist.JumpToPlaylistIndex(index);
        }
    }
}
