using WearWare.Utils;

namespace WearWare.Services.Mode
{
    public enum DisplayMode
    {
        Playlist,
        QuickMedia,
        Physics
    }

    public sealed record ModeState(
        DisplayMode CurrentMode,
        DisplayMode? InterruptingMode,
        DisplayMode ActiveMode,
        string? Message);

    public sealed class ModeManager
    {
        private readonly ILogger<ModeManager> _logger;
        private readonly string _modePath = Path.Combine(PathConfig.ConfigPath, "mode-state.json");
        private ModeState _state;

        public event Action? StateChanged;
        public ModeState State => Volatile.Read(ref _state);

        public ModeManager(ILogger<ModeManager> logger)
        {
            _logger = logger;
            _state = LoadState();
        }

        public bool SaveState(ModeState state)
        {
            try
            {
                var persisted = new PersistedModeState
                {
                    CurrentMode = state.CurrentMode.ToString(),
                    InterruptingMode = state.InterruptingMode?.ToString()
                };
                JsonUtils.ToJsonFileAtomic(_modePath, persisted, _logger);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "[MODEMANAGER] Could not save mode state.");
                return false;
            }
        }

        public void UpdateState(ModeState state)
        {
            Volatile.Write(ref _state, state);
            StateChanged?.Invoke();
        }

        private ModeState LoadState()
        {
            var defaultState = new ModeState(DisplayMode.Playlist, null, DisplayMode.Playlist, null);
            if (!File.Exists(_modePath))
                return defaultState;

            try
            {
                var persisted = JsonUtils.FromJsonFile<PersistedModeState>(_modePath)
                    ?? throw new JsonException("Mode state contains null.");

                var currentMode = ParseNormalMode(persisted.CurrentMode
                    ?? throw new JsonException("Current mode is missing."));
                DisplayMode? interruptingMode = null;
                if (persisted.InterruptingMode != null)
                {
                    if (persisted.InterruptingMode != nameof(DisplayMode.QuickMedia))
                        throw new JsonException("Invalid interrupting mode.");
                    interruptingMode = DisplayMode.QuickMedia;
                }

                return new ModeState(currentMode, interruptingMode, interruptingMode ?? currentMode, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError(ex, "[MODEMANAGER] Could not load mode state from {path}; using defaults.", _modePath);
                return defaultState with { Message = "Could not load mode state. See the log for details." };
            }
        }

        private static DisplayMode ParseNormalMode(string value)
        {
            if (Enum.TryParse<DisplayMode>(value, out var mode) &&
                (mode == DisplayMode.Playlist || mode == DisplayMode.Physics))
                return mode;
            throw new JsonException($"Invalid current mode '{value}'.");
        }

        private sealed class PersistedModeState
        {
            public string? CurrentMode { get; set; }
            public string? InterruptingMode { get; set; }
        }
    }
}
