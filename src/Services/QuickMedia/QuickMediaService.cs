using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.MatrixConfig;
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
    private static readonly string _configFileName = "quickmedia.json";
    private readonly MatrixConfigService _matrixConfigService;

    public QuickMediaService(ILogger<QuickMediaService> logger,
        MediaControllerService mediaController, 
        IQuickMediaButtonFactory buttonFactory,
        MatrixConfigService matrixConfigService)
    {
        _logger = logger;
        _buttons = new IQuickMediaButton[_maxButtons];
        _mediaController = mediaController;
        _buttonFactory = buttonFactory;
        _matrixConfigService = matrixConfigService;
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
        // Nothing to do
        // Do not delete this method though, as Program.cs calls it to ensure that the QuickMedia service is loaded.
        // Without this, QuickMedia buttons will not be enabled until we visit the QuickMedia page
    }

    public IQuickMediaButton?[] GetQuickMediaButtons()
    {
        return _buttons;
    }

    public bool AddQuickMediaButton(int buttonNumber, PlayableItem libItem, PlayMode playMode, int playModeValue, int relativeBrightness, int currentBrightness)
    {
        if (buttonNumber < 0 || buttonNumber >= _maxButtons) return false;
        if (_buttons[buttonNumber] != null) return false;

        var qmItem = new PlayableItem(
            libItem.Name,
            GetQuickMediaPath(buttonNumber),
            libItem.MediaType,
            libItem.SourceFileName,
            playMode,
            playModeValue,
            relativeBrightness,
            currentBrightness,
            _matrixConfigService.CloneOptions() // ToDo: Use the options that the form returned
        );
            try
        {
            if (!Directory.Exists(GetQuickMediaPath(buttonNumber)))
            {
                Directory.CreateDirectory(GetQuickMediaPath(buttonNumber));
            }
            if (!File.Exists(qmItem.GetSourceFilePath())){
                File.Copy(libItem.GetSourceFilePath(), qmItem.GetSourceFilePath(), overwrite: true);
            }
            if (!File.Exists(qmItem.GetStreamFilePath())){
                File.Copy(libItem.GetStreamFilePath(), qmItem.GetStreamFilePath(), overwrite: true);
            }
            var button = _buttonFactory.Create(_mediaController, buttonNumber, qmItem);
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

    /// <summary>
    /// Called when OK is clicked in the EditPlayableItemForm
    /// </summary>
    /// <param name="button"></param> The button being edited
    /// <param name="itemIndex"></param> The index of the item being edited
    /// This is not used any more, but keep for now.
    /// The equivalent method in QuickMediaService still uses it.
    /// <param name="originalItem"></param> The original item before editing
    /// <param name="updatedItem"></param> The updated item from the form
    /// <param name="formMode"></param> The mode of the form (ADD or EDIT)
    /// </summary>
    public bool OnEditFormSubmit(IQuickMediaButton button, PlayMode playMode, int playModeValue, int relativeBrightness, int currentBrightness)
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
        button.Item.RelativeBrightness = relativeBrightness;
        button.Item.CurrentBrightness = currentBrightness;
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
        var dir = GetQuickMediaPath(buttonNumber);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
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
        Directory.CreateDirectory(GetQuickMediaPath(button.ButtonNumber));
        var dto = new QuickMediaDto(button.ButtonNumber, button.Item);
        JsonUtils.ToJsonFile(GetQuickMediaConfigFilePath(button.ButtonNumber), dto);
    }

    public IQuickMediaButton? DeserializeQuickMediaButton(int buttonNumber)
    {
        var path = GetQuickMediaConfigFilePath(buttonNumber);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);  
            var dto = JsonUtils.FromJson<QuickMediaDto>(json);
            if (dto == null) return null;
            var button = _buttonFactory.Create(_mediaController, dto.ButtonNumber, dto.Item);
            // Older JSON may not include MatrixOptions; ensure it's initialized so code relying on it won't see null.
            if (button.Item.MatrixOptions == null)
                button.Item.MatrixOptions = _matrixConfigService.CloneOptions();
            return button;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{tag} Error deserializing Quick Media button from file {filename}: {message}", _logTag, path, ex.Message);
            return null;
        }
    }

    public string GetQuickMediaPath(int buttonNumber)
    {
        return Path.Combine(PathConfig.QuickMediaPath, buttonNumber.ToString());
    }

    public string GetQuickMediaConfigFilePath(int buttonNumber)
    {
        return Path.Combine(GetQuickMediaPath(buttonNumber), _configFileName);
    }
}