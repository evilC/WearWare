using WearWare.Utils;

namespace WearWare.Services.QuickMedia
{
    public class QuickMediaState
    {
        public int ActiveSlotNumber { get; set; }

        private static readonly string _stateFileName = "quickmedia-state.json";

        public static QuickMediaState? Deserialize()
        {
            var path = GetStateFilePath();
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonUtils.FromJsonFile<QuickMediaState>(path);
        }

        public void Serialize()
        {
            var path = GetStateFilePath();
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

        private static string GetStateFilePath()
        {
            return Path.Combine(PathConfig.QuickMediaPath, _stateFileName);
        }
    }
}
