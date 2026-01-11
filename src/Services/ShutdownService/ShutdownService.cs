// Handles application shutdown to stop media playback and clear the matrix display.

using WearWare.Services.MediaController;

namespace WearWare.Services.ShutdownService
{
    public class ShutdownService : IHostedService
    {
        private readonly MediaControllerService _mediaController;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly ILogger<ShutdownService> _logger;
        private readonly string _logTag = "[ShutdownService]";

        public ShutdownService(MediaControllerService mediaController, IHostApplicationLifetime appLifetime, ILogger<ShutdownService> logger)
        {
            _mediaController = mediaController;
            _appLifetime = appLifetime;
            _logger = logger;
            _logger.LogInformation("{tag} initialized.", _logTag);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStopping.Register(OnShutdown);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private void OnShutdown()
        {
            _logger.LogInformation("{tag} Application stopping: calling MediaController.Stop() and clearing matrix.", _logTag);
            _mediaController.Stop();
        }
    }
}
