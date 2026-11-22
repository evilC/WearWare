using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Services.Button
{
    public sealed class ButtonFunctionMappingService
    {
        private readonly ILogger<ButtonFunctionMappingService> _logger;
        private static readonly string ConfigFilePath = Path.Combine(PathConfig.ConfigPath, "buttonfunctionmapping.json");
        private ButtonFunctionMappingConfig _config;

        public ButtonFunctionMappingConfig Config => Volatile.Read(ref _config);
        public event Action? StateChanged;

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
            var mappings = Config.Mappings;
            if (mappings == null)
            {
                return "None";
            }

            var mapping = mappings.FirstOrDefault(m => m.PinName == pinName);
            return mapping?.Function ?? "None";
        }

        /// <summary>
        /// Sets the function assignment for a button pin.
        /// </summary>
        internal void SetFunctionForPin(string pinName, string function)
        {
            var mappings = (Config.Mappings ?? [])
                .Where(m => m.PinName != pinName)
                .Select(m => new ButtonFunctionMapping { PinName = m.PinName, Function = m.Function })
                .Append(new ButtonFunctionMapping { PinName = pinName, Function = function })
                .ToArray();
            var updated = new ButtonFunctionMappingConfig { Mappings = mappings };
            ValidateMappings(updated);

            try
            {
                JsonUtils.ToJsonFileAtomic(ConfigFilePath, updated, _logger);
                Volatile.Write(ref _config, updated);
                _logger.LogInformation("Updated button function mapping: {pinName} -> {function}", pinName, function);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Failed to save button function mapping for pin {pinName}.", pinName);
                throw;
            }
        }

        internal void NotifyStateChanged()
        {
            var handlers = StateChanged;
            if (handlers == null)
                return;

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "A button mapping listener failed.");
                }
            }
        }

        private static void ValidateMappings(ButtonFunctionMappingConfig config)
        {
            if (config.Mappings == null)
            {
                return;
            }

            var validFunctions = Enum.GetNames<ButtonFunction>();

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
