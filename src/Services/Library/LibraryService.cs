using System.Text.Json;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Utils;
using WearWare.Services.MediaController;
using WearWare.Services.MatrixConfig;
using WearWare.Components.Forms.EditPlayableItemForm;

namespace WearWare.Services.Library
{
    public class LibraryService
    {
        public event Action? ItemsChanged;
        private readonly SortedList<string, PlayableItem> _items = [];

        public IReadOnlyList<PlayableItem> Items
        {
            get
            {
                return _items.Values.ToList();
            }
        }

        private readonly ILogger<LibraryService> _logger;
        private readonly MediaControllerService _mediaControllerService;
        private readonly MatrixConfigService _matrixConfigService;
        private readonly StreamConverter.IStreamConverterService _streamConverterService;
        private readonly OperationProgress.IOperationProgressService _operationProgress;

        public LibraryService(ILogger<LibraryService> logger,
            MediaControllerService mediaControllerService,
            MatrixConfigService matrixConfigService,
            StreamConverter.IStreamConverterService streamConverterService,
            OperationProgress.IOperationProgressService operationProgress)
        {
            _logger = logger;
            _mediaControllerService = mediaControllerService;
            _matrixConfigService = matrixConfigService;
            _streamConverterService = streamConverterService;
            _operationProgress = operationProgress;
            LoadLibraryItems();
            _logger.LogInformation("LibraryService initialized.");
        }

        private void LoadLibraryItems()
        {
            _items.Clear();
            if (!Directory.Exists(PathConfig.LibraryPath))
                return;

            var jsonFiles = Directory.EnumerateFiles(PathConfig.LibraryPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in jsonFiles)
            {
                PlayableItem? libraryItem;
                try
                {
                    var json = File.ReadAllText(file);
                    libraryItem = JsonSerializer.Deserialize<PlayableItem>(json);
                }
                catch
                {
                    // Optionally log or handle errors
                    continue;
                }
                
                if (libraryItem != null)
                {
                    // Older JSON may not include MatrixOptions; ensure it's initialized so code relying on it won't see null.
                    if (libraryItem.MatrixOptions == null)
                        libraryItem.MatrixOptions = _matrixConfigService.CloneOptions();

                    _items.Add(libraryItem.Name, libraryItem);
                }
            }
            ItemsChanged?.Invoke();
        }

        /// <summary>
        /// Reloads library items from disk and notifies listeners.
        /// </summary>
        public void Reload()
        {
            LoadLibraryItems();
        }

        /// <summary>
        /// Deletes the specified library item files and metadata, then reloads the library.
        /// </summary>
        /// <param name="item"></param>
        public void DeleteLibraryItem(PlayableItem item)
        {
            try
            {
                var jsonPath = Path.Combine(PathConfig.LibraryPath, $"{item.Name}.json");

                if (File.Exists(jsonPath)) File.Delete(jsonPath);
                if (File.Exists(item.GetStreamFilePath())) File.Delete(item.GetStreamFilePath());
                if (File.Exists(item.GetSourceFilePath())) File.Delete(item.GetSourceFilePath());
            }
            catch
            {
                // Swallow errors - deletion failure shouldn't crash the UI.
            }
            // Reload the library to refresh the UI
            Reload();
        }

        public void PlayPreviewItem(PlayableItem libItem)
        {
            _mediaControllerService.PlayQuickMedia(libItem);
        }

        /// <summary>
        /// Called when the edit form is submitted in the Library page.
        /// </summary>
        /// <param name="originalItem"></param> The original item before editing
        /// <param name="updatedItem"></param> The updated item with new values
        /// <param name="formMode"></param> The mode of the form (Add or Edit)
        /// <returns></returns>
        public async Task OnEditFormSubmit(EditPlayableItemFormModel formModel)
        {
            var opId = await _operationProgress.StartOperation("Updating Library Item");
            try
            {
                if (formModel.OriginalItem.NeedsReConvert(formModel.UpdatedItem))
                {
                    _operationProgress.ReportProgress(opId, "Converting stream...");
                    var result = await _streamConverterService.ConvertToStream(
                        PathConfig.LibraryPath, 
                        formModel.OriginalItem.SourceFileName, 
                        PathConfig.LibraryPath, 
                        formModel.OriginalItem.Name,
                        formModel.UpdatedItem.RelativeBrightness, 
                        formModel.UpdatedItem.MatrixOptions
                    );
                    if (result.ExitCode != 0)
                    {
                        _operationProgress.CompleteOperation(opId, false, result.Message + "\n" + result.Error);
                        return;
                    }
                }

                // Update metadata and save
                try
                {
                    _operationProgress.ReportProgress(opId, "Writing metadata...");
                    var jsonPath = Path.Combine(PathConfig.LibraryPath, $"{formModel.UpdatedItem.Name}.json");
                    JsonUtils.ToJsonFile(jsonPath, formModel.UpdatedItem);
                    ItemsChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    _operationProgress.CompleteOperation(opId, false, "ReConvert succeeded, but failed to write JSON metadata: " + ex.Message);
                    return;
                }
                // Update the original item with data from the clone
                formModel.OriginalItem.UpdateFromClone(formModel.UpdatedItem);

                _operationProgress.CompleteOperation(opId, true, "Done");
                return;
            }
            catch (Exception ex)
            {
                _operationProgress.CompleteOperation(opId, false, ex.Message);
                return;
            }
        }

        /// <summary>
        /// Called when OK is clicked in the ReConvert All dialog.
        /// </summary>
        /// <param name="relativeBrightness"></param> The relative brightness to set for all items
        public async Task ReConvertAllItems(EditPlayableItemFormMode formMode, int relativeBrightness, LedMatrixOptionsConfig? options = null)
        {
            var opId = await _operationProgress.StartOperation("ReConverting All Library Items");
            int itemCount = 0;
            foreach (var item in _items.Values)
            {
                itemCount++;
            }
            int currentItem = 0;
            foreach (var originalItem in _items.Values)
            {
                var item = originalItem.Clone();
                if (formMode == EditPlayableItemFormMode.ReConvertAllBrightness)
                {
                    item.RelativeBrightness = relativeBrightness;
                }
                else if (formMode == EditPlayableItemFormMode.ReConvertAllMatrix && options != null)
                {
                    item.MatrixOptions = options;
                }
                currentItem++;
                if (!originalItem.NeedsReConvert(item))
                {
                    continue;
                }
                _operationProgress.ReportProgress(opId, $"ReConverting {item.Name} ({currentItem} of {itemCount})");
                var result = await _streamConverterService.ConvertToStream(
                    PathConfig.LibraryPath,
                    item.SourceFileName,
                    PathConfig.LibraryPath,
                    item.Name,
                    item.RelativeBrightness,
                    item.MatrixOptions
                );
                if (result.ExitCode != 0)
                {
                    _operationProgress.CompleteOperation(opId, false, $"Failed to ReConvert item {item.Name}: " + result.Message + "\n" + result.Error);
                    return;
                }
                // Update item's relative brightness and matrix options
                item.CurrentBrightness = result.ActualBrightness;
                // Save updated metadata
                try
                {
                    var jsonPath = Path.Combine(PathConfig.LibraryPath, $"{item.Name}.json");
                    JsonUtils.ToJsonFile(jsonPath, item);
                }
                catch (Exception ex)
                {
                    _operationProgress.CompleteOperation(opId, false, "ReConvert succeeded, but failed to write JSON metadata: " + ex.Message);
                    return;
                }
                // Update the original item with data from the clone
                originalItem.UpdateFromClone(item);
            }
            _operationProgress.CompleteOperation(opId, true, "Done");
        }
    }
}
