using WearWare.Common.Media;
using WearWare.Utils;
using WearWare.Architecture.Button;
using System.Device.Gpio;

namespace WearWare.Architecture.QuickMedia
{
    record QuickMediaDto(int ButtonNumber, PlayableItem Item);
    public class QuickMediaManager : IManager
    {
        private readonly ILogger<QuickMediaManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ButtonManager _buttonManager;
        private readonly string _logTag = "[QUICKMEDIAMANAGER]";

        private Dictionary<int, QuickMediaItem?> _buttonSlotItems = [];
        private int _activeButtonSlot = 0;
        private QuickMediaStateConfig _stateConfig;

        public event Action? StateChanged;

        public int ActiveButtonSlot => _activeButtonSlot;
        public QuickMediaItem? ActiveButtonItem => _buttonSlotItems.TryGetValue(_activeButtonSlot, out var item) ? item : null;

        public QuickMediaManager(ILogger<QuickMediaManager> logger, ILoggerFactory loggerFactory, ButtonManager buttonManager)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _buttonManager = buttonManager;
            _stateConfig = QuickMediaStateConfig.Deserialize() ?? new QuickMediaStateConfig();
            _activeButtonSlot = _stateConfig.ActiveSlotNumber;
            _stateConfig.Serialize();
            _logger.LogInformation("{tag} initialized. Active button slot: {slot}.", _logTag, _activeButtonSlot);
        }

        /// <summary>
        /// Loads quick media items for all quickmedia buttons from disk.
        /// Button slots are determined by which buttons are assigned to QuickMedia function.
        /// </summary>
        public void LoadAllItems()
        {
            try
            {
                _buttonSlotItems.Clear();
                _logger.LogInformation("{tag} Loading quick media items from {path}...", _logTag, PathConfig.QuickMediaPath);

                var quickMediaButtons = _buttonManager.GetButtonsForFunction("QuickMedia").ToList();
                foreach (var button in quickMediaButtons)
                {
                    var buttonNumber = button.Index + 1;
                    var item = LoadButtonItem(buttonNumber);
                    _buttonSlotItems[buttonNumber] = item;
                }

                _logger.LogInformation("{tag} Loaded {count} quick media items for {buttonCount} buttons.", _logTag, _buttonSlotItems.Count, quickMediaButtons.Count);
                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error loading quick media items.", _logTag);
            }
        }

        /// <summary>
        /// Loads a single quick media item by button slot number (1-indexed).
        /// </summary>
        public QuickMediaItem? LoadButtonItem(int buttonSlot)
        {
            try
            {
                var buttonPath = Path.Combine(PathConfig.QuickMediaPath, buttonSlot.ToString());
                if (!Directory.Exists(buttonPath))
                {
                    _logger.LogWarning("{tag} Quick media folder for slot {slot} not found.", _logTag, buttonSlot);
                    _buttonSlotItems[buttonSlot] = null;
                    return null;
                }

                var configFile = Path.Combine(buttonPath, "quickmedia.json");
                if (!File.Exists(configFile))
                {
                    _logger.LogWarning("{tag} Quick media config for slot {slot} not found.", _logTag, buttonSlot);
                    _buttonSlotItems[buttonSlot] = null;
                    return null;
                }

                var json = File.ReadAllText(configFile);
                
                QuickMediaItem? item = null;
                try
                {
                    var dto = JsonUtils.FromJson<QuickMediaDto>(json);
                    if (dto?.Item != null)
                    {
                        item = QuickMediaItem.FromPlayableItem(dto.Item);
                        _logger.LogInformation("{tag} Loaded quick media item for slot {slot}.", _logTag, buttonSlot);
                    }
                }
                catch
                {
                    _logger.LogWarning("{tag} Failed to deserialize quick media item for slot {slot}.", _logTag, buttonSlot);
                    _buttonSlotItems[buttonSlot] = null;
                    return null;
                }

                if (item == null)
                {
                    _logger.LogWarning("{tag} Quick media item for slot {slot} is null.", _logTag, buttonSlot);
                    _buttonSlotItems[buttonSlot] = null;
                    return null;
                }

                _buttonSlotItems[buttonSlot] = item;
                _logger.LogInformation("{tag} Loaded quick media item for slot {slot}: {name}.", _logTag, buttonSlot, item.Name);
                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error loading quick media item for slot {slot}.", _logTag, buttonSlot);
                _buttonSlotItems[buttonSlot] = null;
                return null;
            }
        }

        /// <summary>
        /// Sets the active button slot (which item is currently playing).
        /// </summary>
        public bool SetActiveButton(int buttonSlot)
        {
            var quickMediaButtons = _buttonManager.GetButtonsForFunction("QuickMedia").ToList();
            var validButtonNumbers = quickMediaButtons.Select(b => b.Index + 1).ToList();
            
            if (!validButtonNumbers.Contains(buttonSlot))
            {
                _logger.LogWarning("{tag} Cannot set active button: button {button} is not assigned to QuickMedia.", _logTag, buttonSlot);
                return false;
            }

            if (!_buttonSlotItems.ContainsKey(buttonSlot) || _buttonSlotItems[buttonSlot] == null)
            {
                _logger.LogWarning("{tag} Cannot set active button: slot {slot} has no item loaded.", _logTag, buttonSlot);
                return false;
            }

            _activeButtonSlot = buttonSlot;
            _stateConfig.ActiveSlotNumber = buttonSlot;
            _stateConfig.Serialize();
            _logger.LogInformation("{tag} Active button slot set to {slot}.", _logTag, buttonSlot);
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Gets the item for a specific button slot.
        /// </summary>
        public PlayableItem? GetButtonItem(int buttonSlot)
        {
            if (!_buttonSlotItems.ContainsKey(buttonSlot))
                return null;
            var item = _buttonSlotItems[buttonSlot];
            return item?.ToPlayableItem();
        }

        /// <summary>
        /// Gets all button slots that have items loaded.
        /// </summary>
        public List<int> GetLoadedButtonSlots()
        {
            return _buttonSlotItems.Where(kvp => kvp.Value != null).Select(kvp => kvp.Key).ToList();
        }

        /// <summary>
        /// Gets the total number of quickmedia buttons configured.
        /// </summary>
        public int GetButtonCount()
        {
            var quickMediaButtons = _buttonManager.GetButtonsForFunction("QuickMedia").ToList();
            return quickMediaButtons.Count;
        }

        /// <summary>
        /// Gets the button numbers assigned to QuickMedia function.
        /// </summary>
        public List<int> GetQuickMediaButtonNumbers()
        {
            var quickMediaButtons = _buttonManager.GetButtonsForFunction("QuickMedia").ToList();
            return quickMediaButtons.Select(b => b.Index + 1).ToList();
        }

        /// <summary>
        /// Updates or adds a quick media item for a button slot.
        /// </summary>
        public bool UpsertButtonItem(int buttonSlot, PlayableItem item)
        {
            var quickMediaItem = QuickMediaItem.FromPlayableItem(item);
            var quickMediaButtons = _buttonManager.GetButtonsForFunction("QuickMedia").ToList();
            var validButtonNumbers = quickMediaButtons.Select(b => b.Index + 1).ToList();
            
            if (!validButtonNumbers.Contains(buttonSlot))
            {
                _logger.LogWarning("{tag} Cannot upsert item: button {button} is not assigned to QuickMedia.", _logTag, buttonSlot);
                return false;
            }

            try
            {
                var buttonPath = Path.Combine(PathConfig.QuickMediaPath, buttonSlot.ToString());
                Directory.CreateDirectory(buttonPath);

                // Update the item's ParentFolder to the full path
                item.ParentFolder = buttonPath;

                var configFile = Path.Combine(buttonPath, "quickmedia.json");
                // Save in the standard QuickMedia format
                var dto = new QuickMediaDto(buttonSlot, item);
                JsonUtils.ToJsonFile(configFile, dto);

                _buttonSlotItems[buttonSlot] = quickMediaItem;
                _logger.LogInformation("{tag} Upserted quick media item for slot {slot}.", _logTag, buttonSlot);
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error upserting quick media item for slot {slot}.", _logTag, buttonSlot);
                return false;
            }
        }

        /// <summary>
        /// Deletes the quick media item for a button slot.
        /// </summary>
        public bool DeleteButtonItem(int buttonSlot)
        {
            var quickMediaButtons = _buttonManager.GetButtonsForFunction("QuickMedia").ToList();
            var validButtonNumbers = quickMediaButtons.Select(b => b.Index + 1).ToList();
            
            if (!validButtonNumbers.Contains(buttonSlot))
            {
                _logger.LogWarning("{tag} Cannot delete item: button {button} is not assigned to QuickMedia.", _logTag, buttonSlot);
                return false;
            }

            try
            {
                var buttonPath = Path.Combine(PathConfig.QuickMediaPath, buttonSlot.ToString());
                if (Directory.Exists(buttonPath))
                {
                    Directory.Delete(buttonPath, recursive: true);
                }

                _buttonSlotItems[buttonSlot] = null;

                if (_activeButtonSlot == buttonSlot)
                {
                    _activeButtonSlot = 0;
                    _stateConfig.ActiveSlotNumber = 0;
                    _stateConfig.Serialize();
                }

                _logger.LogInformation("{tag} Deleted quick media item for slot {slot}.", _logTag, buttonSlot);
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error deleting quick media item for slot {slot}.", _logTag, buttonSlot);
                return false;
            }
        }
    }
}
