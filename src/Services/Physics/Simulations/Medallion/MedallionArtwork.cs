using System.Numerics;

namespace WearWare.Services.Physics.Simulations.Medallion
{
    internal static class MedallionArtwork
    {
        private const float CenterX = 54.588f;
        private const float CenterY = 65.94f;
        private const float ModelRadius = 50;
        private const int MaskScale = 2;
        private const int MaskWidth = 219;
        private const int MaskHeight = 237;
        private static readonly bool[] Mask = CreateMask();

        public const float AttachmentDistance = (CenterY - 10.6065f) / ModelRadius;

        public static void Prepare() => _ = Mask;

        public static bool IsMetal(float x, float y)
        {
            var maskX = (int)MathF.Floor((x * ModelRadius + CenterX) * MaskScale);
            var maskY = (int)MathF.Floor((y * ModelRadius + CenterY) * MaskScale);
            return maskX >= 0 && maskX < MaskWidth && maskY >= 0 && maskY < MaskHeight &&
                Mask[maskY * MaskWidth + maskX];
        }

        private static bool[] CreateMask()
        {
            // Contours from the supplied SVG: the outer silhouette and its six cutouts.
            Vector2[][] contours =
            [
                new Contour(66.777f, 26.27f)
                    .Curve(58.834f, 23.83f, 50.342f, 23.83f, 42.399f, 26.27f)
                    .Line(54.588f, 53.647f).Close(),
                new Contour(48.558f, 64.69f)
                    .Line(60.618f, 64.69f).Line(75.912f, 30.338f)
                    .Curve(80.739f, 33.229f, 84.928f, 37.071f, 88.223f, 41.632f)
                    .Line(68.731f, 85.413f).Line(60.618f, 67.19f).Line(48.558f, 67.19f)
                    .Line(40.445f, 85.413f).Line(20.952f, 41.632f)
                    .Curve(24.248f, 37.071f, 28.437f, 33.229f, 33.264f, 30.338f).Close(),
                new Contour(15.119f, 53.116f)
                    .Line(37.708f, 103.852f)
                    .Curve(18.304f, 95.213f, 8.555f, 73.317f, 15.119f, 53.116f).Close(),
                new Contour(71.467f, 103.852f)
                    .Line(94.057f, 53.116f)
                    .Curve(100.62f, 73.317f, 90.872f, 95.213f, 71.467f, 103.852f).Close(),
                new Contour(42.399f, 105.61f)
                    .Curve(50.342f, 108.05f, 58.834f, 108.05f, 66.777f, 105.61f)
                    .Line(54.588f, 78.233f).Close(),
                new Contour(44.652f, 16.937f)
                    .Curve(19.483f, 22.04f, 2.273f, 45.381f, 4.838f, 70.933f)
                    .Curve(7.402f, 96.485f, 28.907f, 115.94f, 54.588f, 115.94f)
                    .Curve(80.269f, 115.94f, 101.774f, 96.485f, 104.338f, 70.933f)
                    .Curve(106.902f, 45.381f, 89.692f, 22.04f, 64.524f, 16.937f)
                    .Curve(63.591f, 16.748f, 62.921f, 15.928f, 62.921f, 14.977f)
                    .Line(62.921f, 10.607f)
                    .Curve(62.921f, 6.004f, 59.19f, 2.273f, 54.588f, 2.273f)
                    .Curve(49.985f, 2.273f, 46.255f, 6.004f, 46.255f, 10.607f)
                    .Line(46.255f, 14.977f)
                    .Curve(46.255f, 15.928f, 45.584f, 16.748f, 44.652f, 16.937f).Close(),
                new Contour(57.921f, 10.607f)
                    .Curve(57.921f, 8.766f, 56.429f, 7.273f, 54.588f, 7.273f)
                    .Curve(52.747f, 7.273f, 51.255f, 8.766f, 51.255f, 10.607f)
                    .Curve(51.255f, 12.448f, 52.747f, 13.94f, 54.588f, 13.94f)
                    .Curve(56.429f, 13.94f, 57.921f, 12.448f, 57.921f, 10.607f).Close()
            ];

            var mask = new bool[MaskWidth * MaskHeight];
            for (var y = 0; y < MaskHeight; y++)
            {
                for (var x = 0; x < MaskWidth; x++)
                {
                    var point = new Vector2((x + 0.5f) / MaskScale, (y + 0.5f) / MaskScale);
                    var inside = false;
                    foreach (var contour in contours)
                    {
                        if (Contains(contour, point))
                            inside = !inside;
                    }
                    mask[y * MaskWidth + x] = inside;
                }
            }
            return mask;
        }

        private static bool Contains(Vector2[] contour, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = contour.Length - 1; i < contour.Length; j = i++)
            {
                var a = contour[i];
                var b = contour[j];
                if ((a.Y > point.Y) != (b.Y > point.Y) &&
                    point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }

        private sealed class Contour
        {
            private readonly List<Vector2> _points;

            public Contour(float x, float y) => _points = [new Vector2(x, y)];

            public Contour Line(float x, float y)
            {
                _points.Add(new Vector2(x, y));
                return this;
            }

            public Contour Curve(float x1, float y1, float x2, float y2, float x3, float y3)
            {
                var start = _points[^1];
                var control1 = new Vector2(x1, y1);
                var control2 = new Vector2(x2, y2);
                var end = new Vector2(x3, y3);
                for (var i = 1; i <= 12; i++)
                {
                    var t = i / 12f;
                    var s = 1 - t;
                    _points.Add(s * s * s * start + 3 * s * s * t * control1 +
                        3 * s * t * t * control2 + t * t * t * end);
                }
                return this;
            }

            public Vector2[] Close() => _points.ToArray();
        }
    }
}
