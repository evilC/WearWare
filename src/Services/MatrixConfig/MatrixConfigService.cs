using RPiRgbLEDMatrix;
using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Services.MatrixConfig
{
    public class MatrixConfigService
    {
        private LedMatrixOptionsConfig _options;
        private static readonly string ConfigFilePath = Path.Combine(PathConfig.ConfigPath, "matrixconfig.json");

        public event Action? OptionsChanged;

        public MatrixConfigService()
        {
            if (!File.Exists(ConfigFilePath))
            {
                _options = new LedMatrixOptionsConfig();
                JsonUtils.ToJsonFile(ConfigFilePath, _options);
            }
            else
            {
                _options = JsonUtils.FromJsonFile<LedMatrixOptionsConfig>(ConfigFilePath) ?? new LedMatrixOptionsConfig();
            }
        }

        public void UpdateOptions(LedMatrixOptionsConfig newOptions)
        {
            _options = newOptions;
            JsonUtils.ToJsonFile(ConfigFilePath, _options);
            OptionsChanged?.Invoke();
        }

        public LedMatrixOptionsConfig CloneOptions()
        {
            return _options.Clone();
        }

        internal RGBLedMatrixOptions GetRGBLedMatrixOptions()
        {
            return _options.ToRGBLedMatrixOptions();
        }
    }
}
