using WearWare.Services.MatrixConfig;

namespace WearWare.Services.StreamConverter
{
    public interface IMediaConverterService
    {
        Task<MediaConversionResult> ConvertToFseq(string sourcePath, string sourceFileName, string destPath, string outputNameWithoutExtension, int relativeBrightness, LedMatrixOptionsConfig? options = null);
    }
}
