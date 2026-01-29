using Microsoft.AspNetCore.Components;
using WearWare.Services.MatrixConfig;

namespace WearWare.Components.Pages.Options
{
    public partial class Options
    {
        [Inject]
        private MatrixConfigService MatrixConfigService { get; set; } = null!;
        
        private bool showForm = false;
        private LedMatrixOptionsConfig modalOptions = new();

        protected override void OnInitialized()
        {
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
    }
}