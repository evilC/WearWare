using WearWare.Common.Media;
using WearWare.Services.QuickMedia;
using WearWare.Utils;

namespace WearWare.Architecture.QuickMedia
{
    record QuickMediaDto(int ButtonNumber, PlayableItem Item);
    public class QuickMediaManager : IManager
    {
        private readonly ILogger<QuickMediaManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _logTag = "[QUICKMEDIAMANAGER]";

        private Dictionary<int, PlayableItem?> _buttonItems = [];
        private int _activeButtonNumber = 0;
        private IReadOnlyList<int> _buttonPins = [];
        private QuickMediaStateConfig _stateConfig;

        public event Action? StateChanged;

        public int ActiveButtonNumber => _activeButtonNumber;
        public PlayableItem? ActiveButtonItem => _buttonItems.TryGetValue(_activeButtonNumber, out var item) ? item : null;

        public QuickMediaManager(ILogger<QuickMediaManager> logger, ILoggerFactory loggerFactory, QuickMediaButtonsConfigService buttonsConfig)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _buttonPins = buttonsConfig.Config.ButtonPins ?? [];
            _stateConfig = QuickMediaStateConfig.Deserialize() ?? new QuickMediaStateConfig();
            _activeButtonNumber = _stateConfig.ActiveButtonNumber;
            _stateConfig.Serialize();
            _logger.LogInformation("{tag} initialized with {count} buttons. Active button: {button}.", _logTag, _buttonPins.Count, _activeButtonNumber);
        }

        /// <summary>
        /// Loads quick media items for all buttons from disk
        /// </summary>
        public void LoadAllItems()
        {
            try
            {
                _buttonItems.Clear();
                _logger.LogInformation("{tag} Loading quick media items from {path}...", _logTag, PathConfig.QuickMediaPath);

                for (int i = 0; i < _buttonPins.Count; i++)
                {
                    var item = LoadButtonItem(i);
                    _buttonItems[i] = item;
                }

                _logger.LogInformation("{tag} Loaded {count} quick media items.", _logTag, _buttonItems.Count);
                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error loading quick media items.", _logTag);
            }
        }

        /// <summary>
        /// Loads a single quick media item by button number
        /// </summary>
        public PlayableItem? LoadButtonItem(int buttonNumber)
        {
            try
            {
                var buttonPath = Path.Combine(PathConfig.QuickMediaPath, buttonNumber.ToString());
                if (!Directory.Exists(buttonPath))
                {
                    _logger.LogWarning("{tag} Quick media folder for button {button} not found.", _logTag, buttonNumber);
                    _buttonItems[buttonNumber] = null;
                    return null;
                }

                var configFile = Path.Combine(buttonPath, "quickmedia.json");
                if (!File.Exists(configFile))
                {
                    _logger.LogWarning("{tag} Quick media config for button {button} not found.", _logTag, buttonNumber);
                    _buttonItems[buttonNumber] = null;
                    return null;
                }

                var json = File.ReadAllText(configFile);
                var dto = JsonUtils.FromJson<QuickMediaDto>(json);
                if (dto == null)
                {
                    _logger.LogWarning("{tag} Failed to deserialize quick media item for button {button}.", _logTag, buttonNumber);
                    _buttonItems[buttonNumber] = null;
                    return null;
                }

                var item = dto.Item;
                _buttonItems[buttonNumber] = item;
                _logger.LogInformation("{tag} Loaded quick media item for button {button}: {name}.", _logTag, buttonNumber, item.Name);
                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error loading quick media item for button {button}.", _logTag, buttonNumber);
                _buttonItems[buttonNumber] = null;
                return null;
            }
        }

        /// <summary>
        /// Sets the active button (which item is currently playing)
        /// </summary>
        public bool SetActiveButton(int buttonNumber)
        {
            if (buttonNumber < 0 || buttonNumber >= _buttonPins.Count)
            {
                _logger.LogWarning("{tag} Cannot set active button: {button} out of range.", _logTag, buttonNumber);
                return false;
            }

            if (!_buttonItems.ContainsKey(buttonNumber) || _buttonItems[buttonNumber] == null)
            {
                _logger.LogWarning("{tag} Cannot set active button: button {button} has no item loaded.", _logTag, buttonNumber);
                return false;
            }

            _activeButtonNumber = buttonNumber;
            _stateConfig.ActiveButtonNumber = buttonNumber;
            _stateConfig.Serialize();
            _logger.LogInformation("{tag} Active button set to {button}.", _logTag, buttonNumber);
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Gets the item for a specific button
        /// </summary>
        public PlayableItem? GetButtonItem(int buttonNumber)
        {
            if (!_buttonItems.ContainsKey(buttonNumber))
                return null;
            return _buttonItems[buttonNumber];
        }

        /// <summary>
        /// Gets all button numbers that have items loaded
        /// </summary>
        public List<int> GetLoadedButtonNumbers()
        {
            return _buttonItems.Where(kvp => kvp.Value != null).Select(kvp => kvp.Key).ToList();
        }

        /// <summary>
        /// Gets the total number of buttons
        /// </summary>
        public int GetButtonCount() => _buttonPins.Count;

        /// <summary>
        /// Updates or adds a quick media item for a button
        /// </summary>
        public bool UpsertButtonItem(int buttonNumber, PlayableItem item)
        {
            if (buttonNumber < 0 || buttonNumber >= _buttonPins.Count)
            {
                _logger.LogWarning("{tag} Cannot upsert item: button {button} out of range.", _logTag, buttonNumber);
                return false;
            }

            try
            {
                var buttonPath = Path.Combine(PathConfig.QuickMediaPath, buttonNumber.ToString());
                Directory.CreateDirectory(buttonPath);

                var configFile = Path.Combine(buttonPath, "quickmedia.json");
                var dto = new QuickMediaDto(buttonNumber, item);
                JsonUtils.ToJsonFile(configFile, dto);

                _buttonItems[buttonNumber] = item;
                _logger.LogInformation("{tag} Upserted quick media item for button {button}.", _logTag, buttonNumber);
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error upserting quick media item for button {button}.", _logTag, buttonNumber);
                return false;
            }
        }

        /// <summary>
        /// Deletes the quick media item for a button
        /// </summary>
        public bool DeleteButtonItem(int buttonNumber)
        {
            if (buttonNumber < 0 || buttonNumber >= _buttonPins.Count)
            {
                _logger.LogWarning("{tag} Cannot delete item: button {button} out of range.", _logTag, buttonNumber);
                return false;
            }

            try
            {
                var buttonPath = Path.Combine(PathConfig.QuickMediaPath, buttonNumber.ToString());
                if (Directory.Exists(buttonPath))
                {
                    Directory.Delete(buttonPath, recursive: true);
                }

                _buttonItems[buttonNumber] = null;

                if (_activeButtonNumber == buttonNumber)
                {
                    _activeButtonNumber = 0;
                    _stateConfig.ActiveButtonNumber = 0;
                    _stateConfig.Serialize();
                }

                _logger.LogInformation("{tag} Deleted quick media item for button {button}.", _logTag, buttonNumber);
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error deleting quick media item for button {button}.", _logTag, buttonNumber);
                return false;
            }
        }
    }
}
