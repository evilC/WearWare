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
        private readonly string _calibrationPath = Path.Combine(PathConfig.ConfigPath, "imu-calibration.json");
        private readonly Func<IImuSensor>? _sensorFactory;
        private readonly ILogger<ImuService> _logger;
        private readonly object _samplesLock = new();
        private readonly object _calibrationLock = new();
        private readonly Queue<CalibrationSample> _samples = new();
        private ImuState _state;
        private ImuOrientationConfig? _orientation;
        private Alignment _alignment = new(0);

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

        public (bool Success, string Message) CalibrateAlignment()
        {
            lock (_calibrationLock)
            {
                if (_orientation == null)
                    return (false, "IMU initialization is not complete.");

                CalibrationSample[] samples;
                lock (_samplesLock)
                {
                    if (State.Status != ImuStatus.Available)
                        return (false, "IMU readings are unavailable.");

                    samples = _samples.ToArray();
                }

                if (samples.Length < 15 ||
                    Stopwatch.GetElapsedTime(samples[^1].TimestampTicks) > TimeSpan.FromMilliseconds(250) ||
                    Stopwatch.GetElapsedTime(samples[0].TimestampTicks, samples[^1].TimestampTicks) < TimeSpan.FromSeconds(0.8))
                    return (false, "Wait for IMU readings, then try again.");

                var average = Vector3.Zero;
                foreach (var sample in samples)
                {
                    if (!IsFinite(sample.Acceleration) || !IsFinite(sample.Gyroscope) ||
                        sample.Gyroscope.Length() > 8)
                        return (false, "Hold the shirt still and upright, then try again.");
                    average += sample.Acceleration;
                }
                average /= samples.Length;

                var magnitude = average.Length();
                if (!float.IsFinite(magnitude) || magnitude < 7 || magnitude > 12 ||
                    average.Y < magnitude * 0.5f ||
                    samples.Any(sample => Vector3.Distance(sample.Acceleration, average) > 0.5f))
                    return (false, "Hold the shirt still and upright, then try again.");

                // Rotate both vectors around the display normal so upright gravity has no X component.
                return SaveAlignment(Math.Atan2(average.X, average.Y), "IMU alignment calibrated.");
            }
        }

        public (bool Success, string Message) ResetAlignment()
        {
            lock (_calibrationLock)
            {
                return _orientation == null
                    ? (false, "IMU initialization is not complete.")
                    : SaveAlignment(0, "IMU calibration reset to default.");
            }
        }

        private (bool Success, string Message) SaveAlignment(double radians, string message)
        {
            try
            {
                var config = new AlignmentConfig
                {
                    Radians = radians,
                    X = _orientation!.X,
                    Y = _orientation.Y,
                    Z = _orientation.Z
                };
                Utils.JsonUtils.ToJsonFileAtomic(_calibrationPath, config, _logger);
                Volatile.Write(ref _alignment, new Alignment(radians));
                CalibrationWarning = null;
                return (true, message);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Could not save IMU alignment to {path}.", _calibrationPath);
                return (false, "Could not save IMU alignment. See the log for details.");
            }
        }

        private void LoadAlignment(ImuOrientationConfig orientation)
        {
            if (!File.Exists(_calibrationPath))
                return;

            try
            {
                var config = Utils.JsonUtils.FromJsonFile<AlignmentConfig>(_calibrationPath)
                    ?? throw new JsonException("IMU alignment configuration contains null.");
                if (!double.IsFinite(config.Radians) || Math.Abs(config.Radians) > Math.PI ||
                    config.X != orientation.X || config.Y != orientation.Y || config.Z != orientation.Z)
                    throw new JsonException("Invalid IMU alignment or mounting orientation has changed.");

                Volatile.Write(ref _alignment, new Alignment(config.Radians));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError(ex, "Could not load IMU alignment from {path}; using default alignment.", _calibrationPath);
                CalibrationWarning = "Could not load IMU alignment. Using default alignment; recalibrate or reset. See the log for details.";
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            var orientationPath = Path.Combine(PathConfig.ConfigPath, "imuconfig.json");
            try
            {
                var orientation = ImuOrientationConfig.Load(orientationPath);
                _logger.LogInformation("IMU orientation from {path}: X={x}, Y={y}, Z={z}.",
                    orientationPath, orientation.X, orientation.Y, orientation.Z);
                lock (_calibrationLock)
                {
                    LoadAlignment(orientation);
                    _orientation = orientation;
                }
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
                    var unalignedAcceleration = orientation.Apply(acceleration);
                    var unalignedGyroscope = orientation.Apply(gyroscope);
                    lock (_samplesLock)
                    {
                        _samples.Enqueue(new CalibrationSample(unalignedAcceleration, unalignedGyroscope, timestampTicks));
                        while (Stopwatch.GetElapsedTime(_samples.Peek().TimestampTicks, timestampTicks) > CalibrationWindow)
                            _samples.Dequeue();
                    }
                    var alignment = Volatile.Read(ref _alignment);
                    var mountedAcceleration = alignment.Apply(unalignedAcceleration);
                    var mountedGyroscope = alignment.Apply(unalignedGyroscope);
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
                    orientationPath);
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

        private sealed record CalibrationSample(Vector3 Acceleration, Vector3 Gyroscope, long TimestampTicks);

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
        }
    }
}
