using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WearWare.Common;
using WearWare.Common.Media;
using WearWare.Services.MatrixConfig;
using WearWare.Utils;

namespace WearWare.Components.Forms.EditPlayableItemForm
{
    /// <summary>
    /// The EditPlayableItemForm is actually used for both Add and Edit
    /// In Add mode, an item is first chosen, but after that, the flow is basically the same as Edit
    /// </summary>
    public partial class EditPlayableItemForm
    {
        private readonly string _logTag = "EditPlayableItemForm";
        [Inject] private ILogger<EditPlayableItemForm> _logger { get; set; } = null!;
        [Inject] private IJSRuntime JS { get; set; } = null!;
        [Inject] private MatrixConfigService MatrixConfigService { get; set; } = null!;

        /// <summary>
        /// The model for the form
        /// </summary>
        [Parameter] public EditPlayableItemFormModel FormModel { get; set; } = default!;

        /// <summary>
        /// Callback for clicking Cancel
        /// </summary>
        [Parameter] public EventCallback OnCancel { get; set; }
        /// <summary>
        /// Callback for clicking OK in regular Add / Edit mode
        /// </summary>
        [Parameter] public EventCallback<EditPlayableItemFormModel> OnSave { get; set; }
        /// <summary>
        /// Callback for clicking OK in ReConvert All mode
        /// </summary>
        [Parameter] public EventCallback<EditPlayableItemFormModel> OnReconvertAllOk { get; set; }
        
        // === Form edited values ===

        // === Form readouts ===
        // What the brightness WOULD BE if we reprocessed now with current matrix options and selected relative brightness
        private int adjustedBrightness;

        // === Misc ===
        // True when the Matrix Options form is visible
        private bool showMatrixOptionsForm = false;

        /// <summary>
        /// Called when the form opens
        /// </summary>
        // ToDo: Whould we be using the Async version of this?
        protected override void OnInitialized()
        {
            // Sync FormModel.UpdatedItem properties to local selected* variables
            if (FormModel != null)
            {
                if (FormModel.FormMode == EditPlayableItemFormMode.Add)
                {
                    FormModel.UpdatedItem.PlayMode = PlayMode.Loop;
                    FormModel.UpdatedItem.PlayModeValue = 1;
                }
                CalculateBrightness();
            }
        }

        /// <summary>
        /// Called after the component has been rendered.
        /// Note: IDE says 0 references, but it is called by Blazor framework.
        /// </summary>
        /// <param name="firstRender">True if this is the first time the component is rendered</param>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
            }
        }

        // Recalculates adjusted brightness based on current matrix options and selected relative brightness
        private void CalculateBrightness()
        {
            adjustedBrightness = BrightnessCalculator.CalculateAbsoluteBrightness(
                FormModel.UpdatedItem.RelativeBrightness, FormModel.UpdatedItem.MatrixOptions.Brightness ?? 100
            );
        }

        /// <summary>
        /// Called after the user clicks OK in the Matrix Options form
        /// </summary>
        /// <param name="opts">The updated matrix options</param>
        private async Task OnMatrixOptionsOk(LedMatrixOptionsConfig opts)
        {
            // selectedMatrixOptions = opts;
            showMatrixOptionsForm = false;
            CalculateBrightness();
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called after the user clicks Cancel in the Matrix Options form
        /// </summary>
        private Task OnMatrixOptionsCancel()
        {
            showMatrixOptionsForm = false;
            InvokeAsync(StateHasChanged);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when user clicks Save button
        /// </summary>
        private void SaveEdit()
        {
            if (FormModel is null)
            {
                _logger.LogError($"{_logTag}: Cannot save PlayableItem; FormModel is null");
                return;
            }
            // If we're in a ReConvertAll mode, call the dedicated callback instead
            if (FormModel.FormMode == EditPlayableItemFormMode.ReConvertAllMatrix || FormModel.FormMode == EditPlayableItemFormMode.ReConvertAllBrightness)
            {
                OnReconvertAllOk.InvokeAsync(FormModel);
                return;
            }
            OnSave.InvokeAsync(FormModel);
        }

        /// <summary>
        /// Called when user clicks Matrix Options button
        /// </summary>
        private void ShowMatrixOptions()
        {
            showMatrixOptionsForm = true;
        }

        /// <summary>
        /// Builds the title for this form based on mode and item type
        /// Called by 
        /// </summary>
        /// <returns>The page title</returns>
        public string BuildPageTitle()
        {
            if (FormModel.FormMode == EditPlayableItemFormMode.ReConvertAllMatrix)
            {
                return $"ReConvert {FormModel.FormPage} (Matrix Options)";
            }
            else if (FormModel.FormMode == EditPlayableItemFormMode.ReConvertAllBrightness)
            {
                return $"ReConvert {FormModel.FormPage} (Brightness)";
            }
            var title = FormModel.FormPage == EditPlayableItemFormPage.Import ? "" : $"{FormModel.FormMode} ";
            title += FormModel.FormPage.ToString();
            if (FormModel.FormPage == EditPlayableItemFormPage.QuickMedia)
            {
                title += $" B{FormModel.ItemIndex + 1}";
            }
            else
            {
                title += " Item";
            }
            return title;
        }

        /// <summary>
        /// Builds the title for the Matrix Options form based on item type
        /// </summary>
        /// <returns>The title for the matrix options form</returns>
        private string BuildMatrixOptionsTitle()
        {
            return FormModel.FormPage switch
            {
                EditPlayableItemFormPage.Library => "Library Item Matrix Options",
                EditPlayableItemFormPage.Playlist => "Playlist Item Matrix Options",
                EditPlayableItemFormPage.QuickMedia => $"Button {FormModel.ItemIndex + 1} Matrix Options",
                _ => "Matrix Options"
            };
        }
    }
}