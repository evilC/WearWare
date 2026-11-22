using System.Text.Json;
using WearWare.Config;
using WearWare.Services.Button.Providers;
using WearWare.Utils;

namespace WearWare.Services.Button
{
    /// <summary>
    /// Manages button providers and configuration for the wearable device.
    /// </summary>
    public sealed class ButtonManager : IDisposable
    {
        private readonly ILogger<ButtonManager> _logger;
        private readonly ButtonFunctionMappingService _mappingService;
        private static readonly string ConfigFilePath = Path.Combine(PathConfig.ConfigPath, "buttonsconfig.json");
        private readonly QuickMediaButtonsConfig _config;
        private IButtonGpioProvider? _provider;

        /// <summary>
        /// Provides access to the button GPIO provider.
        /// Will be null if button configuration is disabled or initialization failed.
        /// </summary>
        public IButtonGpioProvider? Provider => _provider;

        public ButtonManager(ILogger<ButtonManager> logger, ButtonFunctionMappingService mappingService)
        {
            _logger = logger;
            _mappingService = mappingService;
            _config = LoadConfig();
        }

        /// <summary>
        /// Initializes the button provider based on configuration.
        /// </summary>
        public void Initialize()
        {
            var config = _config;

            // No button pins configured
            if (config.ButtonPins == null || config.ButtonPins.Length == 0)
            {
                _logger.LogInformation("No button pins configured. Buttons disabled.");
                return;
            }

            try
            {
                if (config.Provider == "PiGpio")
                {
                    _provider = new PiGpioButtonProvider(config.ButtonPins);
                    _logger.LogInformation(
                        "Initialized button provider: PiGpio with {count} pins ({pins}).",
                        config.ButtonPins.Length,
                        string.Join(", ", config.ButtonPins.Select(p => _provider.GetPinName(p))));
                }
                else if (config.Provider == "Mcp23017")
                {
                    _provider = new Mcp23017ButtonProvider(config.ButtonPins, config.InterruptPins!);
                    _logger.LogInformation(
                        "Initialized button provider: Mcp23017 with {count} pins ({pins}).",
                        config.ButtonPins.Length,
                        string.Join(", ", config.ButtonPins.Select(p => _provider.GetPinName(p))));
                }
                else
                {
                    _logger.LogError("Unknown button provider '{provider}'.", config.Provider);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize button provider '{provider}'.", config.Provider);
                _provider?.Dispose();
                _provider = null;
            }
        }

        /// <summary>
        /// Gets information about an available button.
        /// </summary>
        /// <param name="index">The button index (0-based).</param>
        /// <returns>Button information, or null if button doesn't exist.</returns>
        public ButtonInfo? GetButtonInfo(int index)
        {
            if (_provider == null)
            {
                return null;
            }

            if (index < 0 || index >= _provider.GetButtonPinCount())
            {
                return null;
            }

            int pinNumber = _provider.GetButtonPinNumber(index);
            string pinName = _provider.GetPinName(pinNumber);

            return new ButtonInfo(index, pinNumber, pinName);
        }

        /// <summary>
        /// Enumerates information about all available buttons.
        /// </summary>
        public IEnumerable<ButtonInfo> GetAllButtons()
        {
            if (_provider == null)
            {
                yield break;
            }

            int count = _provider.GetButtonPinCount();
            for (int i = 0; i < count; i++)
            {
                int pinNumber = _provider.GetButtonPinNumber(i);
                string pinName = _provider.GetPinName(pinNumber);
                yield return new ButtonInfo(i, pinNumber, pinName);
            }
        }

        /// <summary>
        /// Gets all buttons assigned to a specific function.
        /// </summary>
        /// <param name="function">The function name (e.g., "QuickMedia", "None").</param>
        /// <returns>Enumerable of ButtonInfo for buttons assigned to this function.</returns>
        public IEnumerable<ButtonInfo> GetButtonsForFunction(string function)
        {
            var allButtons = GetAllButtons().ToList();
            if (!allButtons.Any())
            {
                yield break;
            }

            foreach (var button in allButtons)
            {
                if (_mappingService.GetFunctionForPin(button.PinName) == function)
                {
                    yield return button;
                }
            }
        }

        private QuickMediaButtonsConfig LoadConfig()
        {
            if (!File.Exists(ConfigFilePath))
            {
                var config = new QuickMediaButtonsConfig();
                try
                {
                    JsonUtils.ToJsonFile(ConfigFilePath, config);
                    _logger.LogInformation("Created default QuickMedia button configuration at {filename}.", ConfigFilePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(ex, "Could not create QuickMedia button configuration at {filename}. Buttons are disabled.", ConfigFilePath);
                }

                return config;
            }

            try
            {
                var config = JsonUtils.FromJsonFile<QuickMediaButtonsConfig>(ConfigFilePath)
                    ?? throw new JsonException("Button configuration file contains null.");
                Validate(config);
                return config;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                _logger.LogError(ex, "Invalid QuickMedia button configuration in {filename}. Buttons are disabled.", ConfigFilePath);
                return new QuickMediaButtonsConfig();
            }
        }

        private static void Validate(QuickMediaButtonsConfig config)
        {
            if (config.ButtonPins == null)
            {
                throw new JsonException("buttonPins must be an array.");
            }

            if (config.Provider == "PiGpio")
            {
                if (config.InterruptPins != null)
                {
                    throw new JsonException("interruptPins is only valid for the Mcp23017 provider.");
                }

                ValidateDistinctPins(config.ButtonPins, 0, 27, "Pi GPIO");
                if (config.ExcludeI2cPins && config.ButtonPins.Any(pin => pin is 2 or 3))
                {
                    throw new JsonException("buttonPins cannot include GPIO2 or GPIO3 when excludeI2cPins is true.");
                }

                return;
            }

            if (config.Provider != "Mcp23017")
            {
                throw new JsonException($"Unsupported button provider '{config.Provider}'.");
            }

            if (config.ExcludeI2cPins)
            {
                throw new JsonException("excludeI2cPins is only valid for the PiGpio provider.");
            }

            if (config.InterruptPins == null || config.InterruptPins.Length != 2)
            {
                throw new JsonException("Mcp23017 requires two interruptPins entries in IA, IB order.");
            }

            var interruptA = config.InterruptPins[0];
            var interruptB = config.InterruptPins[1];
            ValidateInterruptPin(interruptA, "IA");
            ValidateInterruptPin(interruptB, "IB");
            if (interruptA.HasValue && interruptA == interruptB)
            {
                throw new JsonException("IA and IB must use different Pi GPIO pins.");
            }

            var seenPins = new HashSet<int>();
            foreach (var pin in config.ButtonPins)
            {
                if (pin < 0 || pin > 15 || pin % 8 == 7)
                {
                    throw new JsonException($"Mcp23017 button pin {pin} is not a supported input pin.");
                }

                if (!seenPins.Add(pin))
                {
                    throw new JsonException($"Mcp23017 button pin {pin} is specified more than once.");
                }

                if ((pin < 8 && !interruptA.HasValue) || (pin >= 8 && !interruptB.HasValue))
                {
                    throw new JsonException($"Mcp23017 button pin {pin} requires its port's interrupt pin.");
                }
            }
        }

        private static void ValidateDistinctPins(IEnumerable<int> pins, int minimum, int maximum, string pinType)
        {
            var seenPins = new HashSet<int>();
            foreach (var pin in pins)
            {
                if (pin < minimum || pin > maximum)
                {
                    throw new JsonException($"{pinType} pin {pin} must be between {minimum} and {maximum}.");
                }

                if (!seenPins.Add(pin))
                {
                    throw new JsonException($"{pinType} pin {pin} is specified more than once.");
                }
            }
        }

        private static void ValidateInterruptPin(int? pin, string line)
        {
            if (pin is < 0 or > 27)
            {
                throw new JsonException($"{line} must be a Pi BCM GPIO number from 0 to 27, or null.");
            }

            if (pin is 2 or 3)
            {
                throw new JsonException($"{line} cannot use GPIO2 or GPIO3 because they are reserved for I2C.");
            }
        }

        public void Dispose()
        {
            _provider?.Dispose();
        }
    }

    /// <summary>
    /// Information about an available button.
    /// </summary>
    public sealed record ButtonInfo(int Index, int PinNumber, string PinName);
}
