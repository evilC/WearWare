using WearWare.Services.MediaController;
using WearWare.Common.Media;

namespace WearWare.Services.QuickMedia
{
    public interface IQuickMediaButtonFactory
    {
        IQuickMediaButton Create(MediaControllerService mediaController, int buttonNumber, PlayableItem item);
    }
}
