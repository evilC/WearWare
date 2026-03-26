using System.Diagnostics;
using WearWare.Common;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.StreamConverter
{
    public class StreamConverterService : IStreamConverterService
    {
        // private string _matrixOptions = "";
        private readonly MatrixConfigService _matrixConfigService;
        private readonly ILogger<StreamConverterService> _logger;
        private readonly string _logTag = "[STREAMCONVERTER]";
        public StreamConverterService(MatrixConfigService matrixConfigService, ILogger<StreamConverterService> logger)
        {
            _logger = logger;
            _matrixConfigService = matrixConfigService;
            matrixConfigService.OptionsChanged += OnMatrixOptionsChanged;
            OnMatrixOptionsChanged();
        }

        private void OnMatrixOptionsChanged()
        {
            // _matrixOptions = _matrixConfigService.GetArgsString();
        }

        /// <summary>
        /// Converts the specified source media file to a .stream file using led-image-viewer with the specified options.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="oldFileName"></param>
        /// <param name="destPath"></param>
        /// <param name="newFileNameNoExt"></param>
        /// <param name="relativeBrightness"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// </summary>
        /// <returns></returns>
        public async Task<ReConvertTaskResult> ConvertToStream(string sourcePath, string oldFileName, string destPath, string newFileNameNoExt, int relativeBrightness, LedMatrixOptionsConfig? options = null)
        {
            var mediaType = MediaTypeMappings.GetMediaType(Path.GetExtension(oldFileName));
            if (mediaType == null){
                return new ReConvertTaskResult { ExitCode = -1, Error = "Unknown media type", Message = "Stream conversion failed - unknown media type." };
            }
            var toolPath = Path.Combine(PathConfig.ToolsPath, "led-image-viewer");
            var inputPath = Path.Combine(sourcePath, oldFileName);
            var streamFile = $"{newFileNameNoExt}.stream";
            var streamPath = Path.Combine(destPath, streamFile);
            // Write to a temporary file first, then atomically move into place to avoid read/write races
            var tmpStreamFile = $"{newFileNameNoExt}.stream.tmp";
            var tmpStreamPath = Path.Combine(destPath, tmpStreamFile);
            var matrixOptions = options != null ? options : _matrixConfigService.CloneOptions();
            var argsList = matrixOptions.ToArgsList(relativeBrightness);
            argsList.Add(inputPath);
            argsList.Add($"-O{tmpStreamPath}");
            var sudoPath = "/usr/bin/sudo";
            if (!File.Exists(sudoPath))            {
                return new ReConvertTaskResult { ExitCode = -1, Error = "sudo not found at " + sudoPath, Message = "Stream conversion failed - server misconfiguration." };
            }
            _logger.LogInformation("{LogTag} Executing led-image-viewer with args: {args}", _logTag, string.Join(" ", argsList));
            var psi = new ProcessStartInfo {
                FileName = "/usr/bin/sudo",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Clear();
            psi.ArgumentList.Add(toolPath);
            foreach (var a in argsList) psi.ArgumentList.Add(a);

            int actualBrightness = BrightnessCalculator.CalculateAbsoluteBrightness(matrixOptions.Brightness ?? 100, relativeBrightness);
            /*
            Note: If the code hangs here when running as a service, it's likely because the service does not have a path to the executable
            eg bash, sudo etc.
            Either the service needs to be configured with a PATH that includes the necessary executables...
            ... or the full path to the executable needs to be specified in the code.
            */
            using var process = Process.Start(psi);
            if (process == null)
                return new ReConvertTaskResult { ExitCode = -1, Error = "Failed to start led-image-viewer.", Message = "Failed to start led-image-viewer.", ActualBrightness = actualBrightness };

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            int exitCode = process.ExitCode;
            if (exitCode == 0)
            {
                try
                {
                    // Ensure destination directory exists
                    Directory.CreateDirectory(destPath);
                    // Atomically replace existing file by move/rename. On Unix rename is atomic when on same filesystem.
                    if (File.Exists(tmpStreamPath))
                    {
                        // If target exists, delete it first to ensure move succeeds on Windows; on Unix rename will replace.
                        if (File.Exists(streamPath)) File.Delete(streamPath);
                        File.Move(tmpStreamPath, streamPath);
                    }
                }
                catch (Exception ex)
                {
                    // Clean up temp file on failure
                    try { if (File.Exists(tmpStreamPath)) File.Delete(tmpStreamPath); } catch {}
                    return new ReConvertTaskResult { ExitCode = -1, Error = ex.Message + "\n" + error, Message = "Stream conversion succeeded but failed to move temp file into place.", ActualBrightness = actualBrightness };
                }
                return new ReConvertTaskResult { ExitCode = exitCode, Error = error, Message = "Stream conversion successful.", ActualBrightness = actualBrightness };
            }
            else
            {
                // Clean up temp file on failure
                try { if (File.Exists(tmpStreamPath)) File.Delete(tmpStreamPath); } catch {}
                return new ReConvertTaskResult { ExitCode = exitCode, Error = error, Message = $"Stream conversion failed (exit code {exitCode})", ActualBrightness = actualBrightness };
            }
        }
    }
}
