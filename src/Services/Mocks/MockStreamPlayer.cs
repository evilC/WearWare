using WearWare.Common.Media;
using WearWare.Services.MediaController;

namespace WearWare.Services.Mocks
{
    public class MockStreamPlayer : IStreamPlayer
    {
        private readonly ILogger<MockStreamPlayer> _logger;
        public MockStreamPlayer(ILogger<MockStreamPlayer> logger)
        {
            _logger = logger;
        }

        public bool PlayStream(PlayableItem playableItem, CancellationToken ct)
        {
            _logger.LogInformation("[PLAYSTREAM MOCK] Playing: {ItemName}. PlayMode: {PlayMode}, Value: {PlayModeValue}", playableItem.Name, playableItem.PlayMode, playableItem.PlayModeValue);
            long endTime = 0;
            if (playableItem.PlayMode == PlayMode.Duration)
            {
                endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + (playableItem.PlayModeValue * 1000);
            }
            while (!ct.IsCancellationRequested)
            {
                if (playableItem.PlayMode == PlayMode.Duration && DateTimeOffset.Now.ToUnixTimeMilliseconds() >= endTime)
                {
                    break;
                }
                if (playableItem.PlayMode == PlayMode.Loop)
                {
                    Thread.Sleep(1000);
                    break;
                }
                Thread.Sleep(1000);
            }
            return true;
        }

        public void Clear()
        {
            // No display to clear in the mock player
        }
    }
}
