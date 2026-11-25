using RPiRgbLEDMatrix;
using WearWare.Services.Matrix;
using WearWare.Services.Physics.Imu;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.Physics.Simulations.AcidSmiley
{
    public sealed class AcidSmileyRenderer : PhysicsRenderer
    {
        private static readonly Color FaceColor = new(255, 220, 0);
        private static readonly Color Black = new(0, 0, 0);
        private static readonly Color TongueColor = new(255, 35, 55);
        private static readonly Color TongueGrooveColor = new(170, 15, 35);
        private const double ReferenceRadius = 56.85291170560415;
        private static readonly Point[] MouthControlPoints =
        [
            new(0, -22.28032013741425),
            new(0.5985040195939257, -22.28032013741425),
            new(1.842413595554615, -22.26429145157641),
            new(3.848981818201683, -22.19216347482328),
            new(6.792322182755246, -21.97956652925373),
            new(10.82981039017698, -21.41104558592548),
            new(15.09536121990046, -20.41528989825641),
            new(19.42863478261973, -18.89528663567038),
            new(23.65096514590162, -16.82312449398408),
            new(27.57667108381664, -14.28675569981604),
            new(31.02521567677473, -11.5254785824527),
            new(33.84273991019501, -8.874647596184043),
            new(35.9217135973713, -6.718850002966593),
            new(37.22473812943423, -5.44503574154254),
            new(37.80138745560452, -5.389894393682174),
            new(37.82401501577345, -6.802576299862879),
            new(37.35930077980281, -9.661694572042442),
            new(36.35010488114705, -13.69331393040688),
            new(34.6874179761248, -18.47398421886917),
            new(32.25159550547628, -23.50265096368547),
            new(28.96909946731926, -28.28722155491521),
            new(24.8570254822393, -32.4171063021475),
            new(20.08935828365794, -35.66054299849288),
            new(14.99986942719173, -37.98695760467193),
            new(10.00304633578591, -39.49230312546565),
            new(5.536522079822939, -40.35030790985241),
            new(2.703480077487104, -40.67373186922657),
            new(1.125325763270984, -40.78425856261437),
            new(0.3319815203877852, -40.80881890129521),
            new(0, -40.80881890129521)
        ];
        private static readonly Point[] MouthOutline = CreateMouthOutline();
        private const double MaxTiltRadians = 70 * Math.PI / 180;
        private double _elapsedSeconds;
        private double _basePosition;
        private double _baseAngle;
        private double _tipAngle;
        private bool _motionInitialized;

        public AcidSmileyRenderer(
            ImuService imu,
            IMatrixDisplay display,
            MatrixConfigService config,
            ILogger<AcidSmileyRenderer> logger)
            : base(imu, display, config, logger)
        {
        }

        protected override void Reset()
        {
            _elapsedSeconds = 0;
            _basePosition = 0.5;
            _baseAngle = 0;
            _tipAngle = 0;
            _motionInitialized = false;
        }

        protected override void DrawFrame(RGBLedCanvas canvas, ImuReading reading, double elapsedSeconds)
        {
            var radius = Math.Min(canvas.Width, canvas.Height) * (ReferenceRadius / 128);
            if (radius < 1)
                return;

            var centerX = (canvas.Width - 1) / 2.0;
            var centerY = (canvas.Height - 1) / 2.0;
            UpdateMotion(reading, elapsedSeconds);

            FillEllipse(canvas, centerX, centerY, radius, radius, FaceColor);

            var eyeRadiusX = radius * (4.960674857154 / ReferenceRadius);
            var eyeRadiusY = radius * (14.38153091568093 / ReferenceRadius);
            var eyeY = centerY - radius * (17.99319034459099 / ReferenceRadius);
            var eyeOffset = radius * (14.84340816236941 / ReferenceRadius);
            FillEllipse(canvas, centerX - eyeOffset, eyeY, eyeRadiusX, eyeRadiusY, Black);
            FillEllipse(canvas, centerX + eyeOffset, eyeY, eyeRadiusX, eyeRadiusY, Black);

            DrawMouth(canvas, centerX, centerY, radius);
            DrawTongue(canvas, centerX, centerY, radius);
        }

        private void UpdateMotion(ImuReading reading, double elapsedSeconds)
        {
            var dt = Math.Clamp(elapsedSeconds, 0, 0.1);
            _elapsedSeconds += dt;

            // The accelerometer points opposite gravity. Map its mounted X/Y axes to screen coordinates.
            var gravityX = -reading.MountedAcceleration.X;
            var gravityY = reading.MountedAcceleration.Y;
            var targetAngle = 0.0;
            if (float.IsFinite((float)gravityX) && float.IsFinite((float)gravityY))
            {
                targetAngle = Math.Clamp(
                    Math.Atan2(gravityX, Math.Max(Math.Abs(gravityY), 1.0)),
                    -MaxTiltRadians,
                    MaxTiltRadians);
            }

            var targetBasePosition = 0.5 + Math.Sin(targetAngle) * 0.22;
            if (!_motionInitialized)
            {
                _basePosition = targetBasePosition;
                _baseAngle = targetAngle * 0.35;
                _tipAngle = targetAngle;
                _motionInitialized = true;
                return;
            }

            _basePosition += (targetBasePosition - _basePosition) * SmoothingWeight(dt, 0.45);
            _baseAngle += (targetAngle * 0.35 - _baseAngle) * SmoothingWeight(dt, 0.5);
            _tipAngle += (targetAngle - _tipAngle) * SmoothingWeight(dt, 0.12);
        }

        private static double SmoothingWeight(double elapsedSeconds, double timeConstant)
        {
            return 1 - Math.Exp(-elapsedSeconds / timeConstant);
        }

        private static void DrawMouth(RGBLedCanvas canvas, double centerX, double centerY, double radius)
        {
            var mouth = new Point[MouthOutline.Length];
            for (var i = 0; i < mouth.Length; i++)
            {
                mouth[i] = new Point(
                    centerX + MouthOutline[i].X * radius,
                    centerY + MouthOutline[i].Y * radius);
            }

            FillPolygon(canvas, mouth, Black);
            var cornerX = radius * (37.36421678566272 / ReferenceRadius);
            var cornerY = centerY + radius * (5.230682114258513 / ReferenceRadius);
            DrawMouthCorner(canvas, centerX - cornerX, cornerY, radius, -1);
            DrawMouthCorner(canvas, centerX + cornerX, cornerY, radius, 1);
        }

        private static void DrawMouthCorner(RGBLedCanvas canvas, double centerX, double centerY, double radius, int side)
        {
            var majorX = side * 2.662139290359387 / ReferenceRadius * radius;
            var majorY = 1.625135789877915 / ReferenceRadius * radius;
            var majorRadius = Math.Sqrt(majorX * majorX + majorY * majorY);
            var minorRadius = majorRadius * 0.308229434072314;
            var minX = Math.Max(0, (int)Math.Floor(centerX - majorRadius));
            var maxX = Math.Min(canvas.Width - 1, (int)Math.Ceiling(centerX + majorRadius));
            var minY = Math.Max(0, (int)Math.Floor(centerY - majorRadius));
            var maxY = Math.Min(canvas.Height - 1, (int)Math.Ceiling(centerY + majorRadius));

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    var along = (dx * majorX + dy * majorY) / (majorRadius * majorRadius);
                    var across = (-dx * majorY + dy * majorX) / (majorRadius * minorRadius);
                    if (along * along + across * across <= 1)
                        canvas.SetPixel(x, y, Black);
                }
            }
        }

        private void DrawTongue(RGBLedCanvas canvas, double centerX, double centerY, double radius)
        {
            var mouthPoint = SmilePoint(centerX, centerY, radius, _basePosition);
            var start = mouthPoint;
            var length = radius * 0.31;
            var middleAngle = (_baseAngle + _tipAngle) / 2;
            var idleSway = Math.Sin(_elapsedSeconds * 2.1) * radius * 0.045;
            var tipAngle = _tipAngle + Math.Sin(_elapsedSeconds * 1.7) * 0.055;
            var middle = new Point(
                start.X + Math.Sin(middleAngle) * length * 0.5 + idleSway * 0.4,
                start.Y + Math.Cos(middleAngle) * length * 0.5);
            var tip = new Point(
                start.X + Math.Sin(tipAngle) * length + idleSway,
                start.Y + Math.Cos(tipAngle) * length);

            var baseRadius = radius * 0.21;
            var middleRadius = radius * 0.17;
            var tipRadius = radius * 0.12;
            var control = GetPathControl(start, middle, tip);
            var baseDirection = GetPathDirection(start, control, tip, 0);
            var baseNormal = new Point(baseDirection.Y, -baseDirection.X);
            const double attachmentPosition = 0.9;
            var attachmentDepth = TongueRootDepth(attachmentPosition, radius);
            var rightEdge = new Point(
                start.X + baseNormal.X * baseRadius * attachmentPosition + baseDirection.X * attachmentDepth,
                start.Y + baseNormal.Y * baseRadius * attachmentPosition + baseDirection.Y * attachmentDepth);
            var shift = centerY + MouthBounds((rightEdge.X - centerX) / radius).Bottom * radius - rightEdge.Y;
            start = new Point(start.X, start.Y + shift);
            middle = new Point(middle.X, middle.Y + shift);
            tip = new Point(tip.X, tip.Y + shift);

            control = GetPathControl(start, middle, tip);
            var grooveStart = new Point(
                start.X + baseDirection.X * radius * 0.005,
                start.Y + baseDirection.Y * radius * 0.005);
            var grooveEnd = new Point(
                tip.X + (middle.X - tip.X) * 0.25,
                tip.Y + (middle.Y - tip.Y) * 0.25);
            var grooveRadius = Math.Max(0.5, radius * 0.025);
            DrawTongueShape(canvas, start, control, tip, baseDirection, baseNormal,
                baseRadius, middleRadius, tipRadius, radius, grooveStart, middle, grooveEnd, grooveRadius);
        }

        private static void DrawTongueShape(
            RGBLedCanvas canvas,
            Point start,
            Point control,
            Point tip,
            Point baseDirection,
            Point baseNormal,
            double baseRadius,
            double middleRadius,
            double tipRadius,
            double faceRadius,
            Point grooveStart,
            Point grooveMiddle,
            Point grooveEnd,
            double grooveRadius)
        {
            const int steps = 32;
            var samples = new TongueSample[steps + 1];
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (double)steps;
                samples[i] = new TongueSample(GetPathPoint(start, control, tip, t),
                    GetTongueWidth(t, baseRadius, middleRadius, tipRadius));
            }

            var minX = Math.Max(0, (int)Math.Floor(Math.Min(Math.Min(start.X, control.X), tip.X) - baseRadius));
            var maxX = Math.Min(canvas.Width - 1, (int)Math.Ceiling(Math.Max(Math.Max(start.X, control.X), tip.X) + baseRadius));
            var minY = Math.Max(0, (int)Math.Floor(Math.Min(Math.Min(start.Y, control.Y), tip.Y) - baseRadius));
            var maxY = Math.Min(canvas.Height - 1, (int)Math.Ceiling(Math.Max(Math.Max(start.Y, control.Y), tip.Y) + baseRadius));
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var point = new Point(x, y);
                    if (!TongueContains(point, start, baseDirection, baseNormal, baseRadius, faceRadius, samples))
                        continue;

                    var inGroove = DistanceToSegmentSquared(point, grooveStart, grooveMiddle) <= grooveRadius * grooveRadius ||
                        DistanceToSegmentSquared(point, grooveMiddle, grooveEnd) <= grooveRadius * grooveRadius;
                    canvas.SetPixel(x, y, inGroove ? TongueGrooveColor : TongueColor);
                }
            }
        }

        private static bool TongueContains(
            Point point,
            Point start,
            Point baseDirection,
            Point baseNormal,
            double baseRadius,
            double faceRadius,
            TongueSample[] samples)
        {
            var dx = point.X - start.X;
            var dy = point.Y - start.Y;
            var across = (dx * baseNormal.X + dy * baseNormal.Y) / baseRadius;
            var along = dx * baseDirection.X + dy * baseDirection.Y;
            if (along < TongueRootDepth(Math.Clamp(across, -1, 1), faceRadius))
                return false;

            foreach (var sample in samples)
            {
                var sampleX = point.X - sample.Center.X;
                var sampleY = point.Y - sample.Center.Y;
                if (sampleX * sampleX + sampleY * sampleY <= sample.Radius * sample.Radius)
                    return true;
            }

            return false;
        }

        private static double TongueRootDepth(double across, double faceRadius)
        {
            var lobe = -faceRadius * 0.05 * Math.Pow(Math.Sin(Math.PI * across), 2);
            var notch = -faceRadius * 0.022 * Math.Exp(-Math.Pow(across / 0.3, 2));
            var corner = Math.Clamp((Math.Abs(across) - 0.75) / 0.25, 0, 1);
            var rounding = faceRadius * 0.04 * corner * corner * (3 - 2 * corner);
            return lobe + notch + rounding;
        }

        private static double DistanceToSegmentSquared(Point point, Point start, Point end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var lengthSquared = dx * dx + dy * dy;
            var position = lengthSquared == 0 ? 0 :
                Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0, 1);
            var distanceX = point.X - (start.X + position * dx);
            var distanceY = point.Y - (start.Y + position * dy);
            return distanceX * distanceX + distanceY * distanceY;
        }

        private static Point GetPathControl(Point start, Point middle, Point tip)
        {
            return new Point(
                2 * middle.X - (start.X + tip.X) / 2,
                2 * middle.Y - (start.Y + tip.Y) / 2);
        }

        private static Point GetPathPoint(Point start, Point control, Point tip, double t)
        {
            var inverseT = 1 - t;
            return new Point(
                inverseT * inverseT * start.X + 2 * inverseT * t * control.X + t * t * tip.X,
                inverseT * inverseT * start.Y + 2 * inverseT * t * control.Y + t * t * tip.Y);
        }

        private static Point GetPathDirection(Point start, Point control, Point tip, double t)
        {
            var x = 2 * ((1 - t) * (control.X - start.X) + t * (tip.X - control.X));
            var y = 2 * ((1 - t) * (control.Y - start.Y) + t * (tip.Y - control.Y));
            var length = Math.Sqrt(x * x + y * y);
            return length > 0 ? new Point(x / length, y / length) : new Point(0, 1);
        }

        private static double GetTongueWidth(double t, double baseRadius, double middleRadius, double tipRadius)
        {
            if (t <= 0.5)
            {
                var position = t * 2;
                return baseRadius + (middleRadius - baseRadius) * position;
            }

            var secondPosition = (t - 0.5) * 2;
            return middleRadius + (tipRadius - middleRadius) * secondPosition;
        }

        private static void FillPolygon(RGBLedCanvas canvas, Point[] points, Color color)
        {
            var minX = Math.Max(0, (int)Math.Floor(points.Min(point => point.X)));
            var maxX = Math.Min(canvas.Width - 1, (int)Math.Ceiling(points.Max(point => point.X)));
            var minY = Math.Max(0, (int)Math.Floor(points.Min(point => point.Y)));
            var maxY = Math.Min(canvas.Height - 1, (int)Math.Ceiling(points.Max(point => point.Y)));

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (ContainsPoint(points, x, y))
                        canvas.SetPixel(x, y, color);
                }
            }
        }

        private static bool ContainsPoint(Point[] polygon, double x, double y)
        {
            var inside = false;
            for (var i = 0; i < polygon.Length; i++)
            {
                var current = polygon[i];
                var previous = polygon[(i + polygon.Length - 1) % polygon.Length];
                if ((current.Y > y) != (previous.Y > y) &&
                    x < (previous.X - current.X) * (y - current.Y) / (previous.Y - current.Y) + current.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static Point[] CreateMouthOutline()
        {
            const int steps = 64;
            var outline = new Point[steps * 2 + 2];
            for (var i = 0; i <= steps; i++)
            {
                var point = EvaluateMouthSpline(i / (double)steps);
                outline[i] = new Point(point.X / ReferenceRadius, -point.Y / ReferenceRadius);
                outline[outline.Length - 1 - i] = new Point(-point.X / ReferenceRadius, -point.Y / ReferenceRadius);
            }

            return outline;
        }

        private static Point EvaluateMouthSpline(double position)
        {
            const int degree = 5;
            var span = position == 1 ? 29 : degree + (int)(position * 25);
            var points = new Point[degree + 1];
            for (var j = 0; j <= degree; j++)
                points[j] = MouthControlPoints[span - degree + j];

            // The DXF spline has uniformly spaced interior knots and clamped endpoints.
            for (var level = 1; level <= degree; level++)
            {
                for (var j = degree; j >= level; j--)
                {
                    var left = MouthKnot(span - degree + j);
                    var right = MouthKnot(span + 1 + j - level);
                    var weight = (position - left) / (right - left);
                    points[j] = new Point(
                        (1 - weight) * points[j - 1].X + weight * points[j].X,
                        (1 - weight) * points[j - 1].Y + weight * points[j].Y);
                }
            }

            return points[degree];
        }

        private static double MouthKnot(int index)
        {
            if (index <= 5) return 0;
            if (index >= 30) return 1;
            return (index - 5) / 25.0;
        }

        private static Point SmilePoint(double centerX, double centerY, double radius, double position)
        {
            var x = (position - 0.5) * 1.28;
            var bounds = MouthBounds(x);
            return new Point(centerX + x * radius, centerY + (bounds.Top + bounds.Bottom) * radius / 2);
        }

        private static (double Top, double Bottom) MouthBounds(double x)
        {
            var top = double.PositiveInfinity;
            var bottom = double.NegativeInfinity;
            for (var i = 0; i < MouthOutline.Length; i++)
            {
                var a = MouthOutline[i];
                var b = MouthOutline[(i + 1) % MouthOutline.Length];
                if ((a.X <= x && b.X > x) || (b.X <= x && a.X > x))
                {
                    var y = a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }

            if (!double.IsFinite(top) || !double.IsFinite(bottom))
                throw new InvalidOperationException("Tongue position is outside the smile.");

            return (top, bottom);
        }

        private static void FillEllipse(
            RGBLedCanvas canvas,
            double centerX,
            double centerY,
            double radiusX,
            double radiusY,
            Color color)
        {
            var minX = Math.Max(0, (int)Math.Floor(centerX - radiusX));
            var maxX = Math.Min(canvas.Width - 1, (int)Math.Ceiling(centerX + radiusX));
            var minY = Math.Max(0, (int)Math.Floor(centerY - radiusY));
            var maxY = Math.Min(canvas.Height - 1, (int)Math.Ceiling(centerY + radiusY));

            for (var y = minY; y <= maxY; y++)
            {
                var normalizedY = (y - centerY) / radiusY;
                for (var x = minX; x <= maxX; x++)
                {
                    var normalizedX = (x - centerX) / radiusX;
                    if (normalizedX * normalizedX + normalizedY * normalizedY <= 1)
                        canvas.SetPixel(x, y, color);
                }
            }
        }

        private static void DrawThickSegment(
            RGBLedCanvas canvas,
            Point start,
            Point end,
            double startRadius,
            double endRadius,
            Color color)
        {
            var maxRadius = Math.Max(startRadius, endRadius);
            var minX = Math.Max(0, (int)Math.Floor(Math.Min(start.X, end.X) - maxRadius));
            var maxX = Math.Min(canvas.Width - 1, (int)Math.Ceiling(Math.Max(start.X, end.X) + maxRadius));
            var minY = Math.Max(0, (int)Math.Floor(Math.Min(start.Y, end.Y) - maxRadius));
            var maxY = Math.Min(canvas.Height - 1, (int)Math.Ceiling(Math.Max(start.Y, end.Y) + maxRadius));
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var lengthSquared = deltaX * deltaX + deltaY * deltaY;

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var position = lengthSquared == 0
                        ? 0
                        : Math.Clamp(((x - start.X) * deltaX + (y - start.Y) * deltaY) / lengthSquared, 0, 1);
                    var nearestX = start.X + position * deltaX;
                    var nearestY = start.Y + position * deltaY;
                    var distanceX = x - nearestX;
                    var distanceY = y - nearestY;
                    var strokeRadius = startRadius + position * (endRadius - startRadius);
                    if (distanceX * distanceX + distanceY * distanceY <= strokeRadius * strokeRadius)
                        canvas.SetPixel(x, y, color);
                }
            }
        }

        private readonly record struct Point(double X, double Y);
        private readonly record struct TongueSample(Point Center, double Radius);
    }
}
