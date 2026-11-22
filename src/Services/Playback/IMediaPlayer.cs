using WearWare.Common.Media;

namespace WearWare.Services.Playback
{
    public interface IMediaPlayer
    {
        bool PlayFseq(PlayableItem playableItem, CancellationToken ct);
        void Clear();
    }
}
