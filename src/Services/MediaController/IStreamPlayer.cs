using WearWare.Common.Media;
namespace WearWare.Services.MediaController
{
    public interface IStreamPlayer
    {
        /// <summary>
        /// Plays the given playable item stream.
        /// </summary>
        /// <param name="playableItem"></param> The stream to play
        /// <param name="ct"></param> Cancellation token to stop playback
        /// <returns>True if playback completed successfully, false otherwise.</returns>
        bool PlayStream(PlayableItem playableItem, CancellationToken ct);
        void Clear();
    }
}