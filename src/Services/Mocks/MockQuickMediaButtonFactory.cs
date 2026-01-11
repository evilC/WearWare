using WearWare.Services.MediaController;
using WearWare.Common.Media;
using WearWare.Services.QuickMedia;

namespace WearWare.Services.Mocks
{
    public class MockQuickMediaButtonFactory : IQuickMediaButtonFactory
    {
        public MockQuickMediaButtonFactory()
        {
        }

        public IQuickMediaButton Create(MediaControllerService mediaController, int buttonNumber, PlayableItem item)
        {
            return new MockQuickMediaButton(buttonNumber, item);
        }
    }
}
