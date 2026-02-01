using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.MatrixConfig;
using WearWare.Utils;

namespace WearWare.Services.Playlist
{

    // Contains a list of playable items in a playlist
    // Abstracts away the implementation of getting next item, adding and removing items, etc.
    // This can happen, for example, when an item is deleted from the playlist whilst it is playing
    public class PlaylistItems
    {
        /// <summary>
        /// The name of the playlist
        /// </summary>
        public string Name { get; init; }
        private readonly List<PlayableItem> _items = [];

        private PlaylistItemsConfig _config { get; init; }
        ILogger<PlaylistItems> _logger;
        private static readonly string _logTag = "[PLAYLISTITEMS]";
        private static readonly string _configFileName = "playlistitems.json";

        public PlaylistItems(ILogger<PlaylistItems> logger, string name, PlaylistItemsConfig playlistConfig, List<PlayableItem> items)
        {
            _logger = logger;
            Name = name;
            _config = playlistConfig;
            if (_config.CurrentItem >= items.Count || _config.CurrentItem < -1)
            {
                _config.CurrentItem = -1;
            }
            _items = items;
            _logger.LogInformation("{tag} Playlist {playlist} initialized with {itemCount} items. Current item index: {currentItemIndex}", _logTag, Name, _items.Count, _config.CurrentItem);
            // if (_config.CurrentItem == -1)
            // {
            //     MoveNext();
            // }
        }

        /// <summary>
        /// Gets the current item in the playlist
        /// </summary>
        /// <returns></returns>
        public PlayableItem? GetCurrentItem()
        {
            if (_items.Count == 0 || _config.CurrentItem < 0 || _config.CurrentItem >= _items.Count) return null;
            return _items[_config.CurrentItem];
        }
        
        public PlayableItem? MoveNext()
        {
            var nextIndex = GetNextElegibleItemIndex();
            if (nextIndex == null)
            {
                // No next item found
                _config.CurrentItem = -1;
                _config.Serialize(Name);
                return null;
            }
            _config.CurrentItem = nextIndex.Value;
            _config.Serialize(Name);
            return GetCurrentItem();
        }

        /// <summary>
        /// Gets the index of the next enabled item in the playlist
        /// </summary>
        /// <returns>The index of the next enabled item, or null if none found</returns>
        public int? GetNextElegibleItemIndex()
        {
            var currentIndex = _config.CurrentItem;
            if (_items.Count == 0)
            {
                return null;
            }

            int checkedItems = 0;
            while (true)
            {
                checkedItems++;
                currentIndex = (currentIndex + 1) % _items.Count;
                
                if (_items[currentIndex].Enabled)
                {
                    // Return the next enabled item
                    break;
                }
                // If we've looped all the way around and found no valid items, return null
                if (checkedItems >= _items.Count)
                {
                    return null;
                }
            }
            return currentIndex;
        }


        public bool JumpToPlaylistIndex(int index)
        {
            if (index < 0 || index >= _items.Count) return false;
            if (!_items[index].Enabled) return false;
            _config.CurrentItem = index;
            _config.Serialize(Name);
            return true;
        }

        /// <summary>
        /// Gets all items in the playlist as a list
        /// (Used for serialization)
        /// </summary>
        /// <returns></returns>
        public List<PlayableItem> GetPlaylistItems()
        {
            return _items;
        }

        /// <summary>
        /// Adds an item to the playlist at the specified index
        /// </summary>
         /// <param name="insertIndex"></param> The index to insert the item at
         /// <param name="item"></param> The item to add
         /// <returns>True if the item was added successfully, otherwise false</returns>
        public bool AddItem(int insertIndex, PlayableItem item)
        {
            if (insertIndex > _items.Count)
            {
                // Insert at end
                _items.Add(item);
            }
            else
            {
                // Insert at specified index
                _items.Insert(insertIndex, item);
            }
            // If current index is -1 (no current item), try to move to this item
            if (_config.CurrentItem == -1)
            {
                MoveNext();
            }
            else if (insertIndex <= _config.CurrentItem)
            {
                // Adjust current index if necessary
                _config.CurrentItem++;
                _config.Serialize(Name);
            }
            Serialize();
            return true;
        }

        /// <summary>
        /// Removes an item from the playlist by index.
        /// </summary>
        public bool RemoveItem(int removedIndex)
        {
            if (removedIndex < 0 || removedIndex >= _items.Count) return false;
            var oldCurrentIndex = _config.CurrentItem;
            _items.RemoveAt(removedIndex);

            // Adjust current index if necessary
            // If we are removing the current item, move to the next item
            if (removedIndex == oldCurrentIndex)
            {
                if (_items.Count == 0)
                {
                    // Last item removed, reset current index
                    _config.CurrentItem = -1;
                    _config.Serialize(Name);
                }
                else
                {
                    // Try to move to next item
                    _config.CurrentItem--; // Step back one so that MoveNext advances to the next item correctly
                    MoveNext();
                }
            }
            // Otherwise, if we removed an item before the current index, adjust the current index
            else if (removedIndex < oldCurrentIndex)
            {
                // Adjust current index if necessary
                _config.CurrentItem--;
                _config.Serialize(Name);
            }
            Serialize();
            return true;
        }

        public static PlaylistItems? Deserialize(ILogger<PlaylistItems> logger, string playlistName, MatrixConfigService matrixConfigService)
        {
            var path = Path.Combine(PathConfig.PlaylistPath, playlistName, _configFileName);
            if (!File.Exists(path))
            {
                return null;
            }
            var config = PlaylistItemsConfig.Deserialize(playlistName);
            var items = JsonUtils.FromJsonFile<List<PlayableItem>>(path);
            if (items == null)
            {
                return null;
            }
            foreach (var item in items){
                if (!File.Exists(item.GetStreamFilePath()))
                {
                    items.Remove(item);
                    logger.LogWarning("{tag} Media file for item {item} does not exist in playlist {playlist}, removing item from playlist.", _logTag, item.Name, playlistName);
                }
                // Older JSON may not include MatrixOptions; ensure it's initialized so code relying on it won't see null.
                if (item.MatrixOptions == null)
                    item.MatrixOptions = matrixConfigService.CloneOptions();

            }
            return new PlaylistItems(logger, playlistName, config, items);
        }

        public void Serialize()
        {
            var outPath = Path.Combine(GetPlaylistAbsolutePath(), _configFileName);
            JsonUtils.ToJsonFile(outPath, _items);
        }

        /// <summary>
        /// Gets the absolute path to the playlist folder
        /// </summary>
        /// <param name="playlist"></param>
        /// <returns>The absolute path to the playlist folder (eg /root/WearWare/playlists/playlistName)</returns>
        public string GetPlaylistAbsolutePath()
        {
            return Path.Combine(PathConfig.PlaylistPath, Name);
        }

        /// <summary>
        /// Gets the relative path to the playlist folder from the WearWare root
        /// </summary>
        /// <returns>The relative path to the playlist folder (eg playlists/playlistName)</returns
        public string GetPlaylistRelativePath()
        {
            return Path.Combine(PathConfig.PlaylistFolder, Name);
        }

        /// <summary>
        /// Creates a deep clone of this PlaylistItems with a new name
        /// </summary>
        /// <param name="newName"></param> The name for the new playlist
        public PlaylistItems Clone(string newName)
        {
            var items = new List<PlayableItem>();
            foreach (var originalItem in _items)
            {
                var item = originalItem.Clone();
                item.ParentFolder = Path.Combine(PathConfig.PlaylistFolder, newName);
                items.Add(item);
            }
            return new PlaylistItems(_logger, newName, _config.Clone(), items);
        }
    }
}