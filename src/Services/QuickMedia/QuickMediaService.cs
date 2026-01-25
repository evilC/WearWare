using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.MatrixConfig;
using WearWare.Services.MediaController;
using WearWare.Services.QuickMedia;
using WearWare.Services.StreamConverter;
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
    private readonly IStreamConverterService _streamConverterService;

    public QuickMediaService(ILogger<QuickMediaService> logger,
        MediaControllerService mediaController, 
        IQuickMediaButtonFactory buttonFactory,
        MatrixConfigService matrixConfigService,
        IStreamConverterService streamConverterService)
    {
        _logger = logger;
        _buttons = new IQuickMediaButton[_maxButtons];
        _mediaController = mediaController;
        _buttonFactory = buttonFactory;
        _matrixConfigService = matrixConfigService;
        _streamConverterService = streamConverterService;
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
    public async Task OnEditFormSubmit(IQuickMediaButton button, int itemIndex, PlayableItem originalItem, PlayableItem updatedItem, PlayableItemFormMode formMode)
    {
        /*
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
        */
            bool restartMediaController = false;
            // if (PlaylistIsPlaying(playlist))
            // {
                // Currently editing playlist and media controller is running, need to restart after removing item
                _mediaController.Stop();
                restartMediaController = true;
            // }
            if (formMode == PlayableItemFormMode.ADD)
            {
                // In ADD mode, the originalItem is from the library, so we need to set the ParentFolder of updatedItem
                updatedItem.ParentFolder = button.GetRelativePath();
            }

            if (originalItem.NeedsReConvert(updatedItem)){
                // If the updated item needs re-conversion, do it now
                var readFrom = formMode == PlayableItemFormMode.ADD
                        ? PathConfig.LibraryPath                // For ADD, source is library folder
                        : button.GetAbsolutePath();             // For EDIT, source is quickmedia folder
                var writeTo = button.GetAbsolutePath();         // For both ADD and EDIT, destination is quickmedia folder
                var result = await _streamConverterService.ConvertToStream(readFrom, updatedItem.SourceFileName, writeTo, updatedItem.Name, updatedItem.RelativeBrightness, updatedItem.MatrixOptions);
                if (result.ExitCode != 0)
                {
                    // Re-convert failed - show an alert and do not save changes
                    //await JSRuntime.InvokeVoidAsync("alert", $"Re-conversion failed: {result.Error} - {result.Message}");
                    // ToDo: Show error to user
                    return;
                }
            }
            else if (formMode == PlayableItemFormMode.ADD)
            {
                // If in ADD mode but no re-convert needed, we still need to copy the .stream from library to quickmedia folder
                var copyFrom = originalItem.GetStreamFilePath();    // From library folder
                var copyTo = updatedItem.GetStreamFilePath();       // To quickmedia folder
                File.Copy(copyFrom, copyTo, overwrite: true);
            }
            if (formMode == PlayableItemFormMode.ADD)
            {
                // Copy source file from library to quickmedia folder
                var copyFrom = originalItem.GetSourceFilePath();    // From library folder
                var copyTo = updatedItem.GetSourceFilePath();       // To quickmedia folder
                if (!File.Exists(copyTo)){
                    File.Copy(copyFrom, copyTo, overwrite: true);
                }
            }
            if (formMode == PlayableItemFormMode.ADD)
            {
                // Add new item
                //playlist.AddItem(itemIndex, updatedItem);
            }
            else
            {
                originalItem.UpdateFromClone(updatedItem);
                // if (!originalItem.Enabled)
                // {
                //     // If item is now disabled, and it is the current item, we need to move to the next item
                //     if (playlist.GetCurrentItem() == originalItem)
                //     {
                //         if (playlist.MoveNext() == null)
                //         {
                //             restartMediaController = false; // No more items to play
                //         }
                //     }
                // }
            }

            // button.Serialize();
            SerializeQuickMediaButton(button);
            // Restart media controller if there are still items to play
            // if (restartMediaController && playlist.GetCurrentItem() != null)
            if (restartMediaController)
            {
                _mediaController.Start();
            }

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