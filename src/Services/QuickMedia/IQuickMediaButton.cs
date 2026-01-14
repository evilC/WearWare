using WearWare.Common.Media;

namespace WearWare.Services.QuickMedia
{
    public interface IQuickMediaButton : IDisposable
    {
        int ButtonNumber { get; }
        PlayableItem Item { get; }
    }
}
