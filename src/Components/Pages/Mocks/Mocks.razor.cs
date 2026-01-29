using Microsoft.AspNetCore.Components;
using WearWare.Services.MediaController;

namespace WearWare.Components.Pages.Mocks
{
    public partial class Mocks : IDisposable
    {
        [Inject] public QuickMediaService QuickMediaService { get; set; } = null!;
        
        [Inject] public MediaControllerService MediaController { get; set; } = null!;
        
        protected override void OnInitialized()
        {
            QuickMediaService.StateChanged += OnStateChanged;
        }

        private void OnStateChanged()
        {
            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            QuickMediaService.StateChanged -= OnStateChanged;
        }
    }
}