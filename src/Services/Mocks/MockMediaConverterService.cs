using WearWare.Common;
using WearWare.Services.StreamConverter;
using WearWare.Services.MatrixConfig;
using WearWare.Common.Media;
using WearWare.Config;

namespace WearWare.Services.Mocks
{
    public class MockMediaConverterService : IMediaConverterService
    {
        private readonly MatrixConfigService _matrixConfigService;
        private readonly ILogger<MockMediaConverterService> _logger;
        private readonly string _logTag = "[MOCKMEDIACONVERTER]";
        public MockMediaConverterService(MatrixConfigService matrixConfigService, ILogger<MockMediaConverterService> logger)
        {
            _matrixConfigService = matrixConfigService;
            _logger = logger;
        }

        public async Task<MediaConversionResult> ConvertToFseq(string sourcePath, string sourceFileName, string destPath, string outputNameWithoutExtension, int relativeBrightness, LedMatrixOptionsConfig? options = null)
        {
            var mediaType = MediaTypeMappings.GetMediaType(Path.GetExtension(sourceFileName));
            if (mediaType == null)
            {
                return new MediaConversionResult { ExitCode = -1, Error = "Unknown media type", Message = "FSEQ conversion failed - unknown media type." };
            }

            var toolPath = Path.Combine(PathConfig.ToolsPath, "frame-sequence-player");
            var inputPath = Path.Combine(sourcePath, sourceFileName);
            var fseqPath = Path.Combine(destPath, $"{outputNameWithoutExtension}.fseq");
            var matrixOptions = options != null ? options : _matrixConfigService.CloneOptions();
            var matrixArgs = matrixOptions.ToArgsString(100);
            var command = $"\"sudo {toolPath} {matrixArgs} {inputPath} -O{fseqPath}\"";
            _logger.LogInformation("{LogTag} Executing fseq conversion command: {command}", _logTag, command);

            File.Create(fseqPath).Dispose();
            await Task.Delay(1000); // Simulate some work
            return new MediaConversionResult { ExitCode = 0, Error = "", Message = "FSEQ conversion successful." };
        }
    }
}
