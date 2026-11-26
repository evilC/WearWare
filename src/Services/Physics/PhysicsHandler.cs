using WearWare.Services.Button;
using WearWare.Services.Physics.Imu;
using WearWare.Services.Physics.Simulations;
using WearWare.Services.Physics.Simulations.Ball;
using WearWare.Services.Physics.Simulations.AcidSmiley;
using WearWare.Services.Physics.Simulations.Fluid;
using WearWare.Services.Physics.Simulations.ImuTest;
using WearWare.Utils;

namespace WearWare.Services.Physics
{
    public sealed class PhysicsHandler : IHandler
    {
        private readonly record struct Simulation(string Name, IPhysicsRenderer Renderer);

        private readonly string _selectionPath = Path.Combine(PathConfig.ConfigPath, "physics-state.json");
        private readonly ImuService _imu;
        private readonly ILogger<PhysicsHandler> _logger;
        private readonly Simulation[] _simulations;
        private PhysicsState _physicsState;
        private volatile HandlerState _state = HandlerState.Idle;

        public string Name => "Physics";
        public HandlerState State => _state;
        public IReadOnlyList<string> Simulations { get; }
        public string SelectedSimulation => Volatile.Read(ref _physicsState).Simulation;
        public string? SelectionMessage { get; private set; }
        public event Action? StateChanged;

        public PhysicsHandler(
            ImuService imu,
            FluidRenderer fluid,
            BallPhysicsRenderer ball,
            ImuTestRenderer imuTest,
            AcidSmileyRenderer acidSmiley,
            ILogger<PhysicsHandler> logger)
        {
            _imu = imu;
            _logger = logger;
            _simulations =
            [
                new("Fluid", fluid),
                new("Ball", ball),
                new("IMU Test", imuTest),
                new("Acid Smiley", acidSmiley)
            ];
            Simulations = Array.AsReadOnly(_simulations.Select(simulation => simulation.Name).ToArray());
            _physicsState = LoadState();
        }

        public async Task WaitForImuAsync(CancellationToken cancellationToken)
        {
            while (_imu.State.Status == ImuStatus.Starting)
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            if (_imu.State.Status != ImuStatus.Available)
                throw new InvalidOperationException($"IMU is unavailable: {_imu.State.Status}.");
        }

        internal bool SelectSimulation(string name)
        {
            if (!Simulations.Contains(name))
                throw new ArgumentOutOfRangeException(nameof(name));

            return SaveState(new PhysicsState
            {
                Simulation = name,
                ExcludedFromJog = Volatile.Read(ref _physicsState).ExcludedFromJog
            }, "simulation selection");
        }

        public bool IsJogEnabled(string name) => !Volatile.Read(ref _physicsState).ExcludedFromJog.Contains(name);

        internal void SetJogEnabled(string name, bool enabled)
        {
            if (!Simulations.Contains(name))
                throw new ArgumentOutOfRangeException(nameof(name));

            var state = Volatile.Read(ref _physicsState);
            var excluded = state.ExcludedFromJog.Where(simulation => simulation != name);
            SaveState(new PhysicsState
            {
                Simulation = state.Simulation,
                ExcludedFromJog = (enabled ? excluded : excluded.Append(name)).ToArray()
            }, "button navigation settings");
        }

        private bool SaveState(PhysicsState state, string description)
        {
            try
            {
                JsonUtils.ToJsonFileAtomic(_selectionPath, state, _logger);
                Volatile.Write(ref _physicsState, state);
                SelectionMessage = null;
                NotifyStateChanged();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "[PHYSICSHANDLER] Could not save {description}.", description);
                SelectionMessage = $"Could not save {description}. See the log for details.";
                NotifyStateChanged();
                return false;
            }
        }

        internal string? GetJoggedSimulation(JogDirection direction)
        {
            var state = Volatile.Read(ref _physicsState);
            var index = Array.FindIndex(_simulations, simulation => simulation.Name == state.Simulation);
            if (index < 0)
                throw new InvalidOperationException("The selected Physics simulation is not available.");

            var offset = direction switch
            {
                JogDirection.Next => 1,
                JogDirection.Previous => -1,
                _ => throw new ArgumentOutOfRangeException(nameof(direction))
            };
            for (var i = 0; i < _simulations.Length; i++)
            {
                index = (index + offset + _simulations.Length) % _simulations.Length;
                var name = _simulations[index].Name;
                if (!state.ExcludedFromJog.Contains(name))
                    return name;
            }
            return null;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                SetState(HandlerState.Running);
                var renderer = _simulations.First(simulation => simulation.Name == SelectedSimulation).Renderer;
                await renderer.RunAsync(cancellationToken);
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
        }

        private PhysicsState LoadState()
        {
            if (!File.Exists(_selectionPath))
                return new PhysicsState { Simulation = Simulations[0] };

            try
            {
                var state = JsonUtils.FromJsonFile<PhysicsState>(_selectionPath)
                    ?? throw new JsonException("Physics state contains null.");
                if (state.Simulation == null || !Simulations.Contains(state.Simulation))
                    throw new JsonException($"Invalid Physics simulation '{state.Simulation}'.");
                if (state.ExcludedFromJog == null || state.ExcludedFromJog.Any(name => name == null))
                    throw new JsonException("Invalid Physics button navigation settings.");
                return state;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError(ex, "[PHYSICSHANDLER] Could not load Physics state from {path}; using Fluid.", _selectionPath);
                SelectionMessage = "Could not load Physics state. Using Fluid. See the log for details.";
                return new PhysicsState { Simulation = Simulations[0] };
            }
        }

        private void SetState(HandlerState state)
        {
            if (_state == state)
                return;

            _state = state;
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
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

        private sealed class PhysicsState
        {
            public string Simulation { get; set; } = string.Empty;
            public string[] ExcludedFromJog { get; set; } = [];
        }
    }
}
