/*
Gets CPU temperature of the Raspberry Pi
*/
namespace WearWare.Services.TempMon
{
    public class TempMonService : ITempMonService
    {
        const string ThermalPath = "/sys/class/thermal/thermal_zone0/temp";

        double? _lastTemperatureC;
        public double? LastTemperatureC
        {
            get => _lastTemperatureC;
            private set => _lastTemperatureC = value;
        }

        public TempMonService()
        {
        }

        async Task<long?> ReadTempFileAsync(CancellationToken token)
        {
            try
            {
                var txt = await File.ReadAllTextAsync(ThermalPath, token).ConfigureAwait(false);
                if (long.TryParse(txt.Trim(), out var v))
                    return v;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (FileNotFoundException)
            {
                // File not present on non-Linux systems
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
            }
            return null;
        }

        /// <summary>
        /// Read the temperature once and update LastTemperatureC. Intended for on-demand reads.
        /// </summary>
        public async Task<double?> ReadCurrentTemperatureAsync(CancellationToken token = default)
        {
            var v = await ReadTempFileAsync(token).ConfigureAwait(false);
            if (v.HasValue)
            {
                LastTemperatureC = v.Value / 1000.0;
                return LastTemperatureC;
            }
            LastTemperatureC = null;
            return null;
        }
    }
}