using Iot.Device.Mcp23xxx;
using System.Device.Gpio;
using System.Device.I2c;

namespace WearWare.Services.QuickMedia
{
    public sealed class AdafruitMcp23017GpioProvider : IButtonGpioProvider
    {
        private const int I2cBus = 1;
        private const int I2cAddress = 0x20;
        private readonly IReadOnlyList<int?> _interruptPins;
        private readonly Lazy<HardwareResources> _hardware;

        public IReadOnlyList<int> ButtonPins { get; }

        public AdafruitMcp23017GpioProvider(IReadOnlyList<int> buttonPins, IReadOnlyList<int?> interruptPins)
        {
            ArgumentNullException.ThrowIfNull(buttonPins);
            ArgumentNullException.ThrowIfNull(interruptPins);
            if (interruptPins.Count != 2)
            {
                throw new ArgumentException("Specify two interrupt pins in IA, IB order; use null for an unconnected line.", nameof(interruptPins));
            }

            ButtonPins = buttonPins.ToArray();
            _interruptPins = interruptPins.ToArray();
            _hardware = new Lazy<HardwareResources>(InitializeHardware, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public GpioController Controller => _hardware.Value.ExpanderController;

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

                mcp23017.WriteByte(Register.IODIR, 0xFF, Port.PortA);
                mcp23017.WriteByte(Register.IODIR, 0xFF, Port.PortB);
                mcp23017.WriteByte(Register.OLAT, 0x00, Port.PortA);
                mcp23017.WriteByte(Register.OLAT, 0x00, Port.PortB);
                mcp23017.WriteByte(Register.GPPU, 0x7F, Port.PortA);
                mcp23017.WriteByte(Register.GPPU, 0x7F, Port.PortB);
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

        public void Dispose()
        {
            if (_hardware.IsValueCreated)
            {
                _hardware.Value.Dispose();
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
