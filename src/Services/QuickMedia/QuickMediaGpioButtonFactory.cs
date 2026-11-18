using WearWare.Services.MediaController;

namespace WearWare.Services.QuickMedia
{
    public class QuickMediaGpioButtonFactory : IQuickMediaButtonFactory
    {
        private readonly IButtonGpioProvider _gpioProvider;

        public QuickMediaGpioButtonFactory(IButtonGpioProvider gpioProvider)
        {
            _gpioProvider = gpioProvider;
        }

        public IQuickMediaButton Create(MediaControllerService mediaController, int buttonNumber, int pinNumber, PlayableItem item)
        {
            return new QuickMediaGpioButton(mediaController, _gpioProvider.Controller, buttonNumber, pinNumber, item);
        }
    }
}
