using WearWare.Utils;

namespace WearWare.Architecture.QuickMedia
{
    public class QuickMediaStateConfig
    {
        public int ActiveButtonNumber { get; set; } = 0;

        private static readonly string _configFileName = "quickmedia-state.json";

        public static QuickMediaStateConfig? Deserialize()
        {
            var path = GetConfigFilePath();
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonUtils.FromJsonFile<QuickMediaStateConfig>(path);
        }

        public void Serialize()
        {
            var path = GetConfigFilePath();
            Directory.CreateDirectory(PathConfig.QuickMediaPath);
            JsonUtils.ToJsonFile(path, this);
        }

        private static string GetConfigFilePath()
        {
            return Path.Combine(PathConfig.QuickMediaPath, _configFileName);
        }
    }
}
