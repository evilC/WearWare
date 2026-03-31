using System.Device.Gpio;
using WearWare.Services.MediaController;

namespace WearWare.Services.QuickMedia
{
    public class QuickMediaGpioButtonFactory : IQuickMediaButtonFactory
    {
        public IQuickMediaButton Create(MediaControllerService mediaController, int buttonNumber, int pinNumber, PlayableItem item)
        {
            var gpioController = new GpioController();
            return new QuickMediaGpioButton(mediaController, gpioController, buttonNumber, pinNumber, item);
        }
    }
}
