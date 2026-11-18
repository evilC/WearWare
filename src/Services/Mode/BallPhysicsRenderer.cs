using RPiRgbLEDMatrix;
using WearWare.Services.Imu;
using WearWare.Services.Matrix;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.Mode
{
    public sealed class BallPhysicsRenderer : PhysicsRenderer
    {
        private BallPhysics _ball = new BallPhysics();

        public BallPhysicsRenderer(ImuService imu, MatrixService matrix, MatrixConfigService config, ILogger<BallPhysicsRenderer> logger)
            : base(imu, matrix, config, logger)
        {
        }

        protected override void Reset() => _ball = new BallPhysics();

        protected override void DrawFrame(RGBLedCanvas canvas, ImuReading reading, double elapsedSeconds)
        {
            _ball.Update(reading.Acceleration, elapsedSeconds, canvas.Width, canvas.Height);
            DrawRamps(canvas, _ball.RampRadius);
            var centerX = (int)Math.Round(_ball.X);
            var centerY = (int)Math.Round(_ball.Y);
            var radius = _ball.Radius;
            var color = new Color(255, 255, 255);
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        canvas.SetPixel(centerX + x, centerY + y, color);
                    }
                }
            }
        }

        private static void DrawRamps(RGBLedCanvas canvas, int radius)
        {
            if (radius == 0) return;

            var color = new Color(45, 45, 45);
            var centerY = canvas.Height - 1 - radius;
            for (var y = centerY; y < canvas.Height; y++)
            {
                var dy = y - centerY;
                var x = (int)Math.Round(radius - Math.Sqrt(radius * radius - dy * dy));
                canvas.DrawLine(0, y, x, y, color);
                canvas.DrawLine(canvas.Width - 1 - x, y, canvas.Width - 1, y, color);
            }
        }
    }
}
