namespace WearWare.Architecture.Button
{
    /// <summary>
    /// Initializes the ButtonManager on application startup.
    /// </summary>
    public sealed class ButtonManagerInitializer : IHostedService
    {
        private readonly ButtonManager _buttonManager;

        public ButtonManagerInitializer(ButtonManager buttonManager)
        {
            _buttonManager = buttonManager;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _buttonManager.Initialize();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _buttonManager.Dispose();
            return Task.CompletedTask;
        }
    }
}
