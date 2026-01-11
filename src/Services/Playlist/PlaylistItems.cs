using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.Playlist;
using WearWare.Utils;
record PlaylistDto(PlaylistConfig PlaylistConfig, List<PlayableItem> PlaylistItems);

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

        // ToDo: PlaylistConfig is currently not used. It should be used to store current item index, so that playback resumes on the same item after app restart
        // There is a concern though that storing current item could cause problems if shutdown occurs while writing to the config file
        public PlaylistConfig PlaylistConfig { get; init; }
        private int _currentIndex;


        public PlaylistItems(string name, PlaylistConfig playlistConfig, List<PlayableItem> items)
        {
            Name = name;
            PlaylistConfig = playlistConfig;
            _items = items;
            _currentIndex = -1; // Set to -1 so that first call to MoveNext() sets it to first enabled item
            MoveNext();
        }

        /// <summary>
        /// Gets the current item in the playlist
        /// </summary>
        /// <returns></returns>
        public PlayableItem? GetCurrentItem()
        {
            if (_items.Count == 0 || _currentIndex < 0 || _currentIndex >= _items.Count) return null;
            return _items[_currentIndex];
        }
        
        public PlayableItem? MoveNext()
        {
            var nextIndex = GetNextElegibleItemIndex();
            if (nextIndex == null)
            {
                // No next item found
                _currentIndex = -1;
                return null;
            }
            _currentIndex = nextIndex.Value;
            return GetCurrentItem();
        }

        /// <summary>
        /// Gets the index of the next enabled item in the playlist
        /// </summary>
        /// <returns>The index of the next enabled item, or null if none found</returns>
        public int? GetNextElegibleItemIndex()
        {
            var currentIndex = _currentIndex;
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
            _currentIndex = index;
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
            if (_currentIndex == -1)
            {
                MoveNext();
            }
            return true;
        }

        /// <summary>
        /// Removes an item from the playlist by index.
        /// </summary>
        public bool RemoveItem(int removedIndex)
        {
            if (removedIndex < 0 || removedIndex >= _items.Count) return false;
            var oldCurrentIndex = _currentIndex;
            _items.RemoveAt(removedIndex);

            // Adjust current index if necessary
            // If we are removing the current item, move to the next item
            if (removedIndex == oldCurrentIndex)
            {
                if (_items.Count == 0)
                {
                    // Last item removed, reset current index
                    _currentIndex = -1;
                }
                else
                {
                    // Try to move to next item
                    _currentIndex--; // Step back one so that MoveNext advances to the next item correctly
                    if (MoveNext() == null)
                    {
                        // No valid next item, reset current index
                        _currentIndex = -1;
                    }
                }
            }
            // Otherwise, if we removed an item before the current index, adjust the current index
            else if (removedIndex < oldCurrentIndex)
            {
                // Adjust current index if necessary
                _currentIndex--;
            }

            return true;
        }

        public static PlaylistItems? Deserialize(string playlistName)
        {
            var path = Path.Combine(PathConfig.PlaylistPath, playlistName, "playlist.json");
            if (!File.Exists(path))
            {
                return null;
            }
            // return JsonUtils.FromJsonFile<PlaylistItems>(path);
            var json = File.ReadAllText(path);  
            var dto = JsonUtils.FromJson<PlaylistDto>(json);
            // ToDo: need error handling here
            if (dto == null) return null;
            if (dto.PlaylistConfig == null) return null;
            foreach (var item in dto.PlaylistItems){
                // CheckPlayableMediaItemExists(GetPlaylistPath(new PlaylistItems(playlistName, dto.PlaylistConfig, [])), item);
                CheckPlayableMediaItemExists(Path.Combine(PathConfig.PlaylistPath, playlistName), item);
            }
            return new PlaylistItems(playlistName, dto.PlaylistConfig, dto.PlaylistItems);
        }

        public void Serialize()
        {
            var outPath = Path.Combine(PathConfig.PlaylistPath, Name, "playlist.json");
            var dto = new PlaylistDto(PlaylistConfig, _items);
            JsonUtils.ToJsonFile(outPath, dto);
        }

        /// <summary>
        /// Checks that the media file for the given playable item exists in the given folder
        /// </summary>
        /// <param name="folder"></param> The folder to check
        /// <param name="item"></param> The playable item to check
        public static void CheckPlayableMediaItemExists(string folder, PlayableItem item)
        {
            var filename = $"{folder}/{$"{item.Name}.stream"}";
            if (!File.Exists(filename))
            {
                throw new Exception($"Media file {filename} for playlist item {item.Name} does not exist in playlist folder {folder}");
            }
        }

        /// <summary>
        /// Gets the path to the playlist folder
        /// </summary>
        /// <param name="playlist"></param>
        /// <returns></returns>
        public string GetPlaylistPath()
        {
            return Path.Combine(PathConfig.PlaylistPath, Name);
        }


    }
}