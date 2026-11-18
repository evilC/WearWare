using System.Text.Json;
using WearWare.Config;
using WearWare.Utils;

namespace WearWare.Services.Mode
{
    public sealed class ModeConfigService
    {
        private readonly ILogger<ModeConfigService> _logger;
        private readonly string _modePath = Path.Combine(PathConfig.ConfigPath, "modeconfig.json");
        private readonly string _simulationPath = Path.Combine(PathConfig.ConfigPath, "simulationconfig.json");

        public DisplayMode Mode { get; }
        public PhysicsMode Simulation { get; }
        public string? LoadError { get; private set; }

        public ModeConfigService(ILogger<ModeConfigService> logger)
        {
            _logger = logger;
            Mode = Load(_modePath, new ModeConfig { Mode = nameof(DisplayMode.Playlist) },
                config => config.Mode, DisplayMode.Playlist);
            Simulation = Load(_simulationPath, new SimulationConfig { Simulation = nameof(PhysicsMode.Ball) },
                config => config.Simulation, PhysicsMode.Ball);
        }

        public bool SaveMode(DisplayMode mode) => Save(_modePath, new ModeConfig { Mode = mode.ToString() });

        public bool SaveSimulation(PhysicsMode simulation) =>
            Save(_simulationPath, new SimulationConfig { Simulation = simulation.ToString() });

        private TEnum Load<TConfig, TEnum>(string path, TConfig defaults, Func<TConfig, string?> getName, TEnum fallback)
            where TConfig : class
            where TEnum : struct, Enum
        {
            try
            {
                if (!File.Exists(path))
                {
                    if (!Save(path, defaults))
                    {
                        LoadError = "Could not create mode configuration. See the log for details.";
                    }
                    return fallback;
                }

                var config = JsonUtils.FromJsonFile<TConfig>(path)
                    ?? throw new JsonException("Configuration contains null.");
                var name = getName(config);
                if (name == null || !Enum.TryParse<TEnum>(name, out var value) ||
                    !Enum.IsDefined(value) || name != value.ToString())
                {
                    throw new JsonException($"Invalid {typeof(TEnum).Name} name '{name}'.");
                }

                return value;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError(ex, "Could not load configuration from {path}; using {fallback}.", path, fallback);
                LoadError = "Could not load mode configuration. See the log for details.";
                return fallback;
            }
        }

        private bool Save<T>(string path, T config)
        {
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                JsonUtils.ToJsonFile(temporaryPath, config);
                File.Move(temporaryPath, path, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Could not save configuration to {path}.", path);
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Could not remove temporary configuration file {path}.", temporaryPath);
                }
            }
        }

        private sealed class ModeConfig
        {
            public string? Mode { get; set; }
        }

        private sealed class SimulationConfig
        {
            public string? Simulation { get; set; }
        }
    }
}
