using System.Device.Gpio;

namespace WearWare.Architecture.Button
{
    /// <summary>
    /// Provides button input functionality using the Raspberry Pi's native GPIO pins.
    /// </summary>
    public sealed class PiGpioButtonProvider : IButtonGpioProvider
    {
        private readonly int[] _buttonPins;
        private readonly GpioController _controller;

        public GpioController Controller => _controller;

        /// <summary>
        /// Creates a new PiGpioButtonProvider.
        /// </summary>
        /// <param name="buttonPins">List of Pi GPIO pin numbers to use as buttons.</param>
        public PiGpioButtonProvider(IReadOnlyList<int> buttonPins)
        {
            ArgumentNullException.ThrowIfNull(buttonPins);

            _buttonPins = buttonPins.ToArray();

            foreach (var pin in _buttonPins)
            {
                if (pin < 0 || pin > 27)
                {
                    throw new ArgumentOutOfRangeException(nameof(buttonPins), pin, "Raspberry Pi GPIO numbers must be between 0 and 27.");
                }
            }

            _controller = new GpioController();
        }

        public int GetButtonPinCount() => _buttonPins.Length;

        public int GetButtonPinNumber(int index)
        {
            if (index < 0 || index >= _buttonPins.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "No button pin at this index.");
            }

            return _buttonPins[index];
        }

        public string GetPinName(int pinNumber)
        {
            if (pinNumber < 0 || pinNumber > 27)
            {
                throw new ArgumentOutOfRangeException(nameof(pinNumber), pinNumber, "Raspberry Pi GPIO numbers must be between 0 and 27.");
            }

            return $"GPIO{pinNumber}";
        }

        public void Dispose()
        {
            _controller.Dispose();
        }
    }
}
