using System.Threading.Tasks;
using WearWare.Common;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.StreamConverter
{
    public interface IStreamConverterService
    {
        Task<ReConvertTaskResult> ConvertToStream(string sourcePath, string odldFileName, string destPath, string newFileNameNoExt, int relativeBrightness, LedMatrixOptionsConfig? options = null);
    }
}
