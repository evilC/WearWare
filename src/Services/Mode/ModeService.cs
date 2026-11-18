using WearWare.Services.Imu;
using WearWare.Services.MediaController;
using WearWare.Services.Simulations;

namespace WearWare.Services.Mode
{
    public enum DisplayMode
    {
        Playlist,
        Physics
    }

    public enum PhysicsMode
    {
        ImuTest,
        Ball,
        Fluid
    }

    public sealed record ModeState(DisplayMode Mode, PhysicsMode Physics, string? Message, DisplayMode PreferredMode);

    public sealed class ModeService : IHostedService
    {
        private readonly SemaphoreSlim _transition = new SemaphoreSlim(1, 1);
        private readonly MediaControllerService _mediaController;
        private readonly ImuService _imu;
        private readonly IPhysicsRenderer? _imuTestRenderer;
        private readonly IPhysicsRenderer? _ballRenderer;
        private readonly IPhysicsRenderer? _fluidRenderer;
        private readonly ModeConfigService _config;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<ModeService> _logger;
        private readonly CancellationTokenSource _restoreCancellation = new CancellationTokenSource();
        private ModeState _state;
        private CancellationTokenSource? _physicsCancellation;
        private Task? _physicsTask;
        private volatile bool _stopping;

        public event Action? StateChanged;
        public ModeState State => Volatile.Read(ref _state);

        public ModeService(MediaControllerService mediaController, ImuService imu,
            IPhysicsRenderer? imuTestRenderer, IPhysicsRenderer? ballRenderer, IPhysicsRenderer? fluidRenderer,
            ModeConfigService config,
            IHostApplicationLifetime lifetime, ILogger<ModeService> logger)
        {
            _mediaController = mediaController;
            _imu = imu;
            _imuTestRenderer = imuTestRenderer;
            _ballRenderer = ballRenderer;
            _fluidRenderer = fluidRenderer;
            _config = config;
            _lifetime = lifetime;
            _logger = logger;
            _state = new ModeState(DisplayMode.Playlist, config.Simulation, config.LoadError, config.Mode);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _lifetime.ApplicationStopping.Register(() =>
            {
                _stopping = true;
                _restoreCancellation.Cancel();
            });
            if (State.PreferredMode == DisplayMode.Physics)
            {
                _ = Task.Run(() => RestorePhysicsAsync(_restoreCancellation.Token));
            }
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _stopping = true;
            _restoreCancellation.Cancel();
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

        public Task SetModeAsync(DisplayMode mode)
        {
            if (mode != DisplayMode.Playlist && mode != DisplayMode.Physics)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
            return ChangeModeAsync(mode, null, false);
        }

        public Task SetPhysicsModeAsync(PhysicsMode mode)
        {
            if (mode != PhysicsMode.ImuTest && mode != PhysicsMode.Ball && mode != PhysicsMode.Fluid)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
            return ChangeModeAsync(null, mode, false);
        }

        private async Task RestorePhysicsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!_stopping && State.PreferredMode == DisplayMode.Physics && _imu.State.Status == ImuStatus.Starting)
                {
                    await Task.Delay(100, cancellationToken);
                }

                if (!_stopping && State.PreferredMode == DisplayMode.Physics)
                {
                    await ChangeModeAsync(DisplayMode.Physics, null, true);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore Physics mode.");
                await _transition.WaitAsync();
                try
                {
                    if (!_stopping && !_restoreCancellation.IsCancellationRequested &&
                        State.PreferredMode == DisplayMode.Physics)
                    {
                        SetState(State with { Message = "Could not restore Physics mode. See the log for details." });
                    }
                }
                finally
                {
                    _transition.Release();
                }
            }
        }

        private async Task ChangeModeAsync(DisplayMode? requestedMode, PhysicsMode? requestedPhysics, bool restoring)
        {
            await _transition.WaitAsync();
            try
            {
                var previous = State;
                if (_stopping || (restoring &&
                    (_restoreCancellation.IsCancellationRequested || previous.PreferredMode != DisplayMode.Physics)))
                {
                    return;
                }

                var mode = requestedMode ?? previous.Mode;
                var physics = requestedPhysics ?? previous.Physics;
                var preferredMode = previous.PreferredMode;
                string? saveError = null;
                if (!restoring && requestedMode.HasValue)
                {
                    if (requestedMode.Value == DisplayMode.Playlist)
                    {
                        _restoreCancellation.Cancel();
                    }
                    if (_config.SaveMode(requestedMode.Value))
                    {
                        preferredMode = requestedMode.Value;
                    }
                    else
                    {
                        saveError = "Could not save the selected mode. See the log for details.";
                    }
                }
                if (!restoring && requestedPhysics.HasValue && !_config.SaveSimulation(physics))
                {
                    saveError = "Could not save the selected simulation. See the log for details.";
                }

                if (mode == previous.Mode && physics == previous.Physics)
                {
                    if (preferredMode != previous.PreferredMode || saveError != null)
                    {
                        SetState(previous with { Message = saveError, PreferredMode = preferredMode });
                    }
                    return;
                }

                if (mode == DisplayMode.Playlist)
                {
                    string? error = null;
                    if (previous.Mode == DisplayMode.Physics)
                    {
                        error = await StopPhysicsAsync();
                        if (!_stopping)
                        {
                            _mediaController.Resume();
                        }
                    }
                    SetState(new ModeState(DisplayMode.Playlist, physics, CombineMessages(error, saveError), preferredMode));
                    return;
                }

                IPhysicsRenderer? renderer = physics switch
                {
                    PhysicsMode.ImuTest => _imuTestRenderer,
                    PhysicsMode.Ball => _ballRenderer,
                    PhysicsMode.Fluid => _fluidRenderer,
                    _ => throw new ArgumentOutOfRangeException(nameof(requestedPhysics))
                };
                var imuStatus = _imu.State.Status;
                if (renderer == null || imuStatus != ImuStatus.Available)
                {
                    var message = renderer == null
                        ? "Physics is unavailable on this device."
                        : $"Physics is unavailable: IMU status is {imuStatus}.";
                    if (previous.Mode == DisplayMode.Physics)
                    {
                        await StopPhysicsAsync();
                        if (!_stopping)
                        {
                            _mediaController.Resume();
                        }
                    }
                    SetState(new ModeState(DisplayMode.Playlist, physics, CombineMessages(message, saveError), preferredMode));
                    return;
                }

                if (previous.Mode == DisplayMode.Physics)
                {
                    var error = await StopPhysicsAsync();
                    if (error != null)
                    {
                        if (!_stopping)
                        {
                            _mediaController.Resume();
                        }
                        SetState(new ModeState(DisplayMode.Playlist, physics, CombineMessages(error, saveError), preferredMode));
                        return;
                    }
                }
                else
                {
                    _mediaController.Suspend();
                }
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
                    task = Task.Run(() => renderer.RunAsync(token));
                }
                catch (Exception ex)
                {
                    cancellation?.Dispose();
                    _logger.LogError(ex, "Failed to start physics.");
                    if (!_stopping)
                    {
                        _mediaController.Resume();
                    }
                    SetState(new ModeState(DisplayMode.Playlist, physics,
                        CombineMessages("Could not start Physics.", saveError), preferredMode));
                    return;
                }

                _physicsCancellation = cancellation;
                _physicsTask = task;
                SetState(new ModeState(DisplayMode.Physics, physics, saveError, preferredMode));
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
                SetState(State with { Mode = DisplayMode.Playlist, Message = "Physics failed. Returned to Playlist." });
            }
            finally
            {
                _transition.Release();
            }
        }

        private static string? CombineMessages(string? first, string? second)
        {
            if (first == null) return second;
            return second == null ? first : $"{first} {second}";
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
