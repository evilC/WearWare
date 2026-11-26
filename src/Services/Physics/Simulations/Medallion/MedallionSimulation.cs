using System.Numerics;

namespace WearWare.Services.Physics.Simulations.Medallion
{
    internal sealed class MedallionSimulation
    {
        private const int LinksPerSide = 10;
        private const int Knot = LinksPerSide;
        private const int RightAnchor = LinksPerSide * 2;
        private const int Center = RightAnchor + 1;
        private const int SolverIterations = 20;
        private const double MaxStepSeconds = 1.0 / 120;
        private readonly Vector2[] _positions = new Vector2[Center + 1];
        private readonly Vector2[] _previous = new Vector2[Center + 1];
        private readonly Vector2[] _velocities = new Vector2[Center + 1];
        private readonly float _linkLength;
        private readonly float _attachmentDistance;

        public float Radius { get; }
        public ReadOnlySpan<Vector2> Chain => _positions.AsSpan(0, RightAnchor + 1);
        public Vector2 MedallionCenter => _positions[Center];
        public float Angle
        {
            get
            {
                var direction = _positions[Center] - _positions[Knot];
                return MathF.Atan2(-direction.X, direction.Y);
            }
        }

        public MedallionSimulation(int width, int height, float radiusFraction, float attachmentDistance)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Invalid medallion canvas dimensions.");
            if (!float.IsFinite(radiusFraction) || radiusFraction <= 0)
                throw new ArgumentOutOfRangeException(nameof(radiusFraction), "Invalid medallion radius.");
            if (!float.IsFinite(attachmentDistance) || attachmentDistance <= 0)
                throw new ArgumentOutOfRangeException(nameof(attachmentDistance), "Invalid medallion attachment distance.");

            var scale = Math.Min(width, height);
            _attachmentDistance = attachmentDistance;
            Radius = Math.Max(0.5f, scale * radiusFraction);
            var left = new Vector2((width - 1) * 0.25f, (height - 1) * 0.025f);
            var right = new Vector2((width - 1) * 0.75f, left.Y);
            var knot = new Vector2((width - 1) / 2f, (height - 1) * 0.52f);
            _linkLength = Math.Max(0.01f, Vector2.Distance(left, knot) * 1.025f / LinksPerSide);
            for (var i = 0; i <= RightAnchor; i++)
            {
                _positions[i] = i <= Knot
                    ? Vector2.Lerp(left, knot, (float)i / LinksPerSide)
                    : Vector2.Lerp(knot, right, (float)(i - Knot) / LinksPerSide);
            }
            _positions[Center] = knot + new Vector2(0, Radius * _attachmentDistance);
        }

        public void Update(Vector3 acceleration, double elapsedSeconds)
        {
            if (!float.IsFinite(acceleration.X) || !float.IsFinite(acceleration.Y) ||
                !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(acceleration), "Invalid medallion simulation input.");

            var elapsed = Math.Min(elapsedSeconds, 0.1);
            if (elapsed == 0)
                return;

            var steps = (int)Math.Ceiling(elapsed / MaxStepSeconds);
            var dt = (float)(elapsed / steps);
            var force = new Vector2(
                Math.Clamp(-acceleration.X, -40, 40),
                Math.Clamp(acceleration.Y, -40, 40)) * 100f;
            for (var step = 0; step < steps; step++)
            {
                var damping = MathF.Exp(-1.5f * dt);
                for (var i = 1; i <= Center; i++)
                {
                    if (i == RightAnchor)
                        continue;
                    _previous[i] = _positions[i];
                    _velocities[i] *= damping;
                    _positions[i] += _velocities[i] * dt + force * (dt * dt);
                }

                for (var iteration = 0; iteration < SolverIterations; iteration++)
                {
                    for (var i = 0; i < RightAnchor; i++)
                        Constrain(i, i + 1, _linkLength, false);
                    for (var i = RightAnchor - 1; i >= 0; i--)
                        Constrain(i, i + 1, _linkLength, false);
                    Constrain(Knot, Center, Radius * _attachmentDistance, true);
                }

                for (var i = 1; i <= Center; i++)
                {
                    if (i != RightAnchor)
                        _velocities[i] = (_positions[i] - _previous[i]) / dt;
                }
            }
        }

        private void Constrain(int a, int b, float length, bool rigid)
        {
            var delta = _positions[b] - _positions[a];
            var distance = delta.Length();
            if (distance < 0.0001f || (!rigid && distance <= length))
                return;

            var weightA = InverseMass(a);
            var weightB = InverseMass(b);
            var correction = delta * ((distance - length) / (distance * (weightA + weightB)));
            _positions[a] += correction * weightA;
            _positions[b] -= correction * weightB;
        }

        private static float InverseMass(int index) => index switch
        {
            0 or RightAnchor => 0,
            Knot => 0.25f,
            Center => 0.1f,
            _ => 1
        };
    }
}
