using System.Diagnostics;
using RPiRgbLEDMatrix;
using WearWare.Architecture;
using WearWare.Architecture.Display;
using WearWare.Architecture.Physics.Imu;
using WearWare.Services.MatrixConfig;

namespace WearWare.Architecture.Physics.Simulations
{
    public abstract class PhysicsRenderer : IPhysicsRenderer
    {
        private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 30);
        private readonly IMatrixController _matrixController;
        private readonly ImuService _imu;
        private readonly MatrixConfigService _config;
        private readonly ILogger _logger;
        private volatile HandlerState _state = HandlerState.Idle;

        public string Name => GetType().Name;
        public HandlerState State => _state;
        public event Action? StateChanged;

        protected PhysicsRenderer(ImuService imu, IMatrixController matrixController, MatrixConfigService config, ILogger logger)
        {
            _imu = imu;
            _matrixController = matrixController;
            _config = config;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            IMatrixDisplay? display = null;
            try
            {
                display = await _matrixController.RequestControlAsync(this, cancellationToken);
                SetState(HandlerState.Running);
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

                    display.WithMatrix((matrix, canvas) =>
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetState(HandlerState.Idle);
                throw;
            }
            catch
            {
                SetState(HandlerState.Failed);
                throw;
            }
            finally
            {
                if (display != null)
                {
                    try
                    {
                        if (!display.Clear())
                            _logger.LogError("Cannot clear the matrix after physics: matrix is not initialized.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to clear the matrix after physics.");
                    }
                    finally
                    {
                        _matrixController.ReleaseControl(this);
                    }
                }
            }
        }

        protected virtual void Reset() { }
        protected abstract void DrawFrame(RGBLedCanvas canvas, ImuReading reading, double elapsedSeconds);

        private void SetState(HandlerState state)
        {
            if (_state == state)
                return;

            _state = state;
            var handlers = StateChanged;
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
                    _logger.LogError(ex, "A physics renderer state listener failed.");
                }
            }
        }
    }
}
