using WearWare.Common.Media;

namespace WearWare.Architecture.Playback
{
    public interface IMediaPlayer
    {
        bool PlayFseq(PlayableItem playableItem, CancellationToken ct);
        void Clear();
    }
}
