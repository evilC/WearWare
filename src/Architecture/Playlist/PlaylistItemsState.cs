using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Architecture.Playlist
{
    // Keep the frequently updated current item index separate from the playlist definition.
    public class PlaylistItemsState
    {
        public int CurrentItem { get; set; } = -1;

        public static PlaylistItemsState Deserialize(string name)
        {
            var statePath = Path.Combine(PathConfig.PlaylistPath, name);
            Directory.CreateDirectory(statePath);
            var stateFile = Path.Combine(statePath, "playlist-state.json");
            if (File.Exists(stateFile))
            {
                try
                {
                    var state = JsonUtils.FromJsonFile<PlaylistItemsState>(stateFile);
                    if (state != null)
                    {
                        return state;
                    }
                }
                catch (Exception)
                {
                    // Ignore and return default
                }
            }
            return new PlaylistItemsState();
        }

        public void Serialize(string name)
        {
            var statePath = Path.Combine(PathConfig.PlaylistPath, name);
            Directory.CreateDirectory(statePath);
            var stateFile = Path.Combine(statePath, "playlist-state.json");
            JsonUtils.ToJsonFile(stateFile, this);
        }

        public PlaylistItemsState Clone()
        {
            return new PlaylistItemsState
            {
                CurrentItem = this.CurrentItem
            };
        }
    }
}