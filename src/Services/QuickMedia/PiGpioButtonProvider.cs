using System.Device.Gpio;

namespace WearWare.Services.QuickMedia
{
    public sealed class PiGpioButtonProvider : IButtonGpioProvider
    {
        private readonly Lazy<GpioController> _controller;

        public IReadOnlyList<int> ButtonPins { get; }

        public GpioController Controller => _controller.Value;

        public PiGpioButtonProvider(IReadOnlyList<int> buttonPins)
        {
            ArgumentNullException.ThrowIfNull(buttonPins);
            var pins = buttonPins.ToArray();
            if (pins.Any(pin => pin < 0 || pin > 27))
            {
                throw new ArgumentOutOfRangeException(nameof(buttonPins), "Raspberry Pi GPIO numbers must be between 0 and 27.");
            }

            if (pins.Distinct().Count() != pins.Length)
            {
                throw new ArgumentException("Pi GPIO button pins must be unique.", nameof(buttonPins));
            }

            ButtonPins = pins;
            _controller = new Lazy<GpioController>(() => new GpioController(), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public void Dispose()
        {
            if (_controller.IsValueCreated)
            {
                _controller.Value.Dispose();
            }
        }
    }
}
