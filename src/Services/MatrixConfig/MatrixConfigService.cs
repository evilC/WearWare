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
        public event Action<int>? BrightnessChanged;

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
            var oldOptions = _options.Clone();
            _options = newOptions;
            JsonUtils.ToJsonFile(ConfigFilePath, _options);

            var brightnessChanged = oldOptions.Brightness != _options.Brightness;
            var oldWithoutBrightness = oldOptions.Clone();
            oldWithoutBrightness.Brightness = _options.Brightness;
            var structuralChanged = !oldWithoutBrightness.IsEqual(_options);

            if (brightnessChanged)
            {
                BrightnessChanged?.Invoke(_options.Brightness ?? 100);
            }

            if (structuralChanged)
            {
                OptionsChanged?.Invoke();
            }
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
