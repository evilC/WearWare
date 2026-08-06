using WearWare.Components.Base;
using WearWare.Services.MatrixConfig;
using WearWare.Services.Options;
using WearWare.Services.TempMon;

namespace WearWare.Components.Pages.Options
{
    public partial class Options : DataPageBase
    {
        [Inject] private MatrixConfigService MatrixConfigService { get; set; } = null!;
        [Inject] private AppOptionsService AppOptionsService { get; set; } = null!;
        [Inject] private ITempMonService TempMonService { get; set; } = null!;
        [Inject] private IJSRuntime JS { get; set; } = null!;
        
        private bool showForm = false;
        private LedMatrixOptionsConfig modalOptions = new();
        private int _matrixBrightnessCap = 100;
        private int _liveBrightness = 100;

        protected override Task InitializeDataAsync()
        {
            _matrixBrightnessCap = MatrixConfigService.CloneOptions().Brightness ?? 100;
            _liveBrightness = Math.Clamp(MatrixConfigService.GetCurrentBrightness(), 1, _matrixBrightnessCap);
            MatrixConfigService.SetCurrentBrightness(_liveBrightness);
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

        private Task OnLiveBrightnessInput(ChangeEventArgs e)
        {
            if (!int.TryParse(e.Value?.ToString(), out var brightness))
            {
                return Task.CompletedTask;
            }

            _liveBrightness = Math.Clamp(brightness, 1, _matrixBrightnessCap);
            MatrixConfigService.SetCurrentBrightness(_liveBrightness);
            return Task.CompletedTask;
        }

        private Task SaveCurrentBrightness()
        {
            AppOptionsService.SaveCurrentBrightness(MatrixConfigService.GetCurrentBrightness());
            return Task.CompletedTask;
        }

        private Task StepUpCurrentBrightness()
        {
            var next = _liveBrightness < 5
                ? 5
                : ((_liveBrightness + 4) / 5) * 5 + (_liveBrightness % 5 == 0 ? 5 : 0);

            _liveBrightness = Math.Clamp(next, 1, _matrixBrightnessCap);
            MatrixConfigService.SetCurrentBrightness(_liveBrightness);
            _liveBrightness = MatrixConfigService.GetCurrentBrightness();
            return Task.CompletedTask;
        }

        private Task StepDownCurrentBrightness()
        {
            int next;
            if (_liveBrightness <= 5)
            {
                next = 1;
            }
            else if (_liveBrightness % 5 == 0)
            {
                next = _liveBrightness - 5;
            }
            else
            {
                next = (_liveBrightness / 5) * 5;
            }

            _liveBrightness = Math.Clamp(next, 1, _matrixBrightnessCap);
            MatrixConfigService.SetCurrentBrightness(_liveBrightness);
            _liveBrightness = MatrixConfigService.GetCurrentBrightness();
            return Task.CompletedTask;
        }

        private Task ResetCurrentBrightness()
        {
            var savedCurrent = AppOptionsService.GetCurrentBrightness();
            var target = savedCurrent ?? _matrixBrightnessCap;
            MatrixConfigService.SetCurrentBrightness(target);
            _liveBrightness = MatrixConfigService.GetCurrentBrightness();
            return Task.CompletedTask;
        }

        private Task OnSaveFromForm(LedMatrixOptionsConfig updated)
        {
            MatrixConfigService.UpdateOptions(updated);
            _matrixBrightnessCap = updated.Brightness ?? 100;
            _liveBrightness = MatrixConfigService.GetCurrentBrightness();
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