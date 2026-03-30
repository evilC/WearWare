using WearWare.Components.Base;
using WearWare.Services.MatrixConfig;
using WearWare.Services.TempMon;

namespace WearWare.Components.Pages.Options
{
    public partial class Options : DataPageBase
    {
        [Inject] private MatrixConfigService MatrixConfigService { get; set; } = null!;
        [Inject] private ITempMonService TempMonService { get; set; } = null!;
        [Inject] private IJSRuntime JS { get; set; } = null!;
        
        private bool showForm = false;
        private LedMatrixOptionsConfig modalOptions = new();

        protected override Task InitializeDataAsync()
        {
            // client-driven poller will call Poll(); no server-side event subscription needed
            return Task.CompletedTask;
        }

        DotNetObjectReference<Options>? _dotnetRef;
        int _pollerId;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            // Ensure base handles data initialization and sets Ready
            await base.OnAfterRenderAsync(firstRender);

            if (firstRender)
            {
                _dotnetRef = DotNetObjectReference.Create(this);
                try
                {
                    _pollerId = await JS.InvokeAsync<int>("tempPoller.start", _dotnetRef, 2000);
                }
                catch { }
            }
        }

        private Task ShowForm()
        {
            modalOptions = MatrixConfigService.CloneOptions();
            showForm = true;
            return Task.CompletedTask;
        }

        private Task OnSaveFromForm(LedMatrixOptionsConfig updated)
        {
            MatrixConfigService.UpdateOptions(updated);
            showForm = false;
            StateHasChanged();
            return Task.CompletedTask;
        }

        private Task OnCancelFromForm()
        {
            // Close the form without saving
            showForm = false;
            // revert modal options (not strictly necessary)
            modalOptions = MatrixConfigService.CloneOptions();
            StateHasChanged();
            return Task.CompletedTask;
        }

        [JSInvokable]
        public async Task Poll()
        {
            try
            {
                await TempMonService.ReadCurrentTemperatureAsync(CancellationToken.None);
                await InvokeAsync(StateHasChanged);
            }
            catch { }
        }

        public override void Dispose()
        {
            try
            {
                if (_pollerId != 0)
                    _ = JS.InvokeVoidAsync("tempPoller.stop", _pollerId);
            }
            catch { }

            _dotnetRef?.Dispose();
            base.Dispose();
        }

        private string BuildTemperatureString()
        {
            if (!TempMonService.LastTemperatureC.HasValue)
                return "Unavailable";
            var temp = TempMonService.LastTemperatureC.Value;
            var str = $"{temp:F2} °C ";
            if (temp >= 80)
                return str + "(THROTTLED)";
            if (temp >= 70)
                return str + "(High)";
            if (temp >= 60)
                return str + "(Elevated)";
            return str + "(Normal)";
        }
    }
}