using Iot.Device.Button;

namespace WearWare.Services.Button
{
    public sealed class ButtonHandler : IDisposable
    {
        private readonly ButtonManager _buttonManager;
        private readonly ButtonFunctionMappingService _mappingService;
        private readonly ILogger<ButtonHandler> _logger;
        private readonly List<GpioButton> _buttons = [];
        private bool _initialized;

        public event Action<int>? QuickMediaPressed;

        public ButtonHandler(
            ButtonManager buttonManager,
            ButtonFunctionMappingService mappingService,
            ILogger<ButtonHandler> logger)
        {
            _buttonManager = buttonManager;
            _mappingService = mappingService;
            _logger = logger;
        }

        public void Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("Button handler is already initialized.");
            _initialized = true;

            var provider = _buttonManager.Provider;
            if (provider == null)
            {
                _logger.LogInformation("[BUTTONHANDLER] No button provider available; button events disabled.");
                return;
            }

            foreach (var button in _buttonManager.GetAllButtons())
            {
                try
                {
                    var gpioButton = new GpioButton(
                        buttonPin: button.PinNumber,
                        gpio: provider.Controller,
                        shouldDispose: false);
                    gpioButton.Press += (_, _) => OnPressed(button);
                    _buttons.Add(gpioButton);
                    _logger.LogInformation("[BUTTONHANDLER] Subscribed to button {pin} ({name}) as button {number}.",
                        button.PinNumber, button.PinName, button.Index + 1);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BUTTONHANDLER] Failed to subscribe to button {pin} ({name}).",
                        button.PinNumber, button.PinName);
                }
            }
        }

        private void OnPressed(ButtonInfo button)
        {
            try
            {
                if (_mappingService.GetFunctionForPin(button.PinName) == "QuickMedia")
                    QuickMediaPressed?.Invoke(button.Index + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BUTTONHANDLER] Failed to dispatch button {name}.", button.PinName);
            }
        }

        public void Dispose()
        {
            foreach (var button in _buttons)
            {
                try
                {
                    button.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BUTTONHANDLER] Failed to dispose a GPIO button.");
                }
            }
            _buttons.Clear();
        }
    }
}
