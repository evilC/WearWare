using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Services.MediaController;
using WearWare.Services.OperationProgress;
using WearWare.Services.QuickMedia;
using WearWare.Utils;

record QuickMediaDto(int ButtonNumber, PlayableItem Item);

public class QuickMediaService
{
    public event Action? StateChanged;
    private readonly IQuickMediaButton?[] _buttons;
    private readonly IReadOnlyList<int> _buttonPins;
    private readonly MediaControllerService _mediaController;
    private readonly IQuickMediaButtonFactory _buttonFactory;
    private readonly ILogger<QuickMediaService> _logger;
    private readonly string _logTag = "[QUICKMEDIA]";
    private static readonly string _configFileName = "quickmedia.json";
    private readonly IOperationProgressService _operationProgress;

    public QuickMediaService(ILogger<QuickMediaService> logger,
        MediaControllerService mediaController, 
        IQuickMediaButtonFactory buttonFactory,
        IOperationProgressService operationProgress
    )
    {
        _logger = logger;
        _buttonPins = GetButtonPins();
        _buttons = new IQuickMediaButton[_buttonPins.Count];
        _mediaController = mediaController;
        _buttonFactory = buttonFactory;
        _mediaController.StateChanged += OnMediaControllerStateChanged;
        _operationProgress = operationProgress;
        // Instantiate buttons
        // Note that there seems to be an issue with GPIO pins floating after first boot
        // So these buttons will default to uninitialized, and will be initialized later
        // This is currently done after the MediaControllerService has been started
        for (int i = 0; i < _buttonPins.Count; i++)
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
    /// Loads the button pin configuration from file, or creates a new one with an empty list if it doesn't exist, or is invalid
    /// </summary>
    /// <returns>A list of button pins</returns>
    private IReadOnlyList<int> GetButtonPins()
    {
        IReadOnlyList<int> buttonPins;
        var configFilePath = Path.Combine(PathConfig.ConfigPath, "buttonpins.json");
        if (File.Exists(configFilePath)){
            try
            {
                buttonPins = JsonUtils.FromJsonFile<IReadOnlyList<int>>(configFilePath) ?? [];
                if (buttonPins != null) return buttonPins;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{tag} Error reading button pins configuration from file {filename}: {message}", _logTag, configFilePath, ex.Message);
            }
        }
        // My default: 2, 3, 16, 14
        buttonPins = [];
        JsonUtils.ToJsonFile(configFilePath, buttonPins);
        return buttonPins;
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

    /// <summary>
    /// Called when OK is clicked in the EditPlayableItemForm
    /// </summary>
    /// <param name="itemIndex"></param> The index of the item being edited
    /// This is not used any more, but keep for now.
    /// The equivalent method in QuickMediaService still uses it.
    /// <param name="originalItem"></param> The original item before editing
    /// <param name="updatedItem"></param> The updated item from the form
    /// <param name="formMode"></param> The mode of the form (ADD or EDIT)
    /// </summary>
    public async Task OnEditFormSubmit(EditPlayableItemFormModel formModel)
    {
        IQuickMediaButton button;
        var opId = await _operationProgress.StartOperation($"{(formModel.FormMode == EditPlayableItemFormMode.Add ? "Adding" : "Editing")} Quick Media Item");
        if (formModel.FormMode == EditPlayableItemFormMode.Edit)
        {
            var tmp = _buttons[formModel.ItemIndex];
            if (formModel.ItemIndex < 0 || formModel.ItemIndex >= _buttonPins.Count || tmp == null)
            {
                _operationProgress.CompleteOperation(opId, false, "Error: Quick Media button not found for editing.");
                return; // ToDo: Error handling
            } 
            button = tmp;
        }
        else
        {
            // Create new button
            try
            {
                _operationProgress.ReportProgress(opId, "Creating folder");
                if (!Directory.Exists(GetQuickMediaPath(formModel.ItemIndex)))
                {
                    Directory.CreateDirectory(GetQuickMediaPath(formModel.ItemIndex));
                }
                _operationProgress.ReportProgress(opId, "Creating button");
                button = _buttonFactory.Create(_mediaController, formModel.ItemIndex, _buttonPins[formModel.ItemIndex], formModel.UpdatedItem);
            }
            catch (Exception ex)
            {
                _operationProgress.CompleteOperation(opId, false, "Error creating Quick Media button: " + ex.Message);
                _logger.LogError(ex, "{tag} Error adding Quick Media button for file {filename}: {message}", _logTag, formModel.UpdatedItem.SourceFileName, ex.Message);
                return;
            }
        }
        if (formModel.FormMode == EditPlayableItemFormMode.Add)
        {
            // In ADD mode, the originalItem is from the library, so we need to set the ParentFolder of updatedItem
            formModel.UpdatedItem.ParentFolder = button.GetRelativePath();
        }

        if (formModel.FormMode == EditPlayableItemFormMode.Add)
        {
            try
            {
                _operationProgress.ReportProgress(opId, "Copying fseq file");
                var copyFrom = formModel.OriginalItem.GetFseqFilePath();    // From library folder
                var copyTo = formModel.UpdatedItem.GetFseqFilePath();       // To quickmedia folder
                await FileUtils.CopyFileAsync(copyFrom, copyTo).ConfigureAwait(false);
            }
            catch
            {
                _operationProgress.CompleteOperation(opId, false, "Error copying fseq file from library to Quick Media folder.");
                return;
            }
        }
        if (formModel.FormMode == EditPlayableItemFormMode.Add)
        {
            try
            {
                _operationProgress.ReportProgress(opId, "Copying source file");
                // Copy source file from library to quickmedia folder
                var copyFrom = formModel.OriginalItem.GetSourceFilePath();    // From library folder
                var copyTo = formModel.UpdatedItem.GetSourceFilePath();       // To quickmedia folder
                if (!File.Exists(copyTo)){
                    await FileUtils.CopyFileAsync(copyFrom, copyTo).ConfigureAwait(false);
                }
            }
            catch
            {
                _operationProgress.CompleteOperation(opId, false, "Error copying source file from library to Quick Media folder.");
                return;
            }
        }
        if (formModel.FormMode == EditPlayableItemFormMode.Add)
        {
            _operationProgress.ReportProgress(opId, "Adding button to collection");
            _buttons[formModel.ItemIndex] = button;
        }
        else
        {
            _operationProgress.ReportProgress(opId, "Updating item metadata");
            formModel.OriginalItem.UpdateFromClone(formModel.UpdatedItem);
        }
        try
        {
            _operationProgress.ReportProgress(opId, "Saving configuration");
            SerializeQuickMediaButton(button);
        }
        catch
        {
            _operationProgress.CompleteOperation(opId, false, "Error writing Quick Media button configuration.");
            return;
        }
        _operationProgress.CompleteOperation(opId, true, "Done");
        StateChanged?.Invoke(); // Only used to notify the UI on the Mocks page
    }

    public bool DeleteQuickMediaButton(int buttonNumber)
    {
        if (buttonNumber < 0 || buttonNumber >= _buttonPins.Count) return false;
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
            var button = _buttonFactory.Create(_mediaController, dto.ButtonNumber, _buttonPins[dto.ButtonNumber], dto.Item);
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