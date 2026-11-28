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
        private static readonly Color TongueGrooveColor = new(220, 27, 47);
        private static readonly Color TongueHighlightColor = new(255, 70, 80);
        private const double ReferenceRadius = 56.5;
        private const double UpperMouthRadius = 45;
        private const double UpperMouthCenterY = -18;
        private const double LowerMouthRadius = 38.29090294217289;
        private const double LowerMouthCenterY = 2.709097057827122;
        private const double MouthHalfWidth = 38.16216432703916 / ReferenceRadius;
        private const double MaxTiltRadians = 70 * Math.PI / 180;
        private const double TongueSlideGain = 0.466297348428705;
        private const double TongueTiltSensitivity = 1.5;
        private double _elapsedSeconds;
        private double _basePosition;
        private double _baseAngle;
        private double _tipAngle;
        private double _faceRotation;
        private double _gravityX;
        private double _gravityY;
        private double _offsetX;
        private double _offsetY;
        private double _blinkTime;
        private double _blinkStartTime;
        private double _nextBlinkTime;
        private bool _motionInitialized;
        private bool _gravityInitialized;
        private bool _doubleBlink;
        private Color[] _pixels = [];

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
            _faceRotation = 0;
            _gravityX = 0;
            _gravityY = 0;
            _offsetX = 0;
            _offsetY = 0;
            _blinkTime = 0;
            _blinkStartTime = -1;
            _nextBlinkTime = 5 + Random.Shared.NextDouble() * 5;
            _motionInitialized = false;
            _gravityInitialized = false;
            _doubleBlink = false;
            _pixels = [];
        }

        protected override void DrawFrame(RGBLedCanvas canvas, ImuReading reading, double elapsedSeconds)
        {
            var radius = Math.Min(canvas.Width, canvas.Height) * (ReferenceRadius / 128) * 0.9;
            if (radius < 1)
                return;

            var pixelCount = checked(canvas.Width * canvas.Height);
            if (_pixels.Length != pixelCount)
                _pixels = new Color[pixelCount];

            UpdateMotion(reading, elapsedSeconds);
            UpdateBlink(elapsedSeconds);
            var centerX = (canvas.Width - 1) / 2.0 + _offsetX * radius;
            var centerY = (canvas.Height - 1) / 2.0 + _offsetY * radius;

            DrawSmiley(_pixels, canvas.Width, canvas.Height, centerX, centerY, radius, _faceRotation);
            canvas.SetPixels(0, 0, canvas.Width, canvas.Height, _pixels);
        }

        private void UpdateMotion(ImuReading reading, double elapsedSeconds)
        {
            var dt = Math.Clamp(elapsedSeconds, 0, 0.1);
            _elapsedSeconds += dt;

            // The accelerometer points opposite gravity. Map its mounted X/Y axes to screen coordinates.
            var gravityX = -reading.MountedAcceleration.X;
            var gravityY = reading.MountedAcceleration.Y;
            var targetAngle = 0.0;
            var targetOffsetX = 0.0;
            var targetOffsetY = 0.0;
            var targetFaceRotation = 0.0;
            var translationSlide = 0.0;
            if (float.IsFinite((float)gravityX) && float.IsFinite((float)gravityY))
            {
                targetAngle = Math.Clamp(
                    Math.Atan2(gravityX, Math.Max(Math.Abs(gravityY), 1.0)),
                    -MaxTiltRadians,
                    MaxTiltRadians);

                if (!_gravityInitialized)
                {
                    _gravityX = gravityX;
                    _gravityY = gravityY;
                    _gravityInitialized = true;
                }

                // A slow baseline removes sustained gravity without integrating position drift.
                var gravityWeight = SmoothingWeight(dt, 0.6);
                _gravityX += (gravityX - _gravityX) * gravityWeight;
                _gravityY += (gravityY - _gravityY) * gravityWeight;
                targetOffsetX = Math.Clamp((gravityX - _gravityX) * 0.0125, -0.05, 0.05);
                targetOffsetY = Math.Clamp((gravityY - _gravityY) * 0.0125, -0.05, 0.05);
                translationSlide = Math.Clamp((gravityX - _gravityX) * 0.08, -0.25, 0.25);
                var restingAngle = Math.Clamp(
                    Math.Atan2(_gravityX, Math.Max(Math.Abs(_gravityY), 1.0)),
                    -MaxTiltRadians,
                    MaxTiltRadians);
                targetFaceRotation = -Math.Clamp(targetAngle - restingAngle, -MaxTiltRadians, MaxTiltRadians) * 0.12;
            }

            _offsetX += (targetOffsetX - _offsetX) * SmoothingWeight(dt, 0.16);
            _offsetY += (targetOffsetY - _offsetY) * SmoothingWeight(dt, 0.16);
            targetFaceRotation += Math.Sin(_elapsedSeconds * 1.3) * 0.012;
            _faceRotation += (targetFaceRotation - _faceRotation) * SmoothingWeight(dt, 0.3);
            var localAngle = Math.Clamp(targetAngle + _faceRotation, -MaxTiltRadians, MaxTiltRadians);
            var tongueAngle = Math.Clamp(localAngle * TongueTiltSensitivity, -MaxTiltRadians, MaxTiltRadians);
            var maxBaseTravel = Math.Sin(MaxTiltRadians) * TongueSlideGain;
            var targetBasePosition = 0.5 + Math.Clamp(
                Math.Sin(tongueAngle) * TongueSlideGain + translationSlide, -maxBaseTravel, maxBaseTravel);
            if (!_motionInitialized)
            {
                _basePosition = targetBasePosition;
                _baseAngle = tongueAngle * 0.35;
                _tipAngle = tongueAngle;
                _motionInitialized = true;
                return;
            }

            _basePosition += (targetBasePosition - _basePosition) * SmoothingWeight(dt, 0.45);
            _baseAngle += (tongueAngle * 0.35 - _baseAngle) * SmoothingWeight(dt, 0.5);
            _tipAngle += (tongueAngle - _tipAngle) * SmoothingWeight(dt, 0.12);
        }

        private static double SmoothingWeight(double elapsedSeconds, double timeConstant)
        {
            return 1 - Math.Exp(-elapsedSeconds / timeConstant);
        }

        private void UpdateBlink(double elapsedSeconds)
        {
            _blinkTime += elapsedSeconds;
            if (_blinkTime >= _nextBlinkTime)
            {
                _blinkStartTime = _blinkTime;
                _doubleBlink = Random.Shared.Next(4) == 0;
                _nextBlinkTime = _blinkTime + 5 + Random.Shared.NextDouble() * 5;
            }
        }

        private static double EyeOpenness(double elapsedSeconds, bool doubleBlink)
        {
            if (doubleBlink && elapsedSeconds >= 0.42)
                elapsedSeconds -= 0.42;
            if (elapsedSeconds < 0 || elapsedSeconds >= 0.3)
                return 1;
            if (elapsedSeconds < 0.1)
                return 1 - elapsedSeconds / 0.1;
            if (elapsedSeconds < 0.15)
                return 0;
            return (elapsedSeconds - 0.15) / 0.15;
        }

        private void DrawSmiley(Color[] pixels, int width, int height, double centerX, double centerY, double radius, double rotation)
        {
            var arcAngle = Math.Asin((_basePosition - 0.5) * 1.28 * ReferenceRadius / LowerMouthRadius);
            var arcRadius = radius * LowerMouthRadius / ReferenceRadius * 0.9;
            var start = new Point(
                Math.Sin(arcAngle) * arcRadius,
                radius * LowerMouthCenterY / ReferenceRadius + Math.Cos(arcAngle) * arcRadius);
            // Keep the wider rounded tip within the same overall reach.
            var length = radius * 0.2716;
            var middleAngle = (_baseAngle + _tipAngle) / 2;
            var idleSway = Math.Sin(_elapsedSeconds * 2.1) * radius * 0.045;
            var tipAngle = _tipAngle + Math.Sin(_elapsedSeconds * 1.7) * 0.055;
            var middle = new Point(
                start.X + Math.Sin(middleAngle) * length * 0.5 + idleSway * 0.4,
                start.Y + Math.Cos(middleAngle) * length * 0.5);
            var tip = new Point(
                start.X + Math.Sin(tipAngle) * length + idleSway,
                start.Y + Math.Cos(tipAngle) * length);
            // Reduce curvature without changing the root-to-tip displacement.
            middle = new Point(
                (start.X + tip.X) * 0.1 + middle.X * 0.8,
                (start.Y + tip.Y) * 0.1 + middle.Y * 0.8);

            var baseRadius = radius * 0.231;
            var middleRadius = radius * 0.1955;
            var tipRadius = radius * 0.18;
            var control = GetPathControl(start, middle, tip);
            var restingTip = new Point(
                start.X + Math.Sin(_tipAngle) * length,
                start.Y + Math.Cos(_tipAngle) * length);
            var restingMiddle = new Point(
                (start.X + restingTip.X) * 0.1 + (start.X + Math.Sin(middleAngle) * length * 0.5) * 0.8,
                (start.Y + restingTip.Y) * 0.1 + (start.Y + Math.Cos(middleAngle) * length * 0.5) * 0.8);
            var restingControl = GetPathControl(start, restingMiddle, restingTip);
            // Dampen the idle bend at the root without reducing the tip's sideways sway.
            control = new Point((control.X + restingControl.X) * 0.5, (control.Y + restingControl.Y) * 0.5);
            middle = GetPathPoint(start, control, tip, 0.5);
            var baseDirection = GetPathDirection(start, control, tip, 0);
            var baseNormal = new Point(baseDirection.Y, -baseDirection.X);
            var grooveStart = new Point(
                start.X + baseDirection.X * radius * 0.005,
                start.Y + baseDirection.Y * radius * 0.005);
            var grooveMiddle = GetPathPoint(start, control, tip, 0.325);
            var grooveEnd = GetPathPoint(start, control, tip, 0.65);
            var grooveRadius = Math.Max(0.5, radius * 0.025);
            DrawSmileyPixels(pixels, width, height, centerX, centerY, rotation, start, control, tip, baseDirection, baseNormal,
                baseRadius, middleRadius, tipRadius, radius, grooveStart, grooveMiddle, grooveEnd, grooveRadius,
                EyeOpenness(_blinkTime - _blinkStartTime, _doubleBlink));
        }

        private static void DrawSmileyPixels(
            Color[] pixels,
            int width,
            int height,
            double centerX,
            double centerY,
            double rotation,
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
            double grooveRadius,
            double eyeOpenness)
        {
            const int steps = 32;
            var samples = new TongueSample[steps + 1];
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (double)steps;
                samples[i] = new TongueSample(GetPathPoint(start, control, tip, t),
                    GetTongueWidth(t, baseRadius, middleRadius, tipRadius));
            }

            const int highlightSteps = 8;
            Span<Point> highlight = stackalloc Point[highlightSteps + 1];
            var tipDirection = GetPathDirection(start, control, tip, 1);
            var tipNormal = new Point(tipDirection.Y, -tipDirection.X);
            for (var i = 0; i <= highlightSteps; i++)
            {
                var across = i * 2.0 / highlightSteps - 1;
                var along = -0.15 + 0.65 * (1 - across * across);
                highlight[i] = new Point(
                    tip.X + tipNormal.X * tipRadius * 0.65 * across + tipDirection.X * tipRadius * along,
                    tip.Y + tipNormal.Y * tipRadius * 0.65 * across + tipDirection.Y * tipRadius * along);
            }
            var highlightRadius = Math.Max(1.5, faceRadius * 0.025);

            var minX = Math.Min(Math.Min(start.X, control.X), tip.X) - baseRadius;
            var maxX = Math.Max(Math.Max(start.X, control.X), tip.X) + baseRadius;
            var minY = Math.Min(Math.Min(start.Y, control.Y), tip.Y) - baseRadius;
            var maxY = Math.Max(Math.Max(start.Y, control.Y), tip.Y) + baseRadius;
            var cos = Math.Cos(rotation);
            var sin = Math.Sin(rotation);
            var eyeX = faceRadius * (14.84340816236941 / ReferenceRadius);
            var eyeShift = tip.X * 0.12;
            var eyeY = -faceRadius * (17.99319034459099 / ReferenceRadius);
            var eyeRadiusX = faceRadius * (4.960674857154 / ReferenceRadius);
            var eyeRadiusY = faceRadius * (14.38153091568093 / ReferenceRadius);
            eyeRadiusY = Math.Max(Math.Min(0.5, eyeRadiusY), eyeRadiusY * eyeOpenness);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    var point = new Point(cos * dx + sin * dy, -sin * dx + cos * dy);
                    var color = Black;
                    if (point.X >= minX && point.X <= maxX && point.Y >= minY && point.Y <= maxY &&
                        TongueContains(point, start, baseDirection, baseNormal, baseRadius, faceRadius, samples))
                    {
                        var inGroove = DistanceToSegmentSquared(point, grooveStart, grooveMiddle) <= grooveRadius * grooveRadius ||
                            DistanceToSegmentSquared(point, grooveMiddle, grooveEnd) <= grooveRadius * grooveRadius;
                        var inHighlight = false;
                        for (var i = 0; i < highlightSteps && !inHighlight; i++)
                            inHighlight = DistanceToSegmentSquared(point, highlight[i], highlight[i + 1]) <= highlightRadius * highlightRadius;
                        color = inHighlight ? TongueHighlightColor : inGroove ? TongueGrooveColor : TongueColor;
                    }
                    else if (point.X * point.X + point.Y * point.Y <= faceRadius * faceRadius &&
                        !EllipseContains(point.X - eyeShift - eyeX, point.Y - eyeY, eyeRadiusX, eyeRadiusY) &&
                        !EllipseContains(point.X - eyeShift + eyeX, point.Y - eyeY, eyeRadiusX, eyeRadiusY) &&
                        !MouthContains(point, faceRadius))
                    {
                        color = FaceColor;
                    }
                    pixels[y * width + x] = color;
                }
            }
        }

        private static bool MouthContains(Point point, double radius)
        {
            var x = point.X / radius;
            if (Math.Abs(x) <= MouthHalfWidth)
            {
                var bounds = MouthBounds(x);
                if (point.Y >= bounds.Top * radius && point.Y <= bounds.Bottom * radius)
                    return true;
            }

            var cornerX = radius * (37.88638811911351 / ReferenceRadius);
            var cornerY = radius * (5.909734806123538 / ReferenceRadius);
            var majorX = radius * (3.03108891324545 / ReferenceRadius);
            var majorY = radius * (1.749999999999951 / ReferenceRadius);
            var majorRadius = Math.Sqrt(majorX * majorX + majorY * majorY);
            var minorRadius = majorRadius * 0.3571428571428671;
            var side = point.X < 0 ? -1 : 1;
            var dx = point.X - side * cornerX;
            var dy = point.Y - cornerY;
            if (Math.Abs(dx) > majorRadius || Math.Abs(dy) > majorRadius)
                return false;
            var along = (dx * side * majorX + dy * majorY) / majorRadius;
            var across = (-dx * majorY + dy * side * majorX) / majorRadius;
            return EllipseContains(along, across, majorRadius, minorRadius);
        }

        private static bool EllipseContains(double x, double y, double radiusX, double radiusY)
        {
            var normalizedX = x / radiusX;
            var normalizedY = y / radiusY;
            return normalizedX * normalizedX + normalizedY * normalizedY <= 1;
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

        private static (double Top, double Bottom) MouthBounds(double x)
        {
            if (!double.IsFinite(x) || Math.Abs(x) > MouthHalfWidth)
                throw new InvalidOperationException("Tongue position is outside the smile.");

            var referenceX = x * ReferenceRadius;
            var top = (UpperMouthCenterY +
                Math.Sqrt(UpperMouthRadius * UpperMouthRadius - referenceX * referenceX)) / ReferenceRadius;
            var bottom = (LowerMouthCenterY +
                Math.Sqrt(LowerMouthRadius * LowerMouthRadius - referenceX * referenceX)) / ReferenceRadius;
            return (top, bottom);
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
