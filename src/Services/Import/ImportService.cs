using System.Text.Json;
using System.Text;
using WearWare.Services.Library;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Utils;
using WearWare.Common;
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
        private readonly IStreamConverterService _streamConverterService;
        private readonly LibraryService _libraryService;
        private readonly IOperationProgressService _operationProgress;
        public ImportService(MatrixConfigService matrixConfigService, 
            IStreamConverterService streamConverterService,
            LibraryService libraryService,
            IOperationProgressService operationProgress
        )
        {
            _matrixConfigService = matrixConfigService;
            _matrixConfigService.OptionsChanged += OnMatrixOptionsChanged;
            OnMatrixOptionsChanged();
            _streamConverterService = streamConverterService;
            _libraryService = libraryService;
            _operationProgress = operationProgress;
        }

        private void OnMatrixOptionsChanged()
        {
            
        }

        public List<string>? GetImportFiles()
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
            return files;
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
        /// <param name="oldFileName"></param> The original filename in the incoming folder
        /// <param name="newFileNameNoExt"></param> The new filename (without extension) to use in the library
        /// <param name="relativeBrightness"></param> The relative brightness to set for the imported item
        /// <param name="options"></param> The LED matrix options to use for the imported item
        /// <returns></returns>
        public async Task OnEditFormSubmit(EditPlayableItemFormModel formModel)
        {
            var opId = await _operationProgress.StartOperation("Importing Item");
            var mediaType = MediaTypeMappings.GetMediaType(Path.GetExtension(formModel.OriginalFileName));
            if (mediaType == null)
            {
                _operationProgress.CompleteOperation(opId, false, "Import failed - unknown media type.");
                return;
            }
            formModel.UpdatedItem.Name = FilenameValidator.Sanitize(formModel.NewName);
            _operationProgress.ReportProgress(opId, "Converting stream...");
            var result = await _streamConverterService.ConvertToStream(
                PathConfig.IncomingPath, 
                formModel.OriginalFileName, 
                PathConfig.LibraryPath, 
                formModel.UpdatedItem.Name, 
                formModel.UpdatedItem.RelativeBrightness, 
                formModel.UpdatedItem.MatrixOptions
            );
            if (result.ExitCode != 0)
            {
                _operationProgress.CompleteOperation(opId, false, result.Message + "\n" + result.Error);
                return;
            }
            _operationProgress.ReportProgress(opId, "Copying original file...");
            // Copy original file to library path, and rename source file to newFileNameNoExt + original extension
            var ext = Path.GetExtension(formModel.OriginalFileName);
            var destPath = Path.Combine(PathConfig.LibraryPath, $"{formModel.UpdatedItem.Name}{ext}");
            try {
                var sourcePath = Path.Combine(PathConfig.IncomingPath, formModel.OriginalFileName);
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                File.Copy(sourcePath, destPath);
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
                formModel.UpdatedItem.RelativeBrightness,
                BrightnessCalculator.CalculateAbsoluteBrightness(_matrixConfigService.CloneOptions().Brightness ?? 100, formModel.UpdatedItem.RelativeBrightness),
                formModel.UpdatedItem.MatrixOptions
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
