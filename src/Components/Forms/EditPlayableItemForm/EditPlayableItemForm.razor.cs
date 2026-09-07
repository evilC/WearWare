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
            }
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