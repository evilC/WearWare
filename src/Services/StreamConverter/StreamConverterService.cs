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

        // Example method: convert a file to a stream format
        public async Task<TaskResult> ConvertToStream(string sourcePath, string odldFileName, string destPath, string newFileNameNoExt, int relativeBrightness, LedMatrixOptionsConfig? options = null)
        {
            var mediaType = MediaTypeMappings.GetMediaType(Path.GetExtension(odldFileName));
            if (mediaType == null){
                return new TaskResult { ExitCode = 0, Error = "Unknown media type", Message = "Stream conversion failed - unknown media type." };
            }
            var toolPath = Path.Combine(PathConfig.ToolsPath, "led-image-viewer");
            var inputPath = Path.Combine(sourcePath, odldFileName);
            var streamFile = $"{newFileNameNoExt}.stream";
            var streamPath = Path.Combine(destPath, streamFile);
            // Write to a temporary file first, then atomically move into place to avoid read/write races
            var tmpStreamFile = $"{newFileNameNoExt}.stream.tmp";
            var tmpStreamPath = Path.Combine(destPath, tmpStreamFile);
            var matrixOptions = options != null ? options : _matrixConfigService.CloneOptions();
            var matrixArgs = matrixOptions.ToArgsString(relativeBrightness);
            var command = $"\"sudo {toolPath} {matrixArgs} {inputPath} -O{tmpStreamPath}\"";
            _logger.LogInformation("{LogTag} Executing stream conversion command: {command}", _logTag, command);
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"-c {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return new TaskResult { ExitCode = -1, Error = "Failed to start led-image-viewer.", Message = "Failed to start led-image-viewer." };

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
                    return new TaskResult { ExitCode = -1, Error = ex.Message + "\n" + error, Message = "Stream conversion succeeded but failed to move temp file into place." };
                }
                return new TaskResult { ExitCode = exitCode, Error = error, Message = "Stream conversion successful." };
            }
            else
            {
                // Clean up temp file on failure
                try { if (File.Exists(tmpStreamPath)) File.Delete(tmpStreamPath); } catch {}
                return new TaskResult { ExitCode = exitCode, Error = error, Message = $"Stream conversion failed (exit code {exitCode})" };
            }
        }
    }
}
