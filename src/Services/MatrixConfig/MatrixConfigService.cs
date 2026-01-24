using RPiRgbLEDMatrix;
using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Services.MatrixConfig
{
    public class MatrixConfigService
    {
        private LedMatrixOptionsConfig _options;
        private LedMatrixOptionsVisibility _visibility;
        private static readonly string ConfigFilePath = Path.Combine(PathConfig.ConfigPath, "matrixconfig.json");
        private static readonly string VisibilityFilePath = Path.Combine(PathConfig.ConfigPath, "matrixconfig-visibility.json");

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
            // Load visibility (UI-only flags) from a separate file
            if (!File.Exists(VisibilityFilePath))
            {
                _visibility = new LedMatrixOptionsVisibility();
                JsonUtils.ToJsonFile(VisibilityFilePath, _visibility);
            }
            else
            {
                _visibility = JsonUtils.FromJsonFile<LedMatrixOptionsVisibility>(VisibilityFilePath) ?? new LedMatrixOptionsVisibility();
            }
        }

        public void UpdateOptions(LedMatrixOptionsConfig newOptions)
        {
            _options = newOptions;
            JsonUtils.ToJsonFile(ConfigFilePath, _options);
            JsonUtils.ToJsonFile(VisibilityFilePath, _visibility);
            OptionsChanged?.Invoke();
        }

        public LedMatrixOptionsVisibility Visibility => _visibility;

        public void UpdateVisibility(LedMatrixOptionsVisibility v)
        {
            _visibility = v ?? new LedMatrixOptionsVisibility();
            JsonUtils.ToJsonFile(VisibilityFilePath, _visibility);
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
