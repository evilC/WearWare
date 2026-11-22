using WearWare.Utils;

namespace WearWare.Architecture.QuickMedia
{
    public class QuickMediaStateConfig
    {
        public int ActiveSlotNumber { get; set; }

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
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                JsonUtils.ToJsonFile(temporaryPath, this);
                File.Move(temporaryPath, path, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static string GetConfigFilePath()
        {
            return Path.Combine(PathConfig.QuickMediaPath, _configFileName);
        }
    }
}
