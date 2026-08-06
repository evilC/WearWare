using RPiRgbLEDMatrix;
using WearWare.Config;
using WearWare.Services.Options;
using WearWare.Utils;

namespace WearWare.Services.MatrixConfig
{
    public class MatrixConfigService
    {
        private LedMatrixOptionsConfig _options;
        private readonly AppOptionsService _appOptionsService;
        private int _currentBrightness;
        private static readonly string ConfigFilePath = Path.Combine(PathConfig.ConfigPath, "matrixconfig.json");

        public event Action? OptionsChanged;
        public event Action<int>? BrightnessChanged;

        public MatrixConfigService(AppOptionsService appOptionsService)
        {
            _appOptionsService = appOptionsService;

            if (!File.Exists(ConfigFilePath))
            {
                _options = new LedMatrixOptionsConfig();
                JsonUtils.ToJsonFile(ConfigFilePath, _options);
            }
            else
            {
                _options = JsonUtils.FromJsonFile<LedMatrixOptionsConfig>(ConfigFilePath) ?? new LedMatrixOptionsConfig();
            }

            var cap = _options.Brightness ?? 100;
            var persistedCurrent = appOptionsService.GetCurrentBrightness();
            _currentBrightness = Math.Clamp(persistedCurrent ?? cap, 1, cap);
        }

        public void UpdateOptions(LedMatrixOptionsConfig newOptions)
        {
            var oldOptions = _options.Clone();
            _options = newOptions;
            JsonUtils.ToJsonFile(ConfigFilePath, _options);

            var oldCap = oldOptions.Brightness ?? 100;
            var newCap = _options.Brightness ?? 100;
            var oldWithoutBrightness = oldOptions.Clone();
            oldWithoutBrightness.Brightness = _options.Brightness;
            var structuralChanged = !oldWithoutBrightness.IsEqual(_options);

            // Max brightness changed: only force current down when cap is lowered.
            if (newCap < oldCap && _currentBrightness > newCap)
            {
                _currentBrightness = newCap;
                BrightnessChanged?.Invoke(_currentBrightness);

                // Keep persisted app options in sync when max brightness forces current down.
                if (_appOptionsService.GetCurrentBrightness() != _currentBrightness)
                {
                    _appOptionsService.SaveCurrentBrightness(_currentBrightness);
                }
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

        public int GetCurrentBrightness()
        {
            return _currentBrightness;
        }

        public void SetCurrentBrightness(int currentBrightness)
        {
            var cap = _options.Brightness ?? 100;
            var clamped = Math.Clamp(currentBrightness, 1, cap);
            _currentBrightness = clamped;
            BrightnessChanged?.Invoke(_currentBrightness);
        }

        internal RGBLedMatrixOptions GetRGBLedMatrixOptions()
        {
            return _options.ToRGBLedMatrixOptions();
        }
    }
}
