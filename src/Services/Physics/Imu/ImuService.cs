using System.Diagnostics;
using System.Numerics;
using WearWare.Config;
using WearWare.Services.Physics.Imu.Sensors;

namespace WearWare.Services.Physics.Imu
{
    public sealed class ImuService : BackgroundService
    {
        private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1.0 / 30);
        private static readonly TimeSpan MaxReadingAge = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan CalibrationWindow = TimeSpan.FromSeconds(1);
        private const double TiltSmoothingSeconds = 0.15;
        private readonly string _orientationPath = Path.Combine(PathConfig.ConfigPath, "imuconfig.json");
        private readonly string _calibrationPath = Path.Combine(PathConfig.ConfigPath, "imu-calibration.json");
        private readonly Func<IImuSensor>? _sensorFactory;
        private readonly ILogger<ImuService> _logger;
        private readonly object _samplesLock = new();
        private readonly object _calibrationLock = new();
        private readonly Queue<CalibrationSample> _samples = new();
        private ImuState _state;
        private Transform? _transform;

        public ImuService(ILogger<ImuService> logger, Func<IImuSensor>? sensorFactory)
        {
            _logger = logger;
            _sensorFactory = sensorFactory;
            _state = new ImuState(sensorFactory == null ? ImuStatus.Unavailable : ImuStatus.Starting, null);
        }

        public ImuState State
        {
            get
            {
                var state = Volatile.Read(ref _state);
                return state.Status == ImuStatus.Available && state.Reading != null && state.Reading.Age > MaxReadingAge
                    ? state with { Status = ImuStatus.Stale }
                    : state;
            }
        }

        public string? CalibrationWarning { get; private set; }
        public ImuOrientation? Orientation => Volatile.Read(ref _transform)?.Orientation.Orientation;
        public event Action? OrientationChanged;

        public (bool Success, string Message) SetOrientation(ImuOrientation orientation)
        {
            lock (_calibrationLock)
            {
                var current = Volatile.Read(ref _transform);
                if (current == null)
                    return (false, "IMU initialization is not complete.");
                if (!Enum.IsDefined(orientation))
                    return (false, "Invalid IMU orientation.");
                if (current.Orientation.Orientation == orientation)
                    return (true, "IMU orientation is unchanged.");

                var config = ImuOrientationConfig.Create(orientation);
                try
                {
                    Utils.JsonUtils.ToJsonFileAtomic(_orientationPath, config, _logger);
                    lock (_samplesLock)
                    {
                        _samples.Clear();
                        Volatile.Write(ref _transform, new Transform(config, new Alignment(0)));
                    }
                    CalibrationWarning = null;
                    NotifyOrientationChanged();
                    return (true, "IMU orientation updated. Please calibrate the IMU again.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(ex, "Could not save IMU orientation to {path}.", _orientationPath);
                    return (false, "Could not save IMU orientation. See the log for details.");
                }
            }
        }

        public (bool Success, string Message) CalibrateAlignment()
        {
            lock (_calibrationLock)
            {
                var transform = Volatile.Read(ref _transform);
                if (transform == null)
                    return (false, "IMU initialization is not complete.");

                var (success, message, rawAverage) = GetStationaryAcceleration();
                if (!success)
                    return (false, message);
                var average = transform.Orientation.Apply(rawAverage);
                var magnitude = average.Length();
                if (average.Y < magnitude * 0.5f)
                    return (false, "Hold the shirt still and upright, then try again.");

                // Rotate both vectors around the display normal so upright gravity has no X component.
                return SaveAlignment(Math.Atan2(average.X, average.Y), "IMU alignment calibrated.");
            }
        }

        public (bool Success, string Message) ResetAlignment()
        {
            lock (_calibrationLock)
            {
                return Volatile.Read(ref _transform) == null
                    ? (false, "IMU initialization is not complete.")
                    : SaveAlignment(0, "IMU calibration reset to default.");
            }
        }

        internal (bool Success, string Message, Vector3 Acceleration) GetStationaryAcceleration()
        {
            CalibrationSample[] samples;
            lock (_samplesLock)
            {
                if (State.Status != ImuStatus.Available)
                    return (false, "IMU readings are unavailable.", default);
                samples = _samples.ToArray();
            }

            if (samples.Length < 15 ||
                Stopwatch.GetElapsedTime(samples[^1].TimestampTicks) > TimeSpan.FromMilliseconds(250) ||
                Stopwatch.GetElapsedTime(samples[0].TimestampTicks, samples[^1].TimestampTicks) < TimeSpan.FromSeconds(0.8))
                return (false, "Wait for IMU readings, then try again.", default);

            var average = Vector3.Zero;
            foreach (var sample in samples)
            {
                if (!IsFinite(sample.Acceleration) || !IsFinite(sample.Gyroscope) || sample.Gyroscope.Length() > 8)
                    return (false, "Hold the shirt still for a second, then try again.", default);
                average += sample.Acceleration;
            }
            average /= samples.Length;
            var magnitude = average.Length();
            if (!float.IsFinite(magnitude) || magnitude < 7 || magnitude > 12 ||
                samples.Any(sample => Vector3.Distance(sample.Acceleration, average) > 0.5f))
                return (false, "Hold the shirt still for a second, then try again.", default);
            return (true, string.Empty, average);
        }

        private (bool Success, string Message) SaveAlignment(double radians, string message)
        {
            var transform = Volatile.Read(ref _transform)!;
            try
            {
                var config = new AlignmentConfig
                {
                    Radians = radians,
                    X = transform.Orientation.X,
                    Y = transform.Orientation.Y,
                    Z = transform.Orientation.Z,
                    CalibrationId = transform.Orientation.CalibrationId
                };
                Utils.JsonUtils.ToJsonFileAtomic(_calibrationPath, config, _logger);
                Volatile.Write(ref _transform, new Transform(transform.Orientation, new Alignment(radians)));
                CalibrationWarning = null;
                return (true, message);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Could not save IMU alignment to {path}.", _calibrationPath);
                return (false, "Could not save IMU alignment. See the log for details.");
            }
        }

        private Alignment LoadAlignment(ImuOrientationConfig orientation)
        {
            if (!File.Exists(_calibrationPath))
                return new Alignment(0);

            try
            {
                var config = Utils.JsonUtils.FromJsonFile<AlignmentConfig>(_calibrationPath)
                    ?? throw new JsonException("IMU alignment configuration contains null.");
                if (!double.IsFinite(config.Radians) || Math.Abs(config.Radians) > Math.PI)
                    throw new JsonException("Invalid IMU alignment.");
                if (config.X != orientation.X || config.Y != orientation.Y || config.Z != orientation.Z ||
                    config.CalibrationId != orientation.CalibrationId)
                {
                    CalibrationWarning = "IMU orientation changed. Please calibrate the IMU again.";
                    return new Alignment(0);
                }
                return new Alignment(config.Radians);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError(ex, "Could not load IMU alignment from {path}; using default alignment.", _calibrationPath);
                CalibrationWarning = "Could not load IMU alignment. Using default alignment; recalibrate or reset. See the log for details.";
                return new Alignment(0);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            try
            {
                var orientation = ImuOrientationConfig.Load(_orientationPath);
                _logger.LogInformation("IMU orientation from {path}: X={x}, Y={y}, Z={z}.",
                    _orientationPath, orientation.X, orientation.Y, orientation.Z);
                lock (_calibrationLock)
                {
                    Volatile.Write(ref _transform, new Transform(orientation, LoadAlignment(orientation)));
                }
                NotifyOrientationChanged();
                if (_sensorFactory == null)
                {
                    return;
                }

                using var sensor = _sensorFactory();
                _logger.LogInformation("IMU sensor initialized.");
                double? filteredTilt = null;
                long previousSample = 0;

                while (!stoppingToken.IsCancellationRequested)
                {
                    var frameStart = Stopwatch.GetTimestamp();
                    var (acceleration, gyroscope) = sensor.Read();
                    var timestampTicks = Stopwatch.GetTimestamp();
                    lock (_samplesLock)
                    {
                        _samples.Enqueue(new CalibrationSample(acceleration, gyroscope, timestampTicks));
                        while (Stopwatch.GetElapsedTime(_samples.Peek().TimestampTicks, timestampTicks) > CalibrationWindow)
                            _samples.Dequeue();
                    }
                    var transform = Volatile.Read(ref _transform)!;
                    var mountedAcceleration = transform.Alignment.Apply(transform.Orientation.Apply(acceleration));
                    var mountedGyroscope = transform.Alignment.Apply(transform.Orientation.Apply(gyroscope));
                    var rawTilt = CalculateTiltDegrees(mountedAcceleration);
                    var tilt = rawTilt;
                    if (filteredTilt.HasValue)
                    {
                        var elapsed = Stopwatch.GetElapsedTime(previousSample, timestampTicks).TotalSeconds;
                        var weight = 1 - Math.Exp(-elapsed / TiltSmoothingSeconds);
                        tilt = filteredTilt.Value + weight * (rawTilt - filteredTilt.Value);
                    }

                    filteredTilt = tilt;
                    previousSample = timestampTicks;
                    var reading = new ImuReading(acceleration, gyroscope, mountedAcceleration, mountedGyroscope,
                        rawTilt, tilt, DateTimeOffset.UtcNow, timestampTicks);
                    Volatile.Write(ref _state, new ImuState(ImuStatus.Available, reading));

                    var remaining = SampleInterval - Stopwatch.GetElapsedTime(frameStart);
                    if (remaining > TimeSpan.Zero)
                    {
                        await Task.Delay(remaining, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _state, new ImuState(ImuStatus.Failed, null));
                _logger.LogError(ex, "IMU monitoring stopped after a configuration, initialization or read error. Configuration: {path}.",
                    _orientationPath);
                return;
            }

            Volatile.Write(ref _state, new ImuState(ImuStatus.Stopped, null));
        }

        private static double CalculateTiltDegrees(Vector3 acceleration)
        {
            return Math.Atan2(
                -acceleration.X,
                Math.Sqrt(acceleration.Y * acceleration.Y + acceleration.Z * acceleration.Z)) * 180 / Math.PI;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

        private void NotifyOrientationChanged()
        {
            var handlers = OrientationChanged;
            if (handlers == null)
                return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An IMU orientation listener failed.");
                }
            }
        }

        private sealed record CalibrationSample(Vector3 Acceleration, Vector3 Gyroscope, long TimestampTicks);

        private sealed record Transform(ImuOrientationConfig Orientation, Alignment Alignment);

        private sealed class Alignment(double radians)
        {
            private readonly float _cos = (float)Math.Cos(radians);
            private readonly float _sin = (float)Math.Sin(radians);

            public Vector3 Apply(Vector3 value) =>
                new(value.X * _cos - value.Y * _sin, value.X * _sin + value.Y * _cos, value.Z);
        }

        private sealed class AlignmentConfig
        {
            [System.Text.Json.Serialization.JsonRequired]
            public double Radians { get; set; }
            [System.Text.Json.Serialization.JsonRequired]
            public int X { get; set; }
            [System.Text.Json.Serialization.JsonRequired]
            public int Y { get; set; }
            [System.Text.Json.Serialization.JsonRequired]
            public int Z { get; set; }
            public Guid? CalibrationId { get; set; }
        }
    }
}
