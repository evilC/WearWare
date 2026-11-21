using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Architecture.Button
{
    public sealed class ButtonFunctionMappingService
    {
        private readonly ILogger<ButtonFunctionMappingService> _logger;
        private static readonly string ConfigFilePath = Path.Combine(PathConfig.ConfigPath, "buttonfunctionmapping.json");
        private ButtonFunctionMappingConfig _config;

        public ButtonFunctionMappingConfig Config => _config;

        public ButtonFunctionMappingService(ILogger<ButtonFunctionMappingService> logger)
        {
            _logger = logger;

            if (!File.Exists(ConfigFilePath))
            {
                _config = new ButtonFunctionMappingConfig();
                try
                {
                    JsonUtils.ToJsonFile(ConfigFilePath, _config);
                    _logger.LogInformation("Created default button function mapping configuration at {filename}.", ConfigFilePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(ex, "Could not create button function mapping configuration at {filename}.", ConfigFilePath);
                }

                return;
            }

            try
            {
                _config = JsonUtils.FromJsonFile<ButtonFunctionMappingConfig>(ConfigFilePath)
                    ?? throw new JsonException("Button function mapping configuration file contains null.");
                ValidateMappings(_config);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                _logger.LogError(ex, "Invalid button function mapping configuration in {filename}.", ConfigFilePath);
                _config = new ButtonFunctionMappingConfig();
            }
        }

        /// <summary>
        /// Gets the function assignment for a button pin.
        /// </summary>
        /// <param name="pinName">The human-friendly pin name (e.g., "GPIO16").</param>
        /// <returns>The assigned function ("None", "QuickMedia", etc.), or "None" if not found.</returns>
        public string GetFunctionForPin(string pinName)
        {
            if (_config.Mappings == null)
            {
                return "None";
            }

            var mapping = _config.Mappings.FirstOrDefault(m => m.PinName == pinName);
            return mapping?.Function ?? "None";
        }

        /// <summary>
        /// Sets the function assignment for a button pin.
        /// </summary>
        public async Task SetFunctionForPinAsync(string pinName, string function)
        {
            if (_config.Mappings == null)
            {
                _config.Mappings = [];
            }

            var mapping = _config.Mappings.FirstOrDefault(m => m.PinName == pinName);
            if (mapping == null)
            {
                mapping = new ButtonFunctionMapping { PinName = pinName };
                _config.Mappings = _config.Mappings.Append(mapping).ToArray();
            }

            mapping.Function = function;

            try
            {
                await Task.Run(() => JsonUtils.ToJsonFile(ConfigFilePath, _config));
                _logger.LogInformation("Updated button function mapping: {pinName} -> {function}", pinName, function);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Failed to save button function mapping for pin {pinName}.", pinName);
                throw;
            }
        }

        private static void ValidateMappings(ButtonFunctionMappingConfig config)
        {
            if (config.Mappings == null)
            {
                return;
            }

            var validFunctions = new[] { "None", "QuickMedia" };

            foreach (var mapping in config.Mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.PinName))
                {
                    throw new ArgumentException("Button function mapping has empty pinName.");
                }

                if (!validFunctions.Contains(mapping.Function))
                {
                    throw new ArgumentException($"Unknown button function '{mapping.Function}'.");
                }
            }
        }
    }
}
