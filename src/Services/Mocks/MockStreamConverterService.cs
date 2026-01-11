using WearWare.Common;
using WearWare.Services.StreamConverter;
using WearWare.Services.MatrixConfig;

namespace WearWare.Services.Mocks
{
    public class MockStreamConverterService : IStreamConverterService
    {
        public MockStreamConverterService()
        {
        }

        public async Task<TaskResult> ConvertToStream(string sourcePath, string odldFileName, string destPath, string newFileNameNoExt, LedMatrixOptionsConfig? options = null)
        {
            await Task.Delay(1000); // Simulate some work
            return new TaskResult { ExitCode = 0, Error = "", Message = "Mock conversion successful." };
        }
    }
}
