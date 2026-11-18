using System.Diagnostics;
using System.Numerics;
using WearWare.Services.Imu.Sensors;

namespace WearWare.Services.Imu
{
    public sealed class ImuService : BackgroundService
    {
        private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1.0 / 30);
        private static readonly TimeSpan MaxReadingAge = TimeSpan.FromSeconds(1);
        private const double TiltSmoothingSeconds = 0.15;
        private readonly Func<IImuSensor>? _sensorFactory;
        private readonly ILogger<ImuService> _logger;
        private ImuState _state;

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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_sensorFactory == null)
            {
                return;
            }

            await Task.Yield();
            try
            {
                using var sensor = _sensorFactory();
                _logger.LogInformation("IMU sensor initialized.");
                double? filteredTilt = null;
                long previousSample = 0;

                while (!stoppingToken.IsCancellationRequested)
                {
                    var frameStart = Stopwatch.GetTimestamp();
                    var (acceleration, gyroscope) = sensor.Read();
                    var timestampTicks = Stopwatch.GetTimestamp();
                    var rawTilt = CalculateTiltDegrees(acceleration);
                    var tilt = rawTilt;
                    if (filteredTilt.HasValue)
                    {
                        var elapsed = Stopwatch.GetElapsedTime(previousSample, timestampTicks).TotalSeconds;
                        var weight = 1 - Math.Exp(-elapsed / TiltSmoothingSeconds);
                        tilt = filteredTilt.Value + weight * (rawTilt - filteredTilt.Value);
                    }

                    filteredTilt = tilt;
                    previousSample = timestampTicks;
                    var reading = new ImuReading(acceleration, gyroscope, rawTilt, tilt, DateTimeOffset.UtcNow, timestampTicks);
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
                _logger.LogError(ex, "IMU monitoring stopped after an initialization or read error.");
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
    }
}
