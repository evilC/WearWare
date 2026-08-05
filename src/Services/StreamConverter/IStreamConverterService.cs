using WearWare.Services.MatrixConfig;

namespace WearWare.Services.StreamConverter
{
    public interface IStreamConverterService
    {
        Task<ReConvertTaskResult> ConvertToFseq(string sourcePath, string oldFileName, string destPath, string newFileNameNoExt, int relativeBrightness, LedMatrixOptionsConfig? options = null);
    }
}
