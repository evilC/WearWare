using System.Device.Gpio;

namespace WearWare.Services.QuickMedia
{
    public interface IButtonGpioProvider : IDisposable
    {
        GpioController Controller { get; }

        IReadOnlyList<int> ButtonPins { get; }
    }
}
