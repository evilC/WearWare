using System.Text;
using WearWare.Services.Library;
using WearWare.Utils;
using WearWare.Services.StreamConverter;
using WearWare.Services.MatrixConfig;
using WearWare.Services.OperationProgress;
using WearWare.Components.Forms.EditPlayableItemForm;

namespace WearWare.Services.Import
{
    public class ImportService
    {
        public event Action? StateChanged;
        private readonly MatrixConfigService _matrixConfigService;
        private readonly IMediaConverterService _mediaConverterService;
        private readonly LibraryService _libraryService;
        private readonly IOperationProgressService _operationProgress;
        public ImportService(MatrixConfigService matrixConfigService, 
            IMediaConverterService mediaConverterService,
            LibraryService libraryService,
            IOperationProgressService operationProgress
        )
        {
            _matrixConfigService = matrixConfigService;
            _mediaConverterService = mediaConverterService;
            _libraryService = libraryService;
            _operationProgress = operationProgress;
        }

        public List<PlayableItem>? GetImportItems()
        {
            if (!Directory.Exists(PathConfig.IncomingPath))
                return [];
            var allowedExts = MediaTypeMappings.ExtensionInfo.Keys;
            var files = Directory.GetFiles(PathConfig.IncomingPath)
                .Where(f => allowedExts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Select(f => Path.GetFileName(f)!)
                .Where(f => FilenameValidator.Validate(Path.GetFileNameWithoutExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<PlayableItem> importItems = [];
            foreach (var fileName in files)
            {
                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var sanitized = FilenameValidator.Sanitize(baseName);
                importItems.Add(new PlayableItem(
                    sanitized,
                    PathConfig.LibraryFolder,
                    MediaTypeMappings.GetMediaType(Path.GetExtension(fileName)) ?? MediaType.IMAGE,
                    fileName,
                    PlayMode.Forever,
                    1,
                    100
                ));
            }
            return importItems;
        }

        /// <summary>
        /// Deletes the specified file from the incoming folder and notifies listeners.
        /// </summary>
        /// <param name="fileName"></param>
        public void DeleteIncomingFile(string fileName)
        {
            try
            {
                var path = Path.Combine(PathConfig.IncomingPath, fileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Swallow errors to avoid crashing UI; callers can refresh/listen to StateChanged
            }
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Public helper to notify listeners that the import state changed (e.g. a new incoming file was uploaded).
        /// </summary>
        public void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Called when the edit form is submitted in the Import page.
        /// </summary>
        /// <returns></returns>
        public async Task OnEditFormSubmit(EditPlayableItemFormModel formModel)
        {
            var opId = await _operationProgress.StartOperation("Importing Item");
            var mediaType = MediaTypeMappings.GetMediaType(Path.GetExtension(formModel.UpdatedItem.SourceFileName));
            if (mediaType == null)
            {
                _operationProgress.CompleteOperation(opId, false, "Import failed - unknown media type.");
                return;
            }
            formModel.UpdatedItem.Name = FilenameValidator.Sanitize(formModel.UpdatedItem.Name);
            _operationProgress.ReportProgress(opId, "Converting fseq...");
            var result = await _mediaConverterService.ConvertToFseq(
                PathConfig.IncomingPath, 
                formModel.UpdatedItem.SourceFileName, 
                PathConfig.LibraryPath, 
                formModel.UpdatedItem.Name, 
                formModel.UpdatedItem.RelativeBrightness, 
                _matrixConfigService.CloneOptions()
            );
            if (result.ExitCode != 0)
            {
                _operationProgress.CompleteOperation(opId, false, result.Message + "\n" + result.Error);
                return;
            }
            _operationProgress.ReportProgress(opId, "Copying original file...");
            // Copy original file to library path and rename it to the updated item name plus original extension.
            var ext = Path.GetExtension(formModel.UpdatedItem.SourceFileName);
            var destPath = Path.Combine(PathConfig.LibraryPath, $"{formModel.UpdatedItem.Name}{ext}");
            try {
                var sourcePath = Path.Combine(PathConfig.IncomingPath, formModel.UpdatedItem.SourceFileName);
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                await FileUtils.CopyFileAsync(sourcePath, destPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _operationProgress.CompleteOperation(opId, false, "Import succeeded, but failed to copy original file: " + ex.Message);
                return;
            }
            var item = new PlayableItem(
                formModel.UpdatedItem.Name,
                PathConfig.LibraryFolder,
                mediaType.Value,
                Path.GetFileName(destPath),  
                PlayMode.Forever,
                0,
                formModel.UpdatedItem.RelativeBrightness
            );
            // Serialize item to JSON and write to libraryPath as name.json
            try
            {
                _operationProgress.ReportProgress(opId, "Writing metadata...");
                var json = JsonUtils.ToJson(item);
                var jsonPath = Path.Combine(PathConfig.LibraryPath, $"{formModel.UpdatedItem.Name}.json");
                await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8);
                // Notify library service that new items are available
                try
                {
                    _operationProgress.ReportProgress(opId, "Reloading library...");
                    _libraryService.Reload();
                }
                catch
                {
                    _operationProgress.ReportProgress(opId, "Warning: Failed to reload library after import.");
                }
            }
            catch (Exception ex)
            {
                _operationProgress.CompleteOperation(opId, false, "Import succeeded, but failed to write JSON metadata: " + ex.Message);
                return;
            }
            _operationProgress.CompleteOperation(opId, true, "Done");
            return;
        }
    }
}
