using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Services.Options
{
    public class AppOptionsService
    {
        private static readonly string AppOptionsFilePath = Path.Combine(PathConfig.ConfigPath, "appoptions.json");
        private AppOptions _options;

        public AppOptionsService()
        {
            if (!File.Exists(AppOptionsFilePath))
            {
                _options = new AppOptions();
                JsonUtils.ToJsonFile(AppOptionsFilePath, _options);
            }
            else
            {
                _options = JsonUtils.FromJsonFile<AppOptions>(AppOptionsFilePath) ?? new AppOptions();
            }
        }

        public AppOptions CloneOptions()
        {
            return _options.Clone();
        }

        public int? GetCurrentBrightness()
        {
            return _options.CurrentBrightness;
        }

        public void SaveCurrentBrightness(int currentBrightness)
        {
            _options.CurrentBrightness = currentBrightness;
            JsonUtils.ToJsonFile(AppOptionsFilePath, _options);
        }
    }
}
