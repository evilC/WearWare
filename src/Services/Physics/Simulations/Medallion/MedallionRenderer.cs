using System.Numerics;
using RPiRgbLEDMatrix;
using WearWare.Services.Matrix;
using WearWare.Services.MatrixConfig;
using WearWare.Services.Physics.Imu;

namespace WearWare.Services.Physics.Simulations.Medallion
{
    public sealed class MedallionRenderer : PhysicsRenderer
    {
        // 1 is the finer chain; 2 is the chunkier chain.
        private const float ChainChunkiness = 2f;
        private const int SamplesPerAxis = 4;
        private const int CoverageSamples = SamplesPerAxis * SamplesPerAxis;
        private MedallionSimulation? _simulation;
        private Color[] _pixels = [];
        private int _width;
        private int _height;

        public MedallionRenderer(ImuService imu, IMatrixDisplay display, MatrixConfigService config, ILogger<MedallionRenderer> logger)
            : base(imu, display, config, logger)
        {
        }

        protected override void Reset()
        {
            MedallionArtwork.Prepare();
            _simulation = null;
            _pixels = [];
        }

        protected override void DrawFrame(RGBLedCanvas canvas, ImuReading reading, double elapsedSeconds)
        {
            if (_simulation == null || _width != canvas.Width || _height != canvas.Height)
            {
                _width = canvas.Width;
                _height = canvas.Height;
                _simulation = new MedallionSimulation(_width, _height);
                _pixels = new Color[checked(_width * _height)];
            }

            _simulation.Update(reading.MountedAcceleration, elapsedSeconds);
            Array.Clear(_pixels);
            DrawChain(_simulation.Chain, foreground: false);
            DrawMedallion(_simulation.MedallionCenter, _simulation.Radius, _simulation.Angle, loop: true);
            DrawChain(_simulation.Chain, foreground: true);
            DrawMedallion(_simulation.MedallionCenter, _simulation.Radius, _simulation.Angle, loop: false);
            canvas.SetPixels(0, 0, _width, _height, _pixels);
        }

        private void DrawChain(ReadOnlySpan<Vector2> chain, bool foreground)
        {
            var chunkiness = Math.Clamp(ChainChunkiness - 1, 0, 1);
            var scale = Math.Max(0.5f, Math.Min(_width, _height) / 128f);
            var spacing = (5.2f + 0.2f * chunkiness) * scale;
            var majorRadius = (3.5f + 0.4f * chunkiness) * scale;
            var minorRadius = (1.7f + 0.7f * chunkiness) * scale;
            var thickness = (0.95f + 0.2f * chunkiness) * scale;
            var distanceAlongChain = 0f;
            var nextLink = 0f;
            for (var i = 0; i < chain.Length - 1; i++)
            {
                var segment = chain[i + 1] - chain[i];
                var length = segment.Length();
                if (length < 0.0001f)
                    continue;

                var tangent = segment / length;
                while (nextLink <= distanceAlongChain + length)
                {
                    if ((i < chain.Length / 2) == foreground)
                    {
                        var center = chain[i] + tangent * (nextLink - distanceAlongChain);
                        DrawLink(center, tangent, majorRadius, minorRadius, thickness);
                    }
                    nextLink += spacing;
                }
                distanceAlongChain += length;
            }
        }

        private void DrawLink(Vector2 center, Vector2 tangent, float majorRadius, float minorRadius, float thickness)
        {
            var normal = new Vector2(-tangent.Y, tangent.X);
            var left = Math.Max(0, (int)MathF.Floor(center.X - majorRadius - 1));
            var right = Math.Min(_width - 1, (int)MathF.Ceiling(center.X + majorRadius + 1));
            var top = Math.Max(0, (int)MathF.Floor(center.Y - majorRadius - 1));
            var bottom = Math.Min(_height - 1, (int)MathF.Ceiling(center.Y + majorRadius + 1));
            var innerMajorRadius = majorRadius - thickness;
            var innerMinorRadius = minorRadius - thickness;
            for (var y = top; y <= bottom; y++)
            {
                for (var x = left; x <= right; x++)
                {
                    var coverage = 0;
                    for (var sy = 0; sy < SamplesPerAxis; sy++)
                    {
                        for (var sx = 0; sx < SamplesPerAxis; sx++)
                        {
                            var offset = new Vector2(
                                x + (sx + 0.5f) / SamplesPerAxis - 0.5f - center.X,
                                y + (sy + 0.5f) / SamplesPerAxis - 0.5f - center.Y);
                            var u = Vector2.Dot(offset, tangent);
                            var v = Vector2.Dot(offset, normal);
                            var outer = u * u / (majorRadius * majorRadius) + v * v / (minorRadius * minorRadius);
                            var inner = u * u / (innerMajorRadius * innerMajorRadius) + v * v / (innerMinorRadius * innerMinorRadius);
                            if (outer <= 1 && inner >= 1)
                                coverage++;
                        }
                    }

                    if (coverage == 0)
                        continue;
                    var side = Vector2.Dot(new Vector2(x, y) - center, normal);
                    BlendPixel(x, y, side < 0 ? new Color(255, 200, 80) : new Color(150, 95, 18), coverage);
                }
            }
        }

        private void DrawMedallion(Vector2 center, float radius, float angle, bool loop)
        {
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            var boundsCenter = loop
                ? center + new Vector2(sin, -cos) * (radius * MedallionArtwork.AttachmentDistance)
                : center;
            var reach = radius * (loop ? 0.26f : 1.75f) + 1;
            var left = Math.Max(0, (int)MathF.Floor(boundsCenter.X - reach));
            var right = Math.Min(_width - 1, (int)MathF.Ceiling(boundsCenter.X + reach));
            var top = Math.Max(0, (int)MathF.Floor(boundsCenter.Y - reach));
            var bottom = Math.Min(_height - 1, (int)MathF.Ceiling(boundsCenter.Y + reach));

            for (var y = top; y <= bottom; y++)
            {
                for (var x = left; x <= right; x++)
                {
                    var coverage = 0;
                    for (var sy = 0; sy < SamplesPerAxis; sy++)
                    {
                        for (var sx = 0; sx < SamplesPerAxis; sx++)
                        {
                            var dx = (x + (sx + 0.5f) / SamplesPerAxis - 0.5f - center.X) / radius;
                            var dy = (y + (sy + 0.5f) / SamplesPerAxis - 0.5f - center.Y) / radius;
                            if (MedallionArtwork.IsMetal(dx * cos + dy * sin, -dx * sin + dy * cos, loop))
                                coverage++;
                        }
                    }

                    if (coverage == 0)
                        continue;
                    var metal = y - center.Y < -0.2f * radius
                        ? new Color(235, 240, 245)
                        : new Color(160, 175, 190);
                    BlendPixel(x, y, metal, coverage);
                }
            }
        }

        private void BlendPixel(int x, int y, Color color, int coverage)
        {
            var background = _pixels[y * _width + x];
            _pixels[y * _width + x] = new Color(
                (color.R * coverage + background.R * (CoverageSamples - coverage)) / CoverageSamples,
                (color.G * coverage + background.G * (CoverageSamples - coverage)) / CoverageSamples,
                (color.B * coverage + background.B * (CoverageSamples - coverage)) / CoverageSamples);
        }
    }
}
