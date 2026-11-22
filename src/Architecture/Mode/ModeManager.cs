using WearWare.Utils;

namespace WearWare.Architecture.Mode
{
    public enum DisplayMode
    {
        Playlist,
        QuickMedia,
        Physics
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

        public async Task<bool> SetModeWithInterrupt(DisplayMode newMode)
        {
            var currentMode = State.Mode;
            if (currentMode == newMode)
                return true;

            var temporaryPath = $"{_modePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_modePath)!);
                var config = new ModeConfig
                {
                    Mode = newMode.ToString(),
                    InterruptedMode = currentMode.ToString()
                };
                JsonUtils.ToJsonFile(temporaryPath, config);
                File.Move(temporaryPath, _modePath, true);
                _logger.LogInformation("{tag} Set mode: {mode} (interrupted: {interrupted}).", _logTag, newMode, currentMode);
                UpdateState(new ModeState(newMode, null, State.PreferredMode));
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "{tag} Could not save interrupted mode configuration.", _logTag);
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

        public async Task<bool> RestoreInterruptedMode()
        {
            try
            {
                if (!File.Exists(_modePath))
                    return false;

                var json = File.ReadAllText(_modePath);
                var config = JsonUtils.FromJson<ModeConfig>(json);
                
                if (config?.InterruptedMode == null || !Enum.TryParse<DisplayMode>(config.InterruptedMode, out var mode) || !Enum.IsDefined(mode))
                {
                    _logger.LogWarning("{tag} No valid interrupted mode found, defaulting to Playlist.", _logTag);
                    return await SetModeWithoutInterrupt(DisplayMode.Playlist);
                }

                var temporaryPath = $"{_modePath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    var newConfig = new ModeConfig { Mode = mode.ToString(), InterruptedMode = null };
                    JsonUtils.ToJsonFile(temporaryPath, newConfig);
                    File.Move(temporaryPath, _modePath, true);
                    _logger.LogInformation("{tag} Restored mode: {mode}.", _logTag, mode);
                    UpdateState(new ModeState(mode, null, mode));
                    return true;
                }
                finally
                {
                    try
                    {
                        if (File.Exists(temporaryPath))
                            File.Delete(temporaryPath);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Could not restore interrupted mode.", _logTag);
                return false;
            }
        }

        private async Task<bool> SetModeWithoutInterrupt(DisplayMode mode)
        {
            var temporaryPath = $"{_modePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_modePath)!);
                var config = new ModeConfig { Mode = mode.ToString(), InterruptedMode = null };
                JsonUtils.ToJsonFile(temporaryPath, config);
                File.Move(temporaryPath, _modePath, true);
                _logger.LogInformation("{tag} Saved mode: {mode}.", _logTag, mode);
                UpdateState(new ModeState(mode, null, mode));
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
            public string? InterruptedMode { get; set; }
        }
    }
}
