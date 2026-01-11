using WearWare.Common.Media;
using WearWare.Services.MediaController;
namespace WearWare.Services.Playlist
{

    // Contains a list of playable items in a playlist
    // Abstracts away the implementation of getting next item, adding and removing items, etc.
    // Currently uses OrderedDictionary to maintain order, allow lookup by name or index, and allow removal of items in the middle
    // Retreival of items should be done by nmame or get next. Because removal of items in the middle can change indices
    // This can happen, for example, when an item is deleted from the playlist whilst it is playing
    public class PlaylistItems
    {
        /// <summary>
        /// The name of the playlist
        /// </summary>
        public string Name { get; init; }
        // System.Collections.Specialized.OrderedDictionary is not available in .NET 8
        // Needs .NET 9 or higher
        // private readonly OrderedDictionary<string, PlayableItem> _items = [];
        private readonly List<PlayableItem> _items = [];

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
        /// Checks if the playlist is in exclusive mode
        /// </summary>
        /// <returns></returns>
        // public bool InExclusiveMode()
        // {
        //     return PlaylistConfig.ExclusiveItem != null;
        // }

        /// <summary>
        /// Gets the index of the exclusive item
        /// </summary>
        /// <returns>The index of the exclusive item, or null if not in exclusive mode</returns>
        // public int? GetExclusiveItem()
        // {
        //     return PlaylistConfig.ExclusiveItem;
        // }
        

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
            // if (PlaylistConfig.ExclusiveItem != null
            //  && insertIndex <= PlaylistConfig.ExclusiveItem)
            // {
            //     // Adjust exclusive item index if necessary
            //     PlaylistConfig.ExclusiveItem++;
            // }
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

            // Update exclusive item index if necessary
            // if (PlaylistConfig.ExclusiveItem != null)
            // {
            //     if (removedIndex == PlaylistConfig.ExclusiveItem)
            //     {
            //         // Removed item was the exclusive item, disable exclusive mode
            //         PlaylistConfig.ExclusiveItem = null;
            //     }
            //     else if (removedIndex < PlaylistConfig.ExclusiveItem)
            //     {
            //         // Exclusive item index was after removed item, decrement it
            //         PlaylistConfig.ExclusiveItem--;
            //     }
            // }
            return true;
        }
    }
}