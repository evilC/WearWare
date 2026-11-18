using System.Device.I2c;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Iot.Device.Imu;
using RPiRgbLEDMatrix;
using WearWare.Services.MatrixConfig;
using WearWare.Services.Options;
using WearWare.Utils;

namespace WearWare.Services.Imu
{
    internal static class ImuTiltDiagnostic
    {
        private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 30);

        public static async Task RunAsync()
        {
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, e) =>
            {
                e.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;
            using var sigterm = OperatingSystem.IsLinux()
                ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
                {
                    context.Cancel = true;
                    cancellation.Cancel();
                })
                : null;

            try
            {
                var matrixConfigPath = Path.Combine(PathConfig.ConfigPath, "matrixconfig.json");
                var options = JsonUtils.FromJsonFile<LedMatrixOptionsConfig>(matrixConfigPath)
                    ?? throw new InvalidOperationException($"Matrix configuration is null: {matrixConfigPath}");
                var appOptionsPath = Path.Combine(PathConfig.ConfigPath, "appoptions.json");
                var appOptions = File.Exists(appOptionsPath)
                    ? JsonUtils.FromJsonFile<AppOptions>(appOptionsPath)
                        ?? throw new InvalidOperationException($"App options are null: {appOptionsPath}")
                    : new AppOptions();
                var matrixOptions = options.ToRGBLedMatrixOptions();
                var brightnessCap = options.Brightness ?? 100;
                matrixOptions.Brightness = Math.Clamp(appOptions.CurrentBrightness ?? brightnessCap, 1, brightnessCap);

                var i2cDevice = I2cDevice.Create(new I2cConnectionSettings(1, Mpu6050.DefaultI2cAddress));
                Mpu6050 sensor;
                try
                {
                    sensor = new Mpu6050(i2cDevice);
                }
                catch
                {
                    i2cDevice.Dispose();
                    throw;
                }

                using (sensor)
                using (var matrix = new RGBLedMatrix(matrixOptions))
                {
                    var canvas = matrix.CreateOffscreenCanvas();
                    Console.WriteLine("IMU tilt diagnostic running. Press Ctrl+C to stop.");
                    try
                    {
                        while (!cancellation.IsCancellationRequested)
                        {
                            var frameStart = Stopwatch.GetTimestamp();
                            var tilt = ImuTilt.CalculateDegrees(sensor.GetAccelerometer());
                            var radians = tilt * Math.PI / 180;
                            var centerX = (canvas.Width - 1) / 2.0;
                            var centerY = (canvas.Height - 1) / 2.0;
                            var radius = (Math.Min(canvas.Width, canvas.Height) - 1) / 2.0;
                            var dx = radius * Math.Cos(radians);
                            var dy = -radius * Math.Sin(radians);

                            canvas.Clear();
                            canvas.DrawLine(
                                (int)Math.Round(centerX - dx), (int)Math.Round(centerY - dy),
                                (int)Math.Round(centerX + dx), (int)Math.Round(centerY + dy),
                                new Color(255, 255, 255));
                            matrix.SwapOnVsync(canvas);
                            Console.WriteLine($"[IMU] Left/right tilt (deg, +right/-left): {tilt:F1}");

                            var remaining = FrameInterval - Stopwatch.GetElapsedTime(frameStart);
                            if (remaining > TimeSpan.Zero)
                            {
                                await Task.Delay(remaining, cancellation.Token);
                            }
                        }
                    }
                    finally
                    {
                        canvas.Clear();
                        matrix.SwapOnVsync(canvas);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"IMU tilt diagnostic failed: {ex}");
                System.Environment.ExitCode = 1;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
    }
}
