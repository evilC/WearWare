using System.Text.Json;
using System.Text;
using WearWare.Services.Library;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Utils;
using WearWare.Common;
using WearWare.Services.StreamConverter;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.Import
{
    public class ImportService
    {
        public event Action? StateChanged;
        private readonly MatrixConfigService _matrixConfigService;
        private readonly WearWare.Services.StreamConverter.IStreamConverterService _streamConverterService;
        private readonly WearWare.Services.Library.LibraryService _libraryService;
        public ImportService(MatrixConfigService matrixConfigService, WearWare.Services.StreamConverter.IStreamConverterService streamConverterService, WearWare.Services.Library.LibraryService libraryService)
        {
            _matrixConfigService = matrixConfigService;
            _matrixConfigService.OptionsChanged += OnMatrixOptionsChanged;
            OnMatrixOptionsChanged();
            _streamConverterService = streamConverterService;
            _libraryService = libraryService;
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

        public async Task<TaskResult> ImportLibraryItem(string odldFileName, string newFileNameNoExt, LedMatrixOptionsConfig? options = null)
        {
            var mediaType = MediaTypeMappings.GetMediaType(Path.GetExtension(odldFileName));
            if (mediaType == null){
                return new TaskResult { ExitCode = 0, Error = "Unknown media type", Message = "Import failed - unknown media type." };
            }
            var result = await _streamConverterService.ConvertToStream(PathConfig.IncomingPath, odldFileName, PathConfig.LibraryPath, newFileNameNoExt, options);
            if (result.ExitCode != 0)
            {
                return result;
            }
            // Copy original file to library path, and rename source file to newFileNameNoExt + original extension
            var ext = Path.GetExtension(odldFileName);
            var destPath = Path.Combine(PathConfig.LibraryPath, $"{newFileNameNoExt}{ext}");
            try {
                var sourcePath = Path.Combine(PathConfig.IncomingPath, odldFileName);
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                File.Copy(sourcePath, destPath);
            }
            catch (Exception ex)
            {
                return new TaskResult { ExitCode = 0, Error = ex.Message, Message = "Import succeeded, but failed to copy original file." };
            }
            var item = new LibraryItem(newFileNameNoExt, mediaType.Value, Path.GetFileName(destPath));
            // Serialize item to JSON and write to libraryPath as name.json
            try
            {
                var json = JsonSerializer.Serialize(item, new JsonSerializerOptions { WriteIndented = true });
                var jsonPath = Path.Combine(PathConfig.LibraryPath, $"{newFileNameNoExt}.json");
                await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8);
                // Notify library service that new items are available
                try
                {
                    _libraryService.Reload();
                }
                catch
                {
                    // Swallow any errors from reload; import itself succeeded.
                }
            }
            catch (Exception ex)
            {
                return new TaskResult { ExitCode = 0, Error = ex.Message, Message = "Import succeeded, but failed to write JSON metadata." };
            }
            
            return new TaskResult { ExitCode = 0, Error = "", Message = "Import successful." };
        }
    }
}
