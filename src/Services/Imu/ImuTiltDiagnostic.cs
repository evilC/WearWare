using System.Diagnostics;
using RPiRgbLEDMatrix;
using WearWare.Services.Matrix;
using WearWare.Services.MatrixConfig;
using WearWare.Services.Mode;

namespace WearWare.Services.Imu
{
    public sealed class ImuTiltDiagnostic : IPhysicsRenderer
    {
        private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 30);
        private readonly ImuService _imu;
        private readonly MatrixService _matrix;
        private readonly MatrixConfigService _config;
        private readonly ILogger<ImuTiltDiagnostic> _logger;

        public ImuTiltDiagnostic(ImuService imu, MatrixService matrix, MatrixConfigService config, ILogger<ImuTiltDiagnostic> logger)
        {
            _imu = imu;
            _matrix = matrix;
            _config = config;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var frameStart = Stopwatch.GetTimestamp();
                    var state = _imu.State;
                    if (state.Status != ImuStatus.Available || state.Reading == null)
                    {
                        throw new InvalidOperationException($"IMU reading unavailable: {state.Status}.");
                    }

                    var radians = state.Reading.FilteredTiltDegrees * Math.PI / 180;
                    _matrix.WithMatrix((matrix, canvas) =>
                    {
                        var brightness = (byte)_config.GetCurrentBrightness();
                        if (matrix.Brightness != brightness)
                        {
                            matrix.Brightness = brightness;
                        }

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

                    var remaining = FrameInterval - Stopwatch.GetElapsedTime(frameStart);
                    if (remaining > TimeSpan.Zero)
                    {
                        await Task.Delay(remaining, cancellationToken);
                    }
                }
            }
            finally
            {
                try
                {
                    if (!_matrix.Clear())
                    {
                        _logger.LogError("Cannot clear the matrix after physics: matrix is not initialized.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clear the matrix after physics.");
                }
            }
        }
    }
}
