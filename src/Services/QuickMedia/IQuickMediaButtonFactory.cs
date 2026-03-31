using WearWare.Services.MediaController;

namespace WearWare.Services.QuickMedia
{
    public interface IQuickMediaButtonFactory
    {
        IQuickMediaButton Create(MediaControllerService mediaController, int buttonNumber, int pinNumber, PlayableItem item);
    }
}
