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
            var matrixOptions = options != null ? options : _matrixConfigService.CloneOptions();
            var matrixArgs = matrixOptions.ToArgsString(relativeBrightness);
            var command = $"\"sudo {toolPath} {matrixArgs} {inputPath} -O{streamPath}\"";
            _logger.LogInformation("{LogTag} Executing stream conversion command: {command}", _logTag, command);

            File.Create(Path.Combine(destPath, $"{newFileNameNoExt}.stream")).Dispose();
            await Task.Delay(1000); // Simulate some work
            return new TaskResult { ExitCode = 0, Error = "", Message = "Mock conversion successful." };
        }
    }
}
