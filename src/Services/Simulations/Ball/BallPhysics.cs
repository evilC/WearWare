using System.Numerics;

namespace WearWare.Services.Simulations.Ball
{
    internal sealed class BallPhysics
    {
        private const double PixelsPerMeter = 1000.0 / 1.85;
        private const double Restitution = 0.6;
        private const double SurfaceFriction = 0.05;
        private const double RestingSpeed = PixelsPerMeter * 0.2;
        private const double MaxStepSeconds = 1.0 / 120;
        private bool _initialized;
        private double _velocityX;
        private double _velocityY;

        public double X { get; private set; }
        public double Y { get; private set; }
        public int Radius { get; private set; }
        public int RampRadius { get; private set; }

        public void Update(Vector3 acceleration, double elapsedSeconds, int width, int height)
        {
            if (width <= 0 || height <= 0 || !float.IsFinite(acceleration.X) || !float.IsFinite(acceleration.Y)
                || !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(acceleration), "Invalid ball simulation input.");
            }

            Radius = Math.Min(8, (Math.Min(width, height) - 1) / 2);
            var rampRadius = Math.Min(32, (Math.Min(width, height) - 1) / 2);
            RampRadius = rampRadius > Radius ? rampRadius : 0;
            if (!_initialized)
            {
                X = (width - 1) / 2.0;
                Y = (height - 1) / 2.0;
                _initialized = true;
            }

            var dt = Math.Min(elapsedSeconds, 0.1);
            // Accelerometer readings include gravity. Sensor X points right and Y points up.
            var accelerationX = -acceleration.X * PixelsPerMeter;
            var accelerationY = acceleration.Y * PixelsPerMeter;
            var steps = Math.Max(1, (int)Math.Ceiling(dt / MaxStepSeconds));
            for (var i = 0; i < steps; i++)
            {
                Step(accelerationX, accelerationY, dt / steps, width, height);
            }
        }

        private void Step(double accelerationX, double accelerationY, double dt, int width, int height)
        {
            if (Y >= height - 1 - Radius && _velocityY == 0 && _velocityX == 0 &&
                accelerationY > 0 && Math.Abs(accelerationX) <= SurfaceFriction * accelerationY)
            {
                accelerationX = 0;
            }

            var x = X + _velocityX * dt + 0.5 * accelerationX * dt * dt;
            var y = Y + _velocityY * dt + 0.5 * accelerationY * dt * dt;
            _velocityX += accelerationX * dt;
            _velocityY += accelerationY * dt;
            if (RampRadius > 0)
            {
                var rampCenterY = height - 1 - RampRadius;
                BounceRamp(ref x, ref y, RampRadius, rampCenterY, true);
                BounceRamp(ref x, ref y, width - 1 - RampRadius, rampCenterY, false);
            }
            Bounce(ref x, Radius, width - 1 - Radius, true);
            Bounce(ref y, Radius, height - 1 - Radius, false);
            X = x;
            Y = y;
        }

        private void BounceRamp(ref double x, ref double y, double centerX, double centerY, bool left)
        {
            if (y <= centerY || (left ? x >= centerX : x <= centerX))
            {
                return;
            }

            var dx = x - centerX;
            var dy = y - centerY;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var limit = RampRadius - Radius;
            if (distance <= limit)
            {
                return;
            }

            var normalX = dx / distance;
            var normalY = dy / distance;
            x = centerX + normalX * limit;
            y = centerY + normalY * limit;
            ResolveContact(normalX, normalY);
        }

        private void Bounce(ref double position, double min, double max, bool horizontal)
        {
            if (position < min)
            {
                position = min;
                ResolveContact(horizontal ? -1 : 0, horizontal ? 0 : -1);
            }
            else if (position > max)
            {
                position = max;
                ResolveContact(horizontal ? 1 : 0, horizontal ? 0 : 1);
            }
        }

        private void ResolveContact(double normalX, double normalY)
        {
            var normalVelocity = _velocityX * normalX + _velocityY * normalY;
            if (normalVelocity <= 0) return;

            var restitution = normalVelocity < RestingSpeed ? 0 : Restitution;
            var impulse = (1 + restitution) * normalVelocity;
            _velocityX -= impulse * normalX;
            _velocityY -= impulse * normalY;

            var tangentX = -normalY;
            var tangentY = normalX;
            var tangentVelocity = _velocityX * tangentX + _velocityY * tangentY;
            var friction = Math.Min(Math.Abs(tangentVelocity), SurfaceFriction * impulse);
            _velocityX -= Math.Sign(tangentVelocity) * friction * tangentX;
            _velocityY -= Math.Sign(tangentVelocity) * friction * tangentY;
        }
    }
}
