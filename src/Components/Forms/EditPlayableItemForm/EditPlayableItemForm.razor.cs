using WearWare.Services.MatrixConfig;

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

        /// <summary> The z-index for this form </summary>
        [Parameter] public int ZIndex { get; set; } = 2000;

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

        // === Form edited values ===

        // === Form readouts ===
        // What the brightness WOULD BE if we reprocessed now with current global brightness and selected relative brightness
        private int adjustedBrightness;

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

        // Recalculates adjusted brightness based on current global brightness and selected relative brightness
        private void CalculateBrightness()
        {
            adjustedBrightness = BrightnessCalculator.CalculateAbsoluteBrightness(100, FormModel.UpdatedItem.RelativeBrightness);
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
            OnSave.InvokeAsync(FormModel);
        }

        /// <summary>
        /// Builds the title for this form based on mode and item type
        /// Called by 
        /// </summary>
        /// <returns>The page title</returns>
        public string BuildPageTitle()
        {
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

    }
}