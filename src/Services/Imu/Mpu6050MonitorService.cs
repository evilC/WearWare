using System.Device.I2c;
using Iot.Device.Imu;

namespace WearWare.Services.Imu
{
    public sealed class Mpu6050MonitorService : BackgroundService
    {
        private const int I2cBus = 1;
        private const int I2cAddress = 0x68;
        private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(100);
        private readonly ILogger<Mpu6050MonitorService> _logger;

        public Mpu6050MonitorService(ILogger<Mpu6050MonitorService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();

            try
            {
                using var sensor = new Mpu6050(I2cDevice.Create(new I2cConnectionSettings(I2cBus, I2cAddress)));
                _logger.LogInformation("MPU-6050 monitor started on I2C bus {bus}, address 0x{address:X2}.", I2cBus, I2cAddress);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var acceleration = sensor.GetAccelerometer();
                    var gyroscope = sensor.GetGyroscopeReading();
                    var tilt = ImuTilt.CalculateDegrees(acceleration);
                    Console.WriteLine(
                        $"[IMU] Acceleration (m/s^2): X={acceleration.X:F3}, Y={acceleration.Y:F3}, Z={acceleration.Z:F3}; " +
                        $"Gyroscope (deg/s): X={gyroscope.X:F3}, Y={gyroscope.Y:F3}, Z={gyroscope.Z:F3}; " +
                        $"Left/right tilt (deg, +right/-left): {tilt:F1}");

                    await Task.Delay(SampleInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MPU-6050 monitoring stopped after an initialization or read error. Playback will continue.");
            }
        }
    }
}
