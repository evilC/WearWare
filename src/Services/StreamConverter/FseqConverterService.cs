using System.Diagnostics;
using WearWare.Common;
using WearWare.Common.Media;
using WearWare.Config;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.StreamConverter
{
    public class FseqConverterService : IMediaConverterService
    {
        private readonly MatrixConfigService _matrixConfigService;
        private readonly ILogger<FseqConverterService> _logger;
        private readonly string _logTag = "[FSEQCONVERTER]";
        public FseqConverterService(MatrixConfigService matrixConfigService, ILogger<FseqConverterService> logger)
        {
            _logger = logger;
            _matrixConfigService = matrixConfigService;
        }

        /// <summary>
        /// Converts the specified source media file to a .fseq file using frame-sequence-player.
        /// </summary>
        public async Task<MediaConversionResult> ConvertToFseq(string sourcePath, string sourceFileName, string destPath, string outputNameWithoutExtension, int relativeBrightness, LedMatrixOptionsConfig? options = null)
        {
            var mediaType = MediaTypeMappings.GetMediaType(Path.GetExtension(sourceFileName));
            if (mediaType == null){
                return new MediaConversionResult { ExitCode = -1, Error = "Unknown media type", Message = "FSEQ conversion failed - unknown media type." };
            }
            var toolPath = Path.Combine(PathConfig.ToolsPath, "frame-sequence-player");
            if (!File.Exists(toolPath))
            {
                return new MediaConversionResult { ExitCode = -1, Error = $"Tool not found at {toolPath}", Message = "FSEQ conversion failed - missing tool." };
            }
            var inputPath = Path.Combine(sourcePath, sourceFileName);
            var outputFile = $"{outputNameWithoutExtension}.fseq";
            var outputPath = Path.Combine(destPath, outputFile);
            // Write to a temporary file first, then atomically move into place to avoid read/write races
            var tmpOutputFile = $"{outputNameWithoutExtension}.fseq.tmp";
            var tmpOutputPath = Path.Combine(destPath, tmpOutputFile);
            var matrixOptions = options != null ? options : _matrixConfigService.CloneOptions();

            var argsList = matrixOptions.ToArgsList(100);
            argsList.Add(inputPath);
            argsList.Add($"-O{tmpOutputPath}");
            var sudoPath = "/usr/bin/sudo";
            if (!File.Exists(sudoPath))            {
                return new MediaConversionResult { ExitCode = -1, Error = "sudo not found at " + sudoPath, Message = "FSEQ conversion failed - server misconfiguration." };
            }
            _logger.LogInformation("{LogTag} Executing frame-sequence-player with args: {args}", _logTag, string.Join(" ", argsList));
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

            /*
            Note: If the code hangs here when running as a service, it's likely because the service does not have a path to the executable
            eg bash, sudo etc.
            Either the service needs to be configured with a PATH that includes the necessary executables...
            ... or the full path to the executable needs to be specified in the code.
            */
            using var process = Process.Start(psi);
            if (process == null)
                return new MediaConversionResult { ExitCode = -1, Error = "Failed to start frame-sequence-player.", Message = "Failed to start frame-sequence-player." };

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
                    if (File.Exists(tmpOutputPath))
                    {
                        // If target exists, delete it first to ensure move succeeds on Windows; on Unix rename will replace.
                        if (File.Exists(outputPath)) File.Delete(outputPath);
                        File.Move(tmpOutputPath, outputPath);
                    }
                }
                catch (Exception ex)
                {
                    // Clean up temp file on failure
                    try { if (File.Exists(tmpOutputPath)) File.Delete(tmpOutputPath); } catch {}
                    return new MediaConversionResult { ExitCode = -1, Error = ex.Message + "\n" + error, Message = "FSEQ conversion succeeded but failed to move temp file into place." };
                }
                return new MediaConversionResult { ExitCode = exitCode, Error = error, Message = "FSEQ conversion successful." };
            }
            else
            {
                // Clean up temp file on failure
                try { if (File.Exists(tmpOutputPath)) File.Delete(tmpOutputPath); } catch {}
                return new MediaConversionResult { ExitCode = exitCode, Error = error, Message = $"FSEQ conversion failed (exit code {exitCode})" };
            }
        }
    }
}
