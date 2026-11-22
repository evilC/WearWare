using System.Diagnostics;
using RPiRgbLEDMatrix;
using WearWare.Architecture.Physics.Imu;
using WearWare.Services.Matrix;
using WearWare.Services.MatrixConfig;

namespace WearWare.Architecture.Physics.Simulations
{
    public abstract class PhysicsRenderer : IPhysicsRenderer
    {
        private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 30);
        private readonly ImuService _imu;
        private readonly MatrixService _matrix;
        private readonly MatrixConfigService _config;
        private readonly ILogger _logger;

        protected PhysicsRenderer(ImuService imu, MatrixService matrix, MatrixConfigService config, ILogger logger)
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
                Reset();
                long previousFrame = 0;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var frameStart = Stopwatch.GetTimestamp();
                    var elapsed = previousFrame == 0 ? 0 : Stopwatch.GetElapsedTime(previousFrame, frameStart).TotalSeconds;
                    previousFrame = frameStart;
                    var state = _imu.State;
                    if (state.Status != ImuStatus.Available || state.Reading == null)
                    {
                        throw new InvalidOperationException($"IMU reading unavailable: {state.Status}.");
                    }

                    _matrix.WithMatrix((matrix, canvas) =>
                    {
                        var brightness = (byte)_config.GetCurrentBrightness();
                        if (matrix.Brightness != brightness)
                        {
                            matrix.Brightness = brightness;
                        }

                        canvas.Clear();
                        DrawFrame(canvas, state.Reading, elapsed);
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

        protected virtual void Reset() { }
        protected abstract void DrawFrame(RGBLedCanvas canvas, ImuReading reading, double elapsedSeconds);
    }
}
