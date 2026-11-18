using System.Text.Json;
using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Services.QuickMedia
{
    public sealed class QuickMediaButtonsConfigService
    {
        private readonly ILogger<QuickMediaButtonsConfigService> _logger;
        private static readonly string ConfigFilePath = Path.Combine(PathConfig.ConfigPath, "buttonsconfig.json");

        public QuickMediaButtonsConfig Config { get; }

        public QuickMediaButtonsConfigService(ILogger<QuickMediaButtonsConfigService> logger)
        {
            _logger = logger;
            if (!File.Exists(ConfigFilePath))
            {
                Config = new QuickMediaButtonsConfig();
                try
                {
                    JsonUtils.ToJsonFile(ConfigFilePath, Config);
                    _logger.LogInformation("Created default QuickMedia button configuration at {filename}.", ConfigFilePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(ex, "Could not create QuickMedia button configuration at {filename}. Buttons are disabled.", ConfigFilePath);
                }

                return;
            }

            try
            {
                Config = JsonUtils.FromJsonFile<QuickMediaButtonsConfig>(ConfigFilePath)
                    ?? throw new JsonException("Button configuration file contains null.");
                Validate(Config);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                _logger.LogError(ex, "Invalid QuickMedia button configuration in {filename}. Buttons are disabled.", ConfigFilePath);
                Config = new QuickMediaButtonsConfig();
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
    }
}
