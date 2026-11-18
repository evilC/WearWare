using RPiRgbLEDMatrix;
using WearWare.Services.Imu;
using WearWare.Services.Matrix;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.Simulations.ImuTest
{
    public sealed class ImuTestRenderer : PhysicsRenderer
    {
        public ImuTestRenderer(ImuService imu, MatrixService matrix, MatrixConfigService config, ILogger<ImuTestRenderer> logger)
            : base(imu, matrix, config, logger)
        {
        }

        protected override void DrawFrame(RGBLedCanvas canvas, ImuReading reading, double elapsedSeconds)
        {
            var radians = reading.FilteredTiltDegrees * Math.PI / 180;
            var centerX = (canvas.Width - 1) / 2.0;
            var centerY = (canvas.Height - 1) / 2.0;
            var radius = (Math.Min(canvas.Width, canvas.Height) - 1) / 2.0;
            var dx = radius * Math.Cos(radians);
            var dy = -radius * Math.Sin(radians);

            canvas.DrawLine(
                (int)Math.Round(centerX - dx), (int)Math.Round(centerY - dy),
                (int)Math.Round(centerX + dx), (int)Math.Round(centerY + dy),
                new Color(255, 255, 255));
        }
    }
}
