using Iot.Device.Mcp23xxx;
using System.Device.Gpio;
using System.Device.I2c;

namespace WearWare.Architecture.Button
{
    /// <summary>
    /// Provides button input functionality using an Adafruit MCP23017 I2C GPIO expander.
    /// </summary>
    public sealed class Mcp23017ButtonProvider : IButtonGpioProvider
    {
        private const int I2cBus = 1;
        private const int I2cAddress = 0x20;
        private readonly int[] _buttonPins;
        private readonly IReadOnlyList<int?> _interruptPins;
        private readonly Lazy<HardwareResources> _hardware;

        public GpioController Controller => _hardware.Value.ExpanderController;

        /// <summary>
        /// Creates a new Mcp23017ButtonProvider.
        /// </summary>
        /// <param name="buttonPins">List of expander pin numbers (0-15) to use as buttons.</param>
        /// <param name="interruptPins">Interrupt pins for ports A and B (IA, IB) - use null for unconnected ports.</param>
        public Mcp23017ButtonProvider(IReadOnlyList<int> buttonPins, IReadOnlyList<int?> interruptPins)
        {
            ArgumentNullException.ThrowIfNull(buttonPins);
            ArgumentNullException.ThrowIfNull(interruptPins);

            if (interruptPins.Count != 2)
            {
                throw new ArgumentException("Specify two interrupt pins in IA, IB order; use null for an unconnected line.", nameof(interruptPins));
            }

            _buttonPins = buttonPins.ToArray();
            _interruptPins = interruptPins.ToArray();

            foreach (var pin in _buttonPins)
            {
                if (pin < 0 || pin >= 16)
                {
                    throw new ArgumentOutOfRangeException(nameof(buttonPins), pin, "MCP23017 button pins must be between 0 and 15.");
                }
            }

            var interruptA = _interruptPins[0];
            var interruptB = _interruptPins[1];

            if (interruptA.HasValue && (interruptA < 0 || interruptA > 27))
            {
                throw new ArgumentOutOfRangeException(nameof(interruptPins), "Interrupt pin A must be a Pi GPIO number 0-27 or null.");
            }

            if (interruptB.HasValue && (interruptB < 0 || interruptB > 27))
            {
                throw new ArgumentOutOfRangeException(nameof(interruptPins), "Interrupt pin B must be a Pi GPIO number 0-27 or null.");
            }

            if (interruptA.HasValue && interruptA == interruptB)
            {
                throw new ArgumentException("Interrupt pins IA and IB must use different GPIO pins.", nameof(interruptPins));
            }

            _hardware = new Lazy<HardwareResources>(InitializeHardware, LazyThreadSafetyMode.ExecutionAndPublication);
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
            if (pinNumber < 0 || pinNumber >= 16)
            {
                throw new ArgumentOutOfRangeException(nameof(pinNumber), pinNumber, "MCP23017 pin numbers must be between 0 and 15.");
            }

            return pinNumber < 8 ? $"A{pinNumber}" : $"B{pinNumber - 8}";
        }

        public void Dispose()
        {
            if (_hardware.IsValueCreated)
            {
                _hardware.Value.Dispose();
            }
        }

        private HardwareResources InitializeHardware()
        {
            GpioController? piGpio = null;
            GpioController? expanderGpio = null;
            I2cDevice? i2cDevice = null;
            Mcp23017? expander = null;

            try
            {
                piGpio = new GpioController();
                i2cDevice = I2cDevice.Create(new I2cConnectionSettings(I2cBus, I2cAddress));
                expander = new Mcp23017(
                    i2cDevice,
                    interruptA: _interruptPins[0] ?? -1,
                    interruptB: _interruptPins[1] ?? -1,
                    controller: piGpio,
                    shouldDispose: false);

                var mcp23017 = expander;
                expanderGpio = new GpioController(mcp23017);
                expander = null;

                // Set all pins to inputs
                mcp23017.WriteByte(Register.IODIR, 0xFF, Port.PortA);
                mcp23017.WriteByte(Register.IODIR, 0xFF, Port.PortB);

                // Set output latches low
                mcp23017.WriteByte(Register.OLAT, 0x00, Port.PortA);
                mcp23017.WriteByte(Register.OLAT, 0x00, Port.PortB);

                // Enable pull-ups on pins 0-6 of each port
                mcp23017.WriteByte(Register.GPPU, 0x7F, Port.PortA);
                mcp23017.WriteByte(Register.GPPU, 0x7F, Port.PortB);

                // Keep pins 0-6 as inputs and set pin 7 as output
                mcp23017.WriteByte(Register.IODIR, 0x7F, Port.PortA);
                mcp23017.WriteByte(Register.IODIR, 0x7F, Port.PortB);

                var resources = new HardwareResources(expanderGpio, piGpio, i2cDevice);
                expanderGpio = null;
                piGpio = null;
                i2cDevice = null;
                return resources;
            }
            catch
            {
                expanderGpio?.Dispose();
                expander?.Dispose();
                i2cDevice?.Dispose();
                piGpio?.Dispose();
                throw;
            }
        }

        private sealed class HardwareResources(
            GpioController expanderController,
            GpioController piController,
            I2cDevice i2cDevice) : IDisposable
        {
            public GpioController ExpanderController { get; } = expanderController;

            public void Dispose()
            {
                try
                {
                    ExpanderController.Dispose();
                }
                finally
                {
                    try
                    {
                        piController.Dispose();
                    }
                    finally
                    {
                        i2cDevice.Dispose();
                    }
                }
            }
        }
    }
}
