using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.Library;
using WearWare.Services.MediaController;
using WearWare.Services.QuickMedia;
using WearWare.Utils;

record QuickMediaDto(int ButtonNumber, PlayableItem Item);

public class QuickMediaService
{
    public event Action? StateChanged;
    private readonly IQuickMediaButton?[] _buttons;
    private readonly int _maxButtons = ButtonPins.PinNumbers.Count;
    private readonly MediaControllerService _mediaController;
    private readonly IQuickMediaButtonFactory _buttonFactory;
    private readonly ILogger<QuickMediaService> _logger;
    private readonly string _logTag = "[QUICKMEDIA]";

    public QuickMediaService(ILogger<QuickMediaService> logger, MediaControllerService mediaController, IQuickMediaButtonFactory buttonFactory)
    {
        _logger = logger;
        _buttons = new IQuickMediaButton[_maxButtons];
        _mediaController = mediaController;
        _buttonFactory = buttonFactory;
        _mediaController.StateChanged += OnMediaControllerStateChanged;
        // Instantiate buttons
        // Note that there seems to be an issue with GPIO pins floating after first boot
        // So these buttons will default to uninitialized, and will be initialized later
        // This is currently done after the MediaControllerService has been started
        for (int i = 0; i < _maxButtons; i++)
        {
            var button = DeserializeQuickMediaButton(i);
            if (button != null)
            {
                _buttons[i] = button;
            }
        }
        
        _logger.LogInformation("{tag} Initialized.", _logTag);
    }

    /// <summary>
    /// The MediaControllerService notified us that something changed
    /// This happens eg if an item failed to play and was disabled
    /// However, currently, as we have no Enabled checkbox on the QuickMedia page,
    /// The MediaControllerService does not notify us.
    /// So currently this even does nothing.
    /// Do not Remove it though, as the Mock service uses it to update its UI.
    /// </summary>
    private void OnMediaControllerStateChanged()
    {
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Initializes all Quick Media buttons.
    /// Do not call this until after the MediaControllerService has been started
    /// This will give floating buttons a chance to fire and be ignored
    /// </summary>
    public void Initialize()
    {
        for (int i = 0; i < _maxButtons; i++)
        {
            _buttons[i]?.Initialize();
        }
    }

    public IQuickMediaButton?[] GetQuickMediaButtons()
    {
        return _buttons;
    }

    public bool AddQuickMediaButton(int buttonNumber, LibraryItem libItem, PlayMode playMode, int playModeValue)
    {
        if (buttonNumber < 0 || buttonNumber >= _maxButtons) return false;
        if (_buttons[buttonNumber] != null) return false;

        var item = new PlayableItem(
            libItem.Name,
            Path.Combine(PathConfig.QuickMediaPath, buttonNumber.ToString()),
            libItem.MediaType,
            libItem.SourceFileName,
            playMode,
            playModeValue);
            try
        {
            var sourcePath = Path.Combine(PathConfig.LibraryPath, item.SourceFileName);
            var destPath = Path.Combine(PathConfig.QuickMediaPath, buttonNumber.ToString(), item.SourceFileName);
            if (!File.Exists(destPath)){
                File.Copy(sourcePath, destPath, overwrite: true);
            }
            destPath = Path.Combine(PathConfig.QuickMediaPath, buttonNumber.ToString(), $"{item.Name}.stream");
            if (!File.Exists(destPath)){
                sourcePath = Path.Combine(PathConfig.LibraryPath, $"{item.Name}.stream");
                File.Copy(sourcePath, destPath, overwrite: true);
            }
            var button = _buttonFactory.Create(_mediaController, buttonNumber, item);
            button.Initialize();
            _buttons[buttonNumber] = button;
            SerializeQuickMediaButton(button);
            StateChanged?.Invoke(); // Only used to notify the UI on the Mocks page
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{tag} Error adding Quick Media button for file {filename}: {message}", _logTag, libItem.SourceFileName, ex.Message);
            return false;
        }
    }

    public bool EditQuickMediaButton(IQuickMediaButton button, PlayMode playMode, int playModeValue)
    {
        if (button == null) return false;
        var restartMediaController = false;
        if (ButtonIsPlaying(button))
        {
            _mediaController.Stop();
            restartMediaController = true;
        }
        button.Item.PlayMode = playMode;
        button.Item.PlayModeValue = playModeValue;
        SerializeQuickMediaButton(button);
        if (restartMediaController)
        {
            _mediaController.Start();
        }
        StateChanged?.Invoke(); // Only used to notify the UI on the Mocks page
        return true;
    }

    public bool DeleteQuickMediaButton(int buttonNumber)
    {
        if (buttonNumber < 0 || buttonNumber >= _maxButtons) return false;
        var button = _buttons[buttonNumber];
        if (button == null) return false;
        var restartMediaController = false;
        if (ButtonIsPlaying(button))
        {
            // Stop media controller if it's playing the item associated with the button being deleted
            _mediaController.Stop();
            restartMediaController = true;
        }   

        // Delete associated files
        var dir = Path.Combine(PathConfig.QuickMediaPath, buttonNumber.ToString());
        if (Directory.Exists(dir))
        {
            File.Delete(Path.Combine(dir, "quickmedia.json"));
            File.Delete(Path.Combine(dir, button.Item.SourceFileName));
            File.Delete(Path.Combine(dir, $"{button.Item.Name}.stream"));
        }
        // Remove button
        button.Dispose();
        _buttons[buttonNumber] = null;

        if (restartMediaController)
        {
            _mediaController.Start();
        }
        StateChanged?.Invoke(); // Only used to notify the UI on the Mocks page
        return true;
    }

    public bool ButtonIsPlaying(IQuickMediaButton button)
    {
        return _mediaController.IsRunning() && _mediaController.IsCurrentItem(button.Item);
    }

    public void SerializeQuickMediaButton(IQuickMediaButton button)
    {
        var dir = Path.Combine(PathConfig.QuickMediaPath, button.ButtonNumber.ToString());
        Directory.CreateDirectory(dir);
        var outPath = Path.Combine(dir, "quickmedia.json");
        var dto = new QuickMediaDto(button.ButtonNumber, button.Item);
        JsonUtils.ToJsonFile(outPath, dto);
    }

    public IQuickMediaButton? DeserializeQuickMediaButton(int buttonNumber)
    {
        var path = Path.Combine(PathConfig.QuickMediaPath, buttonNumber.ToString(), "quickmedia.json");
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);  
        var dto = JsonUtils.FromJson<QuickMediaDto>(json);
        if (dto == null) return null;
        return _buttonFactory.Create(_mediaController, dto.ButtonNumber, dto.Item);
    }
}