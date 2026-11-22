namespace WearWare.Architecture.Button
{
    /// <summary>
    /// Manages button providers and configuration for the wearable device.
    /// </summary>
    public sealed class ButtonManager : IDisposable
    {
        private readonly ILogger<ButtonManager> _logger;
        private readonly QuickMediaButtonsConfigService _configService;
        private readonly ButtonFunctionMappingService _mappingService;
        private IButtonGpioProvider? _provider;

        /// <summary>
        /// Provides access to the button GPIO provider.
        /// Will be null if button configuration is disabled or initialization failed.
        /// </summary>
        public IButtonGpioProvider? Provider => _provider;

        public ButtonManager(ILogger<ButtonManager> logger, QuickMediaButtonsConfigService configService, ButtonFunctionMappingService mappingService)
        {
            _logger = logger;
            _configService = configService;
            _mappingService = mappingService;
        }

        /// <summary>
        /// Initializes the button provider based on configuration.
        /// </summary>
        public void Initialize()
        {
            var config = _configService.Config;

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
