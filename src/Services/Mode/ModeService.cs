using WearWare.Services.Imu;
using WearWare.Services.MediaController;

namespace WearWare.Services.Mode
{
    public enum DisplayMode
    {
        Playlist,
        Physics
    }

    public sealed record ModeState(DisplayMode Mode, string? Message);

    public sealed class ModeService : IHostedService
    {
        private readonly SemaphoreSlim _transition = new SemaphoreSlim(1, 1);
        private readonly MediaControllerService _mediaController;
        private readonly ImuService _imu;
        private readonly IPhysicsRenderer? _renderer;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<ModeService> _logger;
        private ModeState _state = new ModeState(DisplayMode.Playlist, null);
        private CancellationTokenSource? _physicsCancellation;
        private Task? _physicsTask;
        private volatile bool _stopping;

        public event Action? StateChanged;
        public ModeState State => Volatile.Read(ref _state);

        public ModeService(MediaControllerService mediaController, ImuService imu, IPhysicsRenderer? renderer,
            IHostApplicationLifetime lifetime, ILogger<ModeService> logger)
        {
            _mediaController = mediaController;
            _imu = imu;
            _renderer = renderer;
            _lifetime = lifetime;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _lifetime.ApplicationStopping.Register(() => _stopping = true);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _stopping = true;
            await _transition.WaitAsync(cancellationToken);
            try
            {
                await StopPhysicsAsync();
            }
            finally
            {
                _transition.Release();
            }
        }

        public async Task SetModeAsync(DisplayMode mode)
        {
            if (mode != DisplayMode.Playlist && mode != DisplayMode.Physics)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            await _transition.WaitAsync();
            try
            {
                if (_stopping || mode == State.Mode)
                {
                    return;
                }

                if (mode == DisplayMode.Playlist)
                {
                    var error = await StopPhysicsAsync();
                    if (!_stopping)
                    {
                        _mediaController.Resume();
                    }
                    SetState(new ModeState(DisplayMode.Playlist, error));
                    return;
                }

                var imuStatus = _imu.State.Status;
                if (_renderer == null || imuStatus != ImuStatus.Available)
                {
                    var message = _renderer == null
                        ? "Physics is unavailable on this device."
                        : $"Physics is unavailable: IMU status is {imuStatus}.";
                    SetState(new ModeState(DisplayMode.Playlist, message));
                    return;
                }

                _mediaController.Suspend();
                if (_stopping)
                {
                    return;
                }

                CancellationTokenSource? cancellation = null;
                Task task;
                try
                {
                    cancellation = new CancellationTokenSource();
                    var token = cancellation.Token;
                    task = Task.Run(() => _renderer.RunAsync(token));
                }
                catch (Exception ex)
                {
                    cancellation?.Dispose();
                    _logger.LogError(ex, "Failed to start physics.");
                    if (!_stopping)
                    {
                        _mediaController.Resume();
                    }
                    SetState(new ModeState(DisplayMode.Playlist, "Could not start Physics."));
                    return;
                }

                _physicsCancellation = cancellation;
                _physicsTask = task;
                SetState(new ModeState(DisplayMode.Physics, null));
                _ = ObservePhysicsAsync(task, cancellation.Token);
            }
            finally
            {
                _transition.Release();
            }
        }

        private async Task<string?> StopPhysicsAsync()
        {
            var cancellation = _physicsCancellation;
            var task = _physicsTask;
            if (cancellation == null || task == null)
            {
                return null;
            }

            cancellation.Cancel();
            string? error = null;
            try
            {
                await task;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Physics stopped unexpectedly.");
                error = "Physics failed. Returned to Playlist.";
            }
            finally
            {
                _physicsTask = null;
                _physicsCancellation = null;
                cancellation.Dispose();
            }
            return error;
        }

        private async Task ObservePhysicsAsync(Task task, CancellationToken cancellationToken)
        {
            Exception failure;
            try
            {
                await task;
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                failure = new InvalidOperationException("Physics renderer stopped unexpectedly.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            _logger.LogError(failure, "Physics failed; returning to Playlist.");
            await _transition.WaitAsync();
            try
            {
                if (!ReferenceEquals(_physicsTask, task) || _stopping)
                {
                    return;
                }

                _physicsTask = null;
                _physicsCancellation?.Dispose();
                _physicsCancellation = null;
                _mediaController.Resume();
                SetState(new ModeState(DisplayMode.Playlist, "Physics failed. Returned to Playlist."));
            }
            finally
            {
                _transition.Release();
            }
        }

        private void SetState(ModeState state)
        {
            Volatile.Write(ref _state, state);
            var handlers = StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "A mode state listener failed.");
                }
            }
        }
    }
}
