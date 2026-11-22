using System.Diagnostics;
using RPiRgbLEDMatrix;
using WearWare.Architecture;
using WearWare.Services.Matrix;
using WearWare.Services.Physics.Imu;
using WearWare.Services.MatrixConfig;
using WearWare.Services.Physics.Simulations.Fluid;

namespace WearWare.Services.Physics
{
    public sealed class PhysicsHandler : IHandler
    {
        private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 30);
        private readonly IMatrixDisplay _display;
        private readonly ImuService _imu;
        private readonly MatrixConfigService _matrixConfig;
        private readonly ILogger<PhysicsHandler> _logger;
        private volatile HandlerState _state = HandlerState.Idle;

        public string Name => "Physics";
        public HandlerState State => _state;
        public event Action? StateChanged;

        public PhysicsHandler(
            IMatrixDisplay display,
            ImuService imu,
            MatrixConfigService matrixConfig,
            ILogger<PhysicsHandler> logger)
        {
            _display = display;
            _imu = imu;
            _matrixConfig = matrixConfig;
            _logger = logger;
        }

        public async Task WaitForImuAsync(CancellationToken cancellationToken)
        {
            while (_imu.State.Status == ImuStatus.Starting)
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            if (_imu.State.Status != ImuStatus.Available)
                throw new InvalidOperationException($"IMU is unavailable: {_imu.State.Status}.");
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                SetState(HandlerState.Running);
                await RunFluidAsync(_display, cancellationToken);
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
                try
                {
                    if (!_display.Clear())
                        _logger.LogError("[PHYSICSHANDLER] Cannot clear the matrix after Physics: matrix is not initialized.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PHYSICSHANDLER] Failed to clear the matrix after Physics.");
                }
            }
        }

        private async Task RunFluidAsync(IMatrixDisplay display, CancellationToken cancellationToken)
        {
            FluidSimulation? fluid = null;
            float[] density = [];
            Color[] pixels = [];
            var width = 0;
            var height = 0;
            long previousFrame = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frameStart = Stopwatch.GetTimestamp();
                var elapsed = previousFrame == 0 ? 0 : Stopwatch.GetElapsedTime(previousFrame, frameStart).TotalSeconds;
                previousFrame = frameStart;

                var reading = _imu.State;
                if (reading.Status != ImuStatus.Available || reading.Reading == null)
                    throw new InvalidOperationException($"IMU reading unavailable: {reading.Status}.");

                display.WithMatrix((matrix, canvas) =>
                {
                    var brightness = (byte)_matrixConfig.GetCurrentBrightness();
                    if (matrix.Brightness != brightness)
                        matrix.Brightness = brightness;

                    if (fluid == null || width != canvas.Width || height != canvas.Height)
                    {
                        width = canvas.Width;
                        height = canvas.Height;
                        fluid = new FluidSimulation(width, height);
                        density = new float[checked(width * height)];
                        pixels = new Color[density.Length];
                    }

                    canvas.Clear();
                    fluid.Update(reading.Reading.MountedAcceleration, elapsed);
                    fluid.WriteDensity(density);
                    var water = new Color(0, 100, 255);
                    for (var i = 0; i < pixels.Length; i++)
                        pixels[i] = density[i] >= 0.6f ? water : default;
                    canvas.SetPixels(0, 0, width, height, pixels);
                    matrix.SwapOnVsync(canvas);
                });

                var remaining = FrameInterval - Stopwatch.GetElapsedTime(frameStart);
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, cancellationToken);
            }
        }

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
                    _logger.LogError(ex, "[PHYSICSHANDLER] A state-changed listener failed.");
                }
            }
        }
    }
}
