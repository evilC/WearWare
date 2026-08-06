using WearWare.Common;
using WearWare.Services.StreamConverter;
using WearWare.Services.MatrixConfig;
using WearWare.Common.Media;
using WearWare.Config;

namespace WearWare.Services.Mocks
{
    public class MockStreamConverterService : IStreamConverterService
    {
        private readonly MatrixConfigService _matrixConfigService;
        private readonly ILogger<MockStreamConverterService> _logger;
        private readonly string _logTag = "[MOCKSTREAMCONVERTER]";
        public MockStreamConverterService(MatrixConfigService matrixConfigService, ILogger<MockStreamConverterService> logger)
        {
            _matrixConfigService = matrixConfigService;
            _logger = logger;
        }

        public async Task<ReConvertTaskResult> ConvertToFseq(string sourcePath, string oldFileName, string destPath, string newFileNameNoExt, int relativeBrightness, LedMatrixOptionsConfig? options = null)
        {
            var mediaType = MediaTypeMappings.GetMediaType(Path.GetExtension(oldFileName));
            if (mediaType == null)
            {
                return new ReConvertTaskResult { ExitCode = -1, Error = "Unknown media type", Message = "FSEQ conversion failed - unknown media type." };
            }

            var toolPath = Path.Combine(PathConfig.ToolsPath, "frame-sequence-player");
            var inputPath = Path.Combine(sourcePath, oldFileName);
            var fseqPath = Path.Combine(destPath, $"{newFileNameNoExt}.fseq");
            var matrixOptions = options != null ? options : _matrixConfigService.CloneOptions();
            var matrixArgs = matrixOptions.ToArgsString(100);
            var command = $"\"sudo {toolPath} {matrixArgs} {inputPath} -O{fseqPath}\"";
            _logger.LogInformation("{LogTag} Executing fseq conversion command: {command}", _logTag, command);

            File.Create(fseqPath).Dispose();
            await Task.Delay(1000); // Simulate some work
            return new ReConvertTaskResult { ExitCode = 0, Error = "", Message = "FSEQ conversion successful." };
        }
    }
}
