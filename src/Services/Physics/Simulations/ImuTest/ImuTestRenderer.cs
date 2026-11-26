using System.Numerics;
using RPiRgbLEDMatrix;
using WearWare.Services.Matrix;
using WearWare.Services.Physics.Imu;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.Physics.Simulations.ImuTest
{
    public sealed class ImuTestRenderer : PhysicsRenderer
    {
        private const double SmoothingSeconds = 0.15;
        private Vector2? _filteredUp;

        public ImuTestRenderer(ImuService imu, IMatrixDisplay display, MatrixConfigService config, ILogger<ImuTestRenderer> logger)
            : base(imu, display, config, logger)
        {
        }

        protected override void Reset() => _filteredUp = null;

        protected override void DrawFrame(RGBLedCanvas canvas, ImuReading reading, double elapsedSeconds)
        {
            var up = new Vector2(reading.MountedAcceleration.X, -reading.MountedAcceleration.Y);
            if (!float.IsFinite(up.X) || !float.IsFinite(up.Y) || !float.IsFinite(up.LengthSquared()) ||
                !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(reading), "Invalid IMU test input.");

            var weight = (float)(1 - Math.Exp(-elapsedSeconds / SmoothingSeconds));
            _filteredUp = _filteredUp is { } filtered ? Vector2.Lerp(filtered, up, weight) : up;

            var centerX = (canvas.Width - 1) / 2;
            var centerY = (canvas.Height - 1) / 2;
            var radius = (Math.Min(canvas.Width, canvas.Height) - 1) / 2;
            var faceColor = new Color(40, 40, 40);
            var referenceColor = new Color(0, 0, 191);
            var labelColor = new Color(180, 180, 180);
            var handColor = new Color(255, 255, 255);
            canvas.DrawCircle(centerX, centerY, radius, faceColor);

            var handRadius = radius;
            if (radius >= 8)
            {
                var scale = Math.Clamp(radius / 12, 1, 3);
                var width = 2 * scale;
                var height = 4 * scale;
                DrawLabel(canvas, 'U', centerX - scale, centerY - radius + 1, scale, labelColor);
                DrawLabel(canvas, 'D', centerX - scale, centerY + radius - height - 1, scale, labelColor);
                DrawLabel(canvas, 'R', centerX - radius + 1, centerY - 2 * scale, scale, labelColor);
                DrawLabel(canvas, 'L', centerX + radius - width - 1, centerY - 2 * scale, scale, labelColor);
                handRadius = Math.Max(1, radius - height - 3);
            }

            for (var offset = -1; offset <= 1; offset++)
            {
                if (centerY + offset >= 0 && centerY + offset < canvas.Height)
                    canvas.DrawLine(centerX - handRadius, centerY + offset, centerX + handRadius, centerY + offset, referenceColor);
                if (centerX + offset >= 0 && centerX + offset < canvas.Width)
                    canvas.DrawLine(centerX + offset, centerY - handRadius, centerX + offset, centerY + handRadius, referenceColor);
            }

            // A flat display has no meaningful in-plane up direction.
            if (up.LengthSquared() < 0.25f * 0.25f || _filteredUp.Value.LengthSquared() < 0.25f * 0.25f)
                return;
            var direction = Vector2.Normalize(_filteredUp.Value);
            canvas.DrawLine(centerX, centerY,
                centerX + (int)Math.Round(direction.X * handRadius),
                centerY + (int)Math.Round(direction.Y * handRadius), handColor);
        }

        private static void DrawLabel(RGBLedCanvas canvas, char label, int x, int y, int scale, Color color)
        {
            void Line(int x0, int y0, int x1, int y1) =>
                canvas.DrawLine(x + x0 * scale, y + y0 * scale, x + x1 * scale, y + y1 * scale, color);

            switch (label)
            {
                case 'U':
                    Line(0, 0, 0, 4);
                    Line(0, 4, 2, 4);
                    Line(2, 4, 2, 0);
                    break;
                case 'D':
                    Line(0, 0, 0, 4);
                    Line(0, 0, 2, 1);
                    Line(2, 1, 2, 3);
                    Line(2, 3, 0, 4);
                    break;
                case 'L':
                    Line(0, 0, 0, 4);
                    Line(0, 4, 2, 4);
                    break;
                case 'R':
                    Line(0, 0, 0, 4);
                    Line(0, 0, 2, 0);
                    Line(2, 0, 2, 2);
                    Line(2, 2, 0, 2);
                    Line(0, 2, 2, 4);
                    break;
            }
        }
    }
}
