using WearWare.Common.Media;
using WearWare.Services.QuickMedia;

namespace WearWare.Services.Mocks
{
    public class MockQuickMediaButton : IQuickMediaButton
    {
        public int ButtonNumber { get; }
        public PlayableItem Item { get; }

        public MockQuickMediaButton(int buttonNumber, PlayableItem item)
        {
            ButtonNumber = buttonNumber;
            Item = item;
        }

        public void Dispose()
        {
            // No hardware resources to clean up
        }

        public void Initialize()
        {
            // Nothing to initialize in the mock
        }
    }
}
