using RPiRgbLEDMatrix;
using WearWare.Services.Matrix;
using WearWare.Services.Physics.Imu;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.Physics.Simulations.Fluid
{
    public sealed class FluidRenderer : PhysicsRenderer
    {
        private FluidSimulation? _fluid;
        private float[] _density = [];
        private Color[] _pixels = [];
        private int _width;
        private int _height;

        public FluidRenderer(ImuService imu, IMatrixDisplay display, MatrixConfigService config, ILogger<FluidRenderer> logger)
            : base(imu, display, config, logger)
        {
        }

        protected override void Reset()
        {
            _fluid = null;
            _density = [];
            _pixels = [];
            _width = 0;
            _height = 0;
        }

        protected override void DrawFrame(RGBLedCanvas canvas, ImuReading reading, double elapsedSeconds)
        {
            if (_fluid == null || _width != canvas.Width || _height != canvas.Height)
            {
                _width = canvas.Width;
                _height = canvas.Height;
                _fluid = new FluidSimulation(_width, _height);
                _density = new float[checked(_width * _height)];
                _pixels = new Color[_density.Length];
            }

            _fluid.Update(reading.MountedAcceleration, elapsedSeconds);
            _fluid.WriteDensity(_density);
            var water = new Color(0, 100, 255);
            for (var i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = _density[i] >= 0.6f ? water : default;
            }
            canvas.SetPixels(0, 0, _width, _height, _pixels);
        }
    }
}
