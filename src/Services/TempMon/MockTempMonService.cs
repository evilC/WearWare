/*
Mock implementation of ITempMonService that returns null
Used when debugging locally (Not on a Pi)
*/
namespace WearWare.Services.TempMon
{
    public class MockTempMonService : ITempMonService
    {
        public double? LastTemperatureC => null;

        public Task<double?> ReadCurrentTemperatureAsync(CancellationToken token = default)
        {
            return Task.FromResult<double?>(null);
        }
    }
}