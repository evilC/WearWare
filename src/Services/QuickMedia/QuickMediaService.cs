using Iot.Device.ExplorerHat;
using WearWare.Common.Media;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Config;
using WearWare.Services.MatrixConfig;
using WearWare.Services.MediaController;
using WearWare.Services.OperationProgress;
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
    private readonly IOperationProgressService _operationProgress;

    public QuickMediaService(ILogger<QuickMediaService> logger,
        MediaControllerService mediaController, 
        IQuickMediaButtonFactory buttonFactory,
        MatrixConfigService matrixConfigService,
        IStreamConverterService streamConverterService,
        IOperationProgressService operationProgress
    )
    {
        _logger = logger;
        _buttons = new IQuickMediaButton[_maxButtons];
        _mediaController = mediaController;
        _buttonFactory = buttonFactory;
        _matrixConfigService = matrixConfigService;
        _streamConverterService = streamConverterService;
        _mediaController.StateChanged += OnMediaControllerStateChanged;
        _operationProgress = operationProgress;
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
    public async Task OnEditFormSubmit(int itemIndex, PlayableItem originalItem, PlayableItem updatedItem, EditPlayableItemFormMode formMode)
    {
        IQuickMediaButton button;
        var opId = await _operationProgress.StartOperation($"{(formMode == EditPlayableItemFormMode.Add ? "Adding" : "Editing")} Quick Media Item");
        if (formMode == EditPlayableItemFormMode.Edit)
        {
            var tmp = _buttons[itemIndex];
            if (itemIndex < 0 || itemIndex >= _maxButtons || tmp == null)
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
                if (!Directory.Exists(GetQuickMediaPath(itemIndex)))
                {
                    Directory.CreateDirectory(GetQuickMediaPath(itemIndex));
                }
                _operationProgress.ReportProgress(opId, "Creating button");
                button = _buttonFactory.Create(_mediaController, itemIndex, updatedItem);
            }
            catch (Exception ex)
            {
                _operationProgress.CompleteOperation(opId, false, "Error creating Quick Media button: " + ex.Message);
                _logger.LogError(ex, "{tag} Error adding Quick Media button for file {filename}: {message}", _logTag, updatedItem.SourceFileName, ex.Message);
                return;
            }
        }
        if (formMode == EditPlayableItemFormMode.Add)
        {
            // In ADD mode, the originalItem is from the library, so we need to set the ParentFolder of updatedItem
            updatedItem.ParentFolder = button.GetRelativePath();
        }

        if (originalItem.NeedsReConvert(updatedItem)){
            _operationProgress.ReportProgress(opId, "Converting stream");
            // If the updated item needs re-conversion, do it now
            var readFrom = formMode == EditPlayableItemFormMode.Add
                    ? PathConfig.LibraryPath                // For ADD, source is library folder
                    : button.GetAbsolutePath();             // For EDIT, source is quickmedia folder
            var writeTo = button.GetAbsolutePath();         // For both ADD and EDIT, destination is quickmedia folder
            var result = await _streamConverterService.ConvertToStream(readFrom, updatedItem.SourceFileName, writeTo, updatedItem.Name, updatedItem.RelativeBrightness, updatedItem.MatrixOptions);
            if (result.ExitCode != 0)
            {
                // Re-convert failed - show an alert and do not save changes
                _operationProgress.CompleteOperation(opId, false, result.Message + "\n" + result.Error);
                return;
            }
        }
        else if (formMode == EditPlayableItemFormMode.Add)
        {
            try
            {
                _operationProgress.ReportProgress(opId, "Copying stream file");
                // If in ADD mode but no re-convert needed, we still need to copy the .stream from library to quickmedia folder
                var copyFrom = originalItem.GetStreamFilePath();    // From library folder
                var copyTo = updatedItem.GetStreamFilePath();       // To quickmedia folder
                File.Copy(copyFrom, copyTo, overwrite: true);
            }
            catch
            {
                _operationProgress.CompleteOperation(opId, false, "Error copying stream file from library to Quick Media folder.");
                return;
            }
        }
        if (formMode == EditPlayableItemFormMode.Add)
        {
            try
            {
                _operationProgress.ReportProgress(opId, "Copying source file");
                // Copy source file from library to quickmedia folder
                var copyFrom = originalItem.GetSourceFilePath();    // From library folder
                var copyTo = updatedItem.GetSourceFilePath();       // To quickmedia folder
                if (!File.Exists(copyTo)){
                    File.Copy(copyFrom, copyTo, overwrite: true);
                }
            }
            catch
            {
                _operationProgress.CompleteOperation(opId, false, "Error copying source file from library to Quick Media folder.");
                return;
            }
        }
        if (formMode == EditPlayableItemFormMode.Add)
        {
            _operationProgress.ReportProgress(opId, "Adding button to collection");
            _buttons[itemIndex] = button;
        }
        else
        {
            _operationProgress.ReportProgress(opId, "Updating item metadata");
            originalItem.UpdateFromClone(updatedItem);
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

    /// <summary>
    /// Called when Re-Convert All is clicked in the QuickMedia page
    /// </summary>
    /// <param name="formMode"></param> The mode of the form (ReConvertAllGlobal or ReConvertAllEmbedded)
    /// <param name="options"></param> The new matrix options to use if ReConvertAllGlobal
    /// <returns></returns>
    public async Task ReConvertAllItems(EditPlayableItemFormMode formMode, int relativeBrightness, LedMatrixOptionsConfig? options)
    {
        var opId = await _operationProgress.StartOperation("ReConverting All Library Items");
        for (int i = 0; i < _maxButtons; i++)
        {
            var button = _buttons[i];
            if (button == null) continue;
            var originalItem = button.Item;
            if (originalItem == null) continue;
            var item = originalItem.Clone();
            if (formMode == EditPlayableItemFormMode.ReConvertAllBrightness)
            {
                item.RelativeBrightness = relativeBrightness;
            }
            else if (formMode == EditPlayableItemFormMode.ReConvertAllMatrix && options != null)
            {
                item.MatrixOptions = options;
            }
            if (!originalItem.NeedsReConvert(item))
            {
                continue;
            }
            _operationProgress.ReportProgress(opId, $"Re-converting Quick Media button {i+1}");
            var folder = button.GetAbsolutePath();
            var result = await _streamConverterService.ConvertToStream(
                folder,
                item.SourceFileName,
                folder,
                item.Name,
                item.RelativeBrightness, 
                item.MatrixOptions
            );
            if (result.ExitCode != 0)
            {
                _logger.LogError("{tag} Re-conversion failed for Quick Media button {buttonNumber}, item {itemName}: {error} - {message}", 
                    _logTag, i, item.Name, result.Error, result.Message);
                _operationProgress.ReportProgress(opId, $"Re-conversion failed for Quick Media button {i+1}: {result.Error} - {result.Message}");
                continue;
            }
            // Update item's relative brightness and matrix options
            item.CurrentBrightness = result.ActualBrightness;
            // Save updated metadata
            button.Item.UpdateFromClone(item);
            // Serialize updated item
            SerializeQuickMediaButton(button);
        }
        _operationProgress.CompleteOperation(opId, true, "Done");
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