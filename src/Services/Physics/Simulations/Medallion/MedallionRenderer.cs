using System.Numerics;
using RPiRgbLEDMatrix;
using WearWare.Services.Matrix;
using WearWare.Services.MatrixConfig;
using WearWare.Services.Physics.Imu;

namespace WearWare.Services.Physics.Simulations.Medallion
{
    public sealed class MedallionRenderer : PhysicsRenderer
    {
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
            DrawChain(_simulation.Chain);
            DrawMedallion(_simulation.MedallionCenter, _simulation.Radius, _simulation.Angle);
            canvas.SetPixels(0, 0, _width, _height, _pixels);
        }

        private void DrawChain(ReadOnlySpan<Vector2> chain)
        {
            var distanceAlongChain = 0f;
            for (var i = 0; i < chain.Length - 1; i++)
            {
                var segment = chain[i + 1] - chain[i];
                var length = segment.Length();
                var samples = Math.Max(1, (int)MathF.Ceiling(length * 2));
                for (var sample = 0; sample <= samples; sample++)
                {
                    var t = (float)sample / samples;
                    var point = chain[i] + segment * t;
                    var x = (int)MathF.Round(point.X);
                    var y = (int)MathF.Round(point.Y);
                    if (x < 0 || x >= _width || y < 0 || y >= _height)
                        continue;
                    var highlight = (int)((distanceAlongChain + length * t) / 3) % 2 == 0;
                    _pixels[y * _width + x] = highlight ? new Color(220, 180, 65) : new Color(120, 80, 20);
                }
                distanceAlongChain += length;
            }
        }

        private void DrawMedallion(Vector2 center, float radius, float angle)
        {
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            var reach = radius * 1.75f;
            var left = Math.Max(0, (int)MathF.Floor(center.X - reach));
            var right = Math.Min(_width - 1, (int)MathF.Ceiling(center.X + reach));
            var top = Math.Max(0, (int)MathF.Floor(center.Y - reach));
            var bottom = Math.Min(_height - 1, (int)MathF.Ceiling(center.Y + reach));

            for (var y = top; y <= bottom; y++)
            {
                for (var x = left; x <= right; x++)
                {
                    var coverage = 0;
                    for (var sy = 0; sy < 4; sy++)
                    {
                        for (var sx = 0; sx < 4; sx++)
                        {
                            var dx = (x + (sx - 1.5f) / 4 - center.X) / radius;
                            var dy = (y + (sy - 1.5f) / 4 - center.Y) / radius;
                            if (MedallionArtwork.IsMetal(dx * cos + dy * sin, -dx * sin + dy * cos))
                                coverage++;
                        }
                    }

                    if (coverage == 0)
                        continue;
                    var metal = y - center.Y < -0.2f * radius
                        ? new Color(235, 240, 245)
                        : new Color(160, 175, 190);
                    var background = _pixels[y * _width + x];
                    _pixels[y * _width + x] = new Color(
                        (metal.R * coverage + background.R * (16 - coverage)) / 16,
                        (metal.G * coverage + background.G * (16 - coverage)) / 16,
                        (metal.B * coverage + background.B * (16 - coverage)) / 16);
                }
            }
        }
    }
}
