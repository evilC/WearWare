using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Services.Playlist
{
    // Configuration settings for a playlist
    // Stored separately from the playlist items themselves because this will be serialized A LOT
    // This saves us from having to read and write the entire playlist file just to update the current item index
    // Also this insulates us from the main playlist file being corrupted if we shut down while writing the config
    public class PlaylistItemsConfig
    {
        public int CurrentItem { get; set; } = -1;

        public static PlaylistItemsConfig Deserialize(string name)
        {
            var configPath = Path.Combine(PathConfig.PlaylistPath, name);
            Directory.CreateDirectory(configPath);
            var configFile = Path.Combine(configPath, "playlistconfig.json");
            if (File.Exists(configFile))
            {
                try
                {
                    var config = JsonUtils.FromJsonFile<PlaylistItemsConfig>(configFile);
                    if (config != null)
                    {
                        return config;
                    }
                }
                catch (Exception)
                {
                    // Ignore and return default
                }
            }
            return new PlaylistItemsConfig();
        }

        public void Serialize(string name)
        {
            var configPath = Path.Combine(PathConfig.PlaylistPath, name);
            Directory.CreateDirectory(configPath);
            var configFile = Path.Combine(configPath, "playlistconfig.json");
            JsonUtils.ToJsonFile(configFile, this);
        }

        public PlaylistItemsConfig Clone()
        {
            return new PlaylistItemsConfig
            {
                CurrentItem = this.CurrentItem
            };
        }
    }
}