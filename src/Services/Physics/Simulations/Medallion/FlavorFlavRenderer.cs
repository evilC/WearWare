using System.Numerics;
using RPiRgbLEDMatrix;
using WearWare.Services.Matrix;
using WearWare.Services.MatrixConfig;
using WearWare.Services.Physics.Imu;

namespace WearWare.Services.Physics.Simulations.Medallion
{
    public sealed class FlavorFlavRenderer : NecklaceRenderer
    {
        private static readonly Color FaceColor = new(245, 245, 235);
        private static readonly Color MarkColor = new(25, 30, 35);
        private static readonly Color SecondColor = new(210, 35, 25);

        public FlavorFlavRenderer(ImuService imu, IMatrixDisplay display, MatrixConfigService config, ILogger<FlavorFlavRenderer> logger)
            : base(imu, display, config, logger, "Flavor Flav.svg")
        {
        }

        protected override void DrawBody(Vector2 center, float radius, float angle) =>
            DrawClock(center, radius, angle, DateTime.Now);

        internal void DrawClock(Vector2 center, float radius, float angle, DateTime time)
        {
            base.DrawBody(center, radius, angle);
            DrawFace(center, radius * 0.84f);

            for (var hour = 0; hour < 12; hour++)
            {
                var direction = Direction(angle + hour * MathF.Tau / 12);
                var inner = hour % 3 == 0 ? 0.64f : 0.69f;
                DrawLine(center + direction * (radius * inner), center + direction * (radius * 0.77f),
                    radius * 0.045f, MarkColor);
            }

            var seconds = (float)time.TimeOfDay.TotalSeconds;
            var hourDirection = Direction(angle + seconds * MathF.Tau / (12 * 60 * 60));
            var minuteDirection = Direction(angle + seconds * MathF.Tau / (60 * 60));
            var secondDirection = Direction(angle + seconds * MathF.Tau / 60);
            DrawLine(center - hourDirection * (radius * 0.08f), center + hourDirection * (radius * 0.46f),
                radius * 0.075f, MarkColor);
            DrawLine(center - minuteDirection * (radius * 0.1f), center + minuteDirection * (radius * 0.66f),
                radius * 0.05f, MarkColor);
            DrawLine(center - secondDirection * (radius * 0.16f), center + secondDirection * (radius * 0.7f),
                radius * 0.023f, SecondColor);
        }

        private static Vector2 Direction(float angle) => new(MathF.Sin(angle), -MathF.Cos(angle));

        private void DrawFace(Vector2 center, float radius)
        {
            var left = Math.Max(0, (int)MathF.Floor(center.X - radius - 1));
            var right = Math.Min(Width - 1, (int)MathF.Ceiling(center.X + radius + 1));
            var top = Math.Max(0, (int)MathF.Floor(center.Y - radius - 1));
            var bottom = Math.Min(Height - 1, (int)MathF.Ceiling(center.Y + radius + 1));
            for (var y = top; y <= bottom; y++)
            {
                for (var x = left; x <= right; x++)
                {
                    var coverage = 0;
                    for (var sy = 0; sy < SamplesPerAxis; sy++)
                    {
                        for (var sx = 0; sx < SamplesPerAxis; sx++)
                        {
                            var sample = new Vector2(x + (sx + 0.5f) / SamplesPerAxis - 0.5f,
                                y + (sy + 0.5f) / SamplesPerAxis - 0.5f);
                            if (Vector2.DistanceSquared(sample, center) <= radius * radius)
                                coverage++;
                        }
                    }
                    if (coverage > 0)
                        BlendPixel(x, y, FaceColor, coverage);
                }
            }
        }

        private void DrawLine(Vector2 start, Vector2 end, float halfWidth, Color color)
        {
            var left = Math.Max(0, (int)MathF.Floor(MathF.Min(start.X, end.X) - halfWidth - 1));
            var right = Math.Min(Width - 1, (int)MathF.Ceiling(MathF.Max(start.X, end.X) + halfWidth + 1));
            var top = Math.Max(0, (int)MathF.Floor(MathF.Min(start.Y, end.Y) - halfWidth - 1));
            var bottom = Math.Min(Height - 1, (int)MathF.Ceiling(MathF.Max(start.Y, end.Y) + halfWidth + 1));
            var line = end - start;
            var lengthSquared = line.LengthSquared();
            for (var y = top; y <= bottom; y++)
            {
                for (var x = left; x <= right; x++)
                {
                    var coverage = 0;
                    for (var sy = 0; sy < SamplesPerAxis; sy++)
                    {
                        for (var sx = 0; sx < SamplesPerAxis; sx++)
                        {
                            var sample = new Vector2(x + (sx + 0.5f) / SamplesPerAxis - 0.5f,
                                y + (sy + 0.5f) / SamplesPerAxis - 0.5f);
                            var projection = Math.Clamp(Vector2.Dot(sample - start, line) / lengthSquared, 0, 1);
                            if (Vector2.DistanceSquared(sample, start + line * projection) <= halfWidth * halfWidth)
                                coverage++;
                        }
                    }
                    if (coverage > 0)
                        BlendPixel(x, y, color, coverage);
                }
            }
        }
    }
}
