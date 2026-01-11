using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Services.Playlist
{
    public class PlaylistsConfig
    {
        public string? ActivePlaylist { get; set; } = null;
        public string? EditingPlaylist { get; set; } = null;
        private static readonly string _configFile = Path.Combine(PathConfig.ConfigPath, "playlistsconfig.json");

        public void Serialize()
        {
            var configDir = PathConfig.ConfigPath;
            Directory.CreateDirectory(configDir);
            var configFile = Path.Combine(configDir, _configFile);
            JsonUtils.ToJsonFile(configFile, this);
        }

        public static PlaylistsConfig? Deserialize()
        {
            try
            {
                if (File.Exists(_configFile))
                {
                    return JsonUtils.FromJsonFile<PlaylistsConfig>(_configFile);
                }
            }
            catch (Exception)
            {
                return null;
            }
            return null;
        }
    }
}