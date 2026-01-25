using WearWare.Common.Media;
using WearWare.Config;

namespace WearWare.Services.QuickMedia
{
    public interface IQuickMediaButton : IDisposable
    {
        int ButtonNumber { get; }
        PlayableItem Item { get; }

        public string GetAbsolutePath()
        {
            return Path.Combine(PathConfig.Root, GetRelativePath());
        }

        public string GetRelativePath()
        {
            return Path.Combine(PathConfig.QuickMediaPath, ButtonNumber.ToString());
        }
    }
}
