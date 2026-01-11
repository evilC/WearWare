using WearWare.Utils;

namespace WearWare.Config
{
    public class AppConfig
    {
        public string? ActivePlaylist { get; set; } = null;
        public string? EditingPlaylist { get; set; } = null;

        public void Serialize()
        {
            var configDir = PathConfig.ConfigPath;
            Directory.CreateDirectory(configDir);
            var configFile = Path.Combine(configDir, "appconfig.json");
            JsonUtils.ToJsonFile(configFile, this);
        }
    }
}