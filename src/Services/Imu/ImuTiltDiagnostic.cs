using System.Diagnostics;
using System.Runtime.InteropServices;
using RPiRgbLEDMatrix;
using WearWare.Services.Imu.Sensors;
using WearWare.Services.Matrix;
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
                var brightnessCap = options.Brightness ?? 100;
                options.Brightness = Math.Clamp(appOptions.CurrentBrightness ?? brightnessCap, 1, brightnessCap);

                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                using var imu = new ImuService(loggerFactory.CreateLogger<ImuService>(), () => new Mpu6050Sensor());
                await imu.StartAsync(cancellation.Token);
                try
                {
                    var startupTime = Stopwatch.GetTimestamp();
                    while (imu.State.Status == ImuStatus.Starting)
                    {
                        if (Stopwatch.GetElapsedTime(startupTime) > TimeSpan.FromSeconds(5))
                        {
                            throw new TimeoutException("Timed out waiting for the IMU sensor.");
                        }

                        await Task.Delay(50, cancellation.Token);
                    }

                    GetReading(imu);
                    using var matrixService = new MatrixService(loggerFactory.CreateLogger<MatrixService>(), options);
                    Console.WriteLine("IMU tilt diagnostic running. Press Ctrl+C to stop.");
                    try
                    {
                        while (!cancellation.IsCancellationRequested)
                        {
                            var frameStart = Stopwatch.GetTimestamp();
                            var reading = GetReading(imu);
                            var radians = reading.FilteredTiltDegrees * Math.PI / 180;
                            matrixService.WithMatrix((matrix, canvas) =>
                            {
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
                            });
                            // Console.WriteLine($"[IMU] Left/right tilt (deg, +right/-left): {reading.FilteredTiltDegrees:F1} (raw: {reading.RawTiltDegrees:F1})");
                            var remaining = FrameInterval - Stopwatch.GetElapsedTime(frameStart);
                            if (remaining > TimeSpan.Zero)
                            {
                                await Task.Delay(remaining, cancellation.Token);
                            }
                        }
                    }
                    finally
                    {
                        if (!matrixService.Clear())
                        {
                            throw new InvalidOperationException("Matrix is not initialized.");
                        }
                    }
                }
                finally
                {
                    using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await imu.StopAsync(stopTimeout.Token);
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

        private static ImuReading GetReading(ImuService imu)
        {
            var state = imu.State;
            if (state.Status != ImuStatus.Available || state.Reading == null)
            {
                throw new InvalidOperationException($"IMU reading unavailable: {state.Status}.");
            }

            return state.Reading;
        }
    }
}
