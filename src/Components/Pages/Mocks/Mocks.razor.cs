using WearWare.Architecture.QuickMedia;
using WearWare.Components.Base;
using WearWare.Services.MediaController;

namespace WearWare.Components.Pages.Mocks
{
    public partial class Mocks : DataPageBase
    {
        [Inject] public QuickMediaManager QuickMediaManager { get; set; } = null!;
        [Inject] public QuickMediaHandler QuickMediaHandler { get; set; } = null!;
        [Inject] public MediaControllerService MediaController { get; set; } = null!;
        
        private bool _subscribed;
        
        protected override Task InitializeDataAsync()
        {
            if (!_subscribed)
            {
                QuickMediaManager.StateChanged += OnStateChanged;
                _subscribed = true;
            }
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            if (_subscribed)
            {
                QuickMediaManager.StateChanged -= OnStateChanged;
                _subscribed = false;
            }
            base.Dispose();
        }

        private void OnStateChanged()
        {
            InvokeAsync(StateHasChanged);
        }

        private void OnButtonClick(int buttonNumber)
        {
            QuickMediaManager.SetActiveButton(buttonNumber);
            QuickMediaHandler.Start();
        }
    }
}