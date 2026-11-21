using WearWare.Utils;

namespace WearWare.Architecture.Mode
{
    public enum DisplayMode
    {
        Playlist,
        QuickMedia
    }

    public sealed record ModeState(DisplayMode Mode, string? Message, DisplayMode PreferredMode);

    public sealed class ModeManager
    {
        private readonly ILogger<ModeManager> _logger;
        private readonly string _logTag = "[MODEMANAGER]";
        private readonly string _modePath = Path.Combine(PathConfig.ConfigPath, "modeconfig.json");
        private ModeState _state;

        public event Action? StateChanged;
        public ModeState State => Volatile.Read(ref _state);

        public ModeManager(ILogger<ModeManager> logger)
        {
            _logger = logger;
            var mode = LoadMode();
            _state = new ModeState(mode, null, mode);
        }

        public bool SaveMode(DisplayMode mode)
        {
            var temporaryPath = $"{_modePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_modePath)!);
                var config = new ModeConfig { Mode = mode.ToString() };
                JsonUtils.ToJsonFile(temporaryPath, config);
                File.Move(temporaryPath, _modePath, true);
                _logger.LogInformation("{tag} Saved mode: {mode}.", _logTag, mode);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "{tag} Could not save mode configuration.", _logTag);
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "{tag} Could not remove temporary configuration file {path}.", _logTag, temporaryPath);
                }
            }
        }

        private DisplayMode LoadMode()
        {
            try
            {
                if (!File.Exists(_modePath))
                {
                    var config = new ModeConfig { Mode = nameof(DisplayMode.Playlist) };
                    SaveMode(DisplayMode.Playlist);
                    return DisplayMode.Playlist;
                }

                var json = File.ReadAllText(_modePath);
                var loadedConfig = JsonUtils.FromJson<ModeConfig>(json);
                if (loadedConfig?.Mode == null || !Enum.TryParse<DisplayMode>(loadedConfig.Mode, out var mode) || !Enum.IsDefined(mode))
                {
                    _logger.LogWarning("{tag} Invalid mode in config, using Playlist.", _logTag);
                    return DisplayMode.Playlist;
                }

                _logger.LogInformation("{tag} Loaded mode: {mode}.", _logTag, mode);
                return mode;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                _logger.LogError(ex, "{tag} Could not load mode configuration, using Playlist.", _logTag);
                return DisplayMode.Playlist;
            }
        }

        public void UpdateState(ModeState newState)
        {
            Volatile.Write(ref _state, newState);
            StateChanged?.Invoke();
        }

        private sealed class ModeConfig
        {
            public string? Mode { get; set; }
        }
    }
}
