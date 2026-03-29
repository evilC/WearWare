namespace WearWare.Services.TempMon
{
    public interface ITempMonService
    {
        double? LastTemperatureC { get; }

        Task<double?> ReadCurrentTemperatureAsync(CancellationToken token = default);
    }
}
