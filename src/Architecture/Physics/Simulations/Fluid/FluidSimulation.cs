/*
Fluid simulation using Position-Based Fluids (PBF) method.
ToDo: Simulation currently has a "Margin"
ie the simulation exists outside the visible canvas area.
This is to work around an issue where particles near the edges of the canvas may not behave correctly.
Repro:
Tilt the sensor left.
Start tilting the sensor back to flat, notice how a column of particles at the edge of the screen "stack" in a column vertically.
This can also be reprodueced with particles at the bottom of the screen
*/
using System.Numerics;

namespace WearWare.Architecture.Physics.Simulations.Fluid
{
    internal sealed class FluidSimulation
    {
        private const int MaxParticles = 512;
        private const float PixelsPerMeter = 1000f / 1.85f;
        private const float MinimumSpacing = 4;
        private const float ViscosityRate = 27f;
        private const float AlignmentTolerance = 0.1f;
        private const float SymmetryBreakStrength = 0.05f;
        private const int SolverIterations = 6;
        private const double MaxStepSeconds = 1.0 / 120;
        private readonly int _width;
        private readonly int _height;
        private readonly float _spacing;
        private readonly float _supportRadius;
        private readonly float _margin;
        private readonly float _restDensity;
        private readonly float _paddingX;
        private readonly float _paddingY;
        private readonly Vector2[] _positions;
        private readonly Vector2[] _previous;
        private readonly Vector2[] _velocities;
        private readonly Vector2[] _corrections;
        private readonly Vector2[] _velocityChanges;
        private readonly float[] _lambdas;
        private readonly Vector2[] _wallOffsets;
        private readonly List<int>[] _neighbors;
        private readonly List<Vector2>[] _gradients;
        private readonly int[] _cellHeads;
        private readonly int[] _next;
        private readonly int _gridWidth;
        private readonly int _gridHeight;

        public int ParticleCount => _positions.Length;

        public FluidSimulation(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Invalid fluid canvas dimensions.");
            }

            _width = width;
            _height = height;
            var spacing = Math.Max(MinimumSpacing, (float)Math.Sqrt((double)width * height / (2 * MaxParticles)));
            int columns;
            int rows;
            float paddingX;
            float paddingY;
            do
            {
                paddingX = Math.Min(spacing / 2, (width - 1) / 2f);
                paddingY = Math.Min(spacing / 2, (height - 1) / 2f);
                columns = Math.Max(1, (int)((width - 1 - 2 * paddingX) / spacing) + 1);
                rows = Math.Max(1, ((int)((height - 1 - 2 * paddingY) / spacing) + 2) / 2);
                if ((long)columns * rows <= MaxParticles) break;
                spacing *= 1.1f;
            } while (true);

            // rows = Math.Max(1, (rows + 1) / 2) - Original code - existing rows value compensates for margin bodge
            rows = Math.Min(rows, (rows + 1) / 2 + 1);
            _spacing = spacing;
            _supportRadius = spacing * 2;
            _margin = spacing * 2;
            _paddingX = paddingX;
            _paddingY = paddingY;
            // Density of a regular square lattice at the chosen support radius.
            _restDensity = 1 + 4 * Kernel(0.5f) + 4 * Kernel(MathF.Sqrt(0.5f));
            var count = columns * rows;
            _positions = new Vector2[count];
            _previous = new Vector2[count];
            _velocities = new Vector2[count];
            _corrections = new Vector2[count];
            _velocityChanges = new Vector2[count];
            _lambdas = new float[count];
            _wallOffsets = new Vector2[count];
            _neighbors = new List<int>[count];
            _gradients = new List<Vector2>[count];
            _next = new int[count];
            _gridWidth = Math.Max(1, (int)Math.Ceiling((width + 2.0 * _margin) / _supportRadius));
            _gridHeight = Math.Max(1, (int)Math.Ceiling((height + 2.0 * _margin) / _supportRadius));
            _cellHeads = new int[checked(_gridWidth * _gridHeight)];

            var maxWallOffset = new Vector2(
                Math.Min(spacing / 2, (width - 1 - 2 * paddingX) / 2),
                Math.Min(spacing / 2, (height - 1 - 2 * paddingY) / 2));
            for (var i = 0; i < count; i++)
            {
                var row = i / columns;
                var column = i % columns;
                // Stable offsets break wall alignment without adding frame-to-frame jitter.
                var hash = unchecked((uint)(i + 1) * 2654435761u);
                _wallOffsets[i] = maxWallOffset * ((hash >> 8) / 16777216f);
                _positions[i] = Constrain(new Vector2(paddingX + column * spacing, height - 1 - paddingY - row * spacing), i);
                _neighbors[i] = new List<int>(16);
                _gradients[i] = new List<Vector2>(16);
            }
        }

        public void Update(Vector3 acceleration, double elapsedSeconds)
        {
            if (!float.IsFinite(acceleration.X) || !float.IsFinite(acceleration.Y) ||
                !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(acceleration), "Invalid fluid simulation input.");
            }

            var force = new Vector2(-acceleration.X * PixelsPerMeter, acceleration.Y * PixelsPerMeter);
            if (!float.IsFinite(force.X) || !float.IsFinite(force.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(acceleration), "Fluid acceleration is out of range.");
            }

            var elapsed = Math.Min(elapsedSeconds, 0.05);
            var steps = Math.Max(1, (int)Math.Ceiling(elapsed / MaxStepSeconds));
            var dt = (float)(elapsed / steps);
            if (dt == 0) return;
            for (var step = 0; step < steps; step++)
            {
                Step(force, dt);
            }
        }

        private void Step(Vector2 acceleration, float dt)
        {
            var scale = Math.Max(Math.Abs(acceleration.X), Math.Abs(acceleration.Y));
            var axis = scale > 0 ? Vector2.Normalize(acceleration / scale) : Vector2.Zero;
            for (var i = 0; i < _positions.Length; i++)
            {
                _previous[i] = _positions[i];
                _velocities[i] += acceleration * dt;
                _positions[i] = Constrain(_positions[i] + _velocities[i] * dt, i);
            }

            for (var iteration = 0; iteration < SolverIterations; iteration++)
            {
                FindNeighbors();
                CalculateLambdas();
                CalculateCorrections(axis);
                for (var i = 0; i < _positions.Length; i++)
                {
                    _positions[i] = Constrain(_positions[i] + _corrections[i], i);
                }
            }

            for (var i = 0; i < _positions.Length; i++)
            {
                _velocities[i] = (_positions[i] - _previous[i]) / dt;
            }

            FindNeighbors();
            var viscosity = 1 - MathF.Exp(-ViscosityRate * dt);
            for (var i = 0; i < _positions.Length; i++)
            {
                var change = Vector2.Zero;
                var weightSum = 0f;
                foreach (var neighbor in _neighbors[i])
                {
                    var weight = Kernel(Vector2.Distance(_positions[i], _positions[neighbor]) / _supportRadius);
                    change += (_velocities[neighbor] - _velocities[i]) * weight;
                    weightSum += weight;
                }
                _velocityChanges[i] = weightSum > 0 ? change * (viscosity / weightSum) : Vector2.Zero;
            }
            for (var i = 0; i < _positions.Length; i++)
            {
                _velocities[i] += _velocityChanges[i];
            }
        }

        private void FindNeighbors()
        {
            Array.Fill(_cellHeads, -1);
            for (var i = 0; i < _positions.Length; i++)
            {
                _neighbors[i].Clear();
                var cellX = Math.Clamp((int)((_positions[i].X + _margin) / _supportRadius), 0, _gridWidth - 1);
                var cellY = Math.Clamp((int)((_positions[i].Y + _margin) / _supportRadius), 0, _gridHeight - 1);
                var cell = cellY * _gridWidth + cellX;
                _next[i] = _cellHeads[cell];
                _cellHeads[cell] = i;
            }

            var supportSquared = _supportRadius * _supportRadius;
            for (var i = 0; i < _positions.Length; i++)
            {
                var neighbors = _neighbors[i];
                var cellX = Math.Clamp((int)((_positions[i].X + _margin) / _supportRadius), 0, _gridWidth - 1);
                var cellY = Math.Clamp((int)((_positions[i].Y + _margin) / _supportRadius), 0, _gridHeight - 1);
                for (var y = Math.Max(0, cellY - 1); y <= Math.Min(_gridHeight - 1, cellY + 1); y++)
                {
                    for (var x = Math.Max(0, cellX - 1); x <= Math.Min(_gridWidth - 1, cellX + 1); x++)
                    {
                        for (var other = _cellHeads[y * _gridWidth + x]; other >= 0; other = _next[other])
                        {
                            if (other > i && Vector2.DistanceSquared(_positions[i], _positions[other]) < supportSquared)
                            {
                                neighbors.Add(other);
                                _neighbors[other].Add(i);
                            }
                        }
                    }
                }
            }
        }

        private void CalculateLambdas()
        {
            for (var i = 0; i < _positions.Length; i++)
            {
                var density = 1f;
                var gradientSum = Vector2.Zero;
                var squaredGradients = 0f;
                var gradients = _gradients[i];
                gradients.Clear();
                foreach (var neighbor in _neighbors[i])
                {
                    var gradient = Gradient(i, neighbor, out var distance);
                    gradients.Add(gradient);
                    density += Kernel(distance / _supportRadius);
                    gradientSum += gradient;
                    squaredGradients += gradient.LengthSquared();
                }
                squaredGradients += gradientSum.LengthSquared();
                var compression = Math.Max(density / _restDensity - 1, 0);
                _lambdas[i] = -compression / (squaredGradients + 0.0001f);
            }
        }

        private void CalculateCorrections(Vector2 axis)
        {
            var lateral = new Vector2(axis.Y, -axis.X);
            var range = _spacing * AlignmentTolerance;
            for (var i = 0; i < _positions.Length; i++)
            {
                var correction = Vector2.Zero;
                var neighbors = _neighbors[i];
                var gradients = _gradients[i];
                for (var n = 0; n < neighbors.Count; n++)
                {
                    var neighbor = neighbors[n];
                    var lambda = _lambdas[i] + _lambdas[neighbor];
                    if (lambda == 0) continue;
                    var pair = lambda * gradients[n];
                    var difference = _positions[i] - _positions[neighbor];
                    var across = Math.Abs(Vector2.Dot(difference, lateral));
                    var along = Math.Abs(Vector2.Dot(difference, axis));
                    if (across < range && along > across)
                    {
                        // Break alignment symmetrically so compressed particles can move sideways.
                        var sign = ((i + neighbor) & 1) == 0 ? 1f : -1f;
                        pair += lateral * (Vector2.Dot(pair, axis) * sign * SymmetryBreakStrength * (1 - across / range));
                    }
                    correction += pair;
                }
                var length = correction.Length();
                if (length > _spacing / 2)
                {
                    correction *= (_spacing / 2) / length;
                }
                _corrections[i] = correction;
            }
        }

        private Vector2 Gradient(int index, int neighbor, out float distance)
        {
            var difference = _positions[index] - _positions[neighbor];
            distance = difference.Length();
            Vector2 direction;
            if (distance < 0.0001f)
            {
                direction = ((index + neighbor) & 1) == 0 ? Vector2.UnitX : Vector2.UnitY;
                if (index < neighbor) direction = -direction;
            }
            else
            {
                direction = difference / distance;
            }
            var remaining = Math.Max(0, 1 - distance / _supportRadius);
            return direction * (-3 * remaining * remaining / (_supportRadius * _restDensity));
        }

        private static float Kernel(float normalizedDistance)
        {
            var remaining = Math.Max(0, 1 - normalizedDistance);
            return remaining * remaining * remaining;
        }

        private Vector2 Constrain(Vector2 position, int index) => new Vector2(
            Math.Clamp(position.X, _paddingX + _wallOffsets[index].X - _margin, _width - 1 - _paddingX - _wallOffsets[index].X + _margin),
            Math.Clamp(position.Y, _paddingY + _wallOffsets[index].Y - _margin, _height - 1 - _paddingY - _wallOffsets[index].Y + _margin));

        public void WriteDensity(Span<float> density)
        {
            if (density.Length != checked(_width * _height))
            {
                throw new ArgumentException("Density buffer does not match the canvas.", nameof(density));
            }
            density.Clear();
            var radius = _spacing * 1.35f;
            var radiusSquared = radius * radius;
            foreach (var position in _positions)
            {
                var minX = Math.Max(0, (int)MathF.Floor(position.X - radius));
                var maxX = Math.Min(_width - 1, (int)MathF.Ceiling(position.X + radius));
                var minY = Math.Max(0, (int)MathF.Floor(position.Y - radius));
                var maxY = Math.Min(_height - 1, (int)MathF.Ceiling(position.Y + radius));
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var dx = x - position.X;
                        var dy = y - position.Y;
                        var weight = 1 - (dx * dx + dy * dy) / radiusSquared;
                        if (weight > 0)
                        {
                            density[y * _width + x] += weight * weight;
                        }
                    }
                }
            }
        }
    }
}
