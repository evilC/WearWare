using WearWare.Services.MediaController;
using WearWare.Services.QuickMedia;

namespace WearWare.Services.Mocks
{
    public class MockQuickMediaButtonFactory : IQuickMediaButtonFactory
    {
        public MockQuickMediaButtonFactory()
        {
        }

        public IQuickMediaButton Create(MediaControllerService mediaController, int buttonNumber, int pinNumber, PlayableItem item)
        {
            return new MockQuickMediaButton(buttonNumber, item);
        }
    }
}
