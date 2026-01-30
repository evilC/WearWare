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
        [Inject] private ILogger<EditPlayableItemForm> _logger { get; set; } = null!;
        [Inject] private IJSRuntime JS { get; set; } = null!;
        [Inject] private MatrixConfigService MatrixConfigService { get; set; } = null!;

        [Parameter] public EditPlayableItemFormModel FormModel { get; set; } = default!;
        private readonly string _logTag = "EditPlayableItemForm";
        /// <summary>
        /// The PlayableItem being edited.
        /// In ADD mode, this will be a Library item clone.
        /// In EDIT mode, this will be the Playlist or QuickMedia item being edited.
        /// </summary>
        [Parameter] public PlayableItem? EditingItem { get; set; }
        /// <summary>
        /// The index of the item being edited in the Playlist or QuickMedia list.
        /// In ADD mode, this will be the index where the new item will be inserted.
        /// In EDIT mode, this will be the index of the item being edited.
        /// </summary>

        [Parameter] public int EditingIndex { get; set; }

        /// <summary>
        /// The URL of the image to display for the item being edited
        /// In ADD mode, this will be the Library image URL.
        /// In EDIT mode, this will be the Playlist or QuickMedia image URL depending on where the item is from.
        /// </summary>
        [Parameter] public string? ImageUrl { get; set; }
        [Parameter] public EventCallback OnCancel { get; set; }
        /// <summary>
        /// Callback for clicking OK in regular Add / Edit mode
        /// </summary>
        // ToDo: Delete once Import and QuickMedia uses OnNewSave
        [Parameter] public EventCallback<(int editingIndex, PlayableItem updatedItem, EditPlayableItemFormMode formMode)> OnSave { get; set; }
        [Parameter] public EventCallback<EditPlayableItemFormModel> OnNewSave { get; set; }
        /// <summary>
        /// Callback for clicking OK in ReConvert All mode
        /// </summary>
        [Parameter] public EventCallback<(EditPlayableItemFormMode formMode, int relativeBrightness, LedMatrixOptionsConfig? options)> OnReconvertAllOk { get; set; }

        // ToDo: Remove this and do convert on save instead of immediate convert on ReConvert
        // Needs to be left until QuickMedia page is updated to not use it
        [Parameter] public Func<LedMatrixOptionsConfig, int, Task<ReConvertTaskResult>>? OnReprocessAsync { get; set; }
        [Parameter] public EditPlayableItemFormPage FormPage { get; set; } = default!;
        [Parameter] public EditPlayableItemFormMode FormMode { get; set; } = EditPlayableItemFormMode.Edit;
        
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

        /*
        // Old way of setting variables when form is opened
        // Remove
        protected override void OnInitialized()
        {
            if (EditingItem == null)
            {
                // If we're being used for ReConvertAll flows, EditingItem may be null.
                // Initialize reasonable defaults so the dialog can render and be used
                // to select relative brightness and matrix options.
                if (FormMode == EditPlayableItemFormMode.ReConvertAllMatrix || FormMode == EditPlayableItemFormMode.ReConvertAllBrightness)
                {
                    selectedPlayMode = (int)PlayMode.Loop;
                    selectedPlayModeValue = 1;
                    selectedRelativeBrightness = 100;
                    selectedMatrixOptions = MatrixConfigService.CloneOptions();
                    nameInput = string.Empty;
                    importNameModel.Name = string.Empty;
                    // streamCurrentBrightness = selectedMatrixOptions?.Brightness ?? 100;
                    CalculateBrightness();
                    return;
                }
                _logger.LogError($"{_logTag}: EditingItem is null in EditPlayableItemForm");
                return;
            }
            selectedPlayMode = (int)EditingItem.PlayMode;
            selectedPlayModeValue = EditingItem.PlayModeValue;
            selectedRelativeBrightness = EditingItem.RelativeBrightness;
            selectedMatrixOptions = EditingItem.MatrixOptions.Clone();
            nameInput = EditingItem.Name;
            importNameModel.Name = nameInput;
            // streamCurrentBrightness = EditingItem.CurrentBrightness;
            CalculateBrightness();
        }
        */

        /// <summary>
        /// Called after the component has been rendered.
        /// Note: IDE says 0 references, but it is called by Blazor framework.
        /// </summary>
        /// <param name="firstRender">True if this is the first time the component is rendered</param>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JS.InvokeVoidAsync("import", "/js/ScrollbarHider.js");
                await JS.InvokeVoidAsync("modalScrollLock.lock");
            }
        }

        /// <summary>
        /// Unlocks scroll when the form is disposed 
        /// </summary>
        public async Task UnlockScrollAsync()
        {
            await JS.InvokeVoidAsync("modalScrollLock.unlock");
        }

        public async ValueTask DisposeAsync()
        {
            await JS.InvokeVoidAsync("modalScrollLock.unlock");
        }

        /// <summary>
        /// Called when the user changes the Play Mode radio buttons
        /// </summary>
        // private void OnSelectPlayMode(int value)
        // {
        //     selectedPlayMode = value;
        //     if (selectedPlayMode == (int)PlayMode.Forever)
        //     {
        //         selectedPlayModeValue = 1;
        //     }
        // }

        /// <summary>
        /// Called when the user changes the Play Mode Value input
        /// </summary>
        // private void OnSelectPlayModeValue(ChangeEventArgs e)
        // {
        //     if (int.TryParse(e.Value?.ToString(), out var v))
        //         selectedPlayModeValue = v;
        // }

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
                OnReconvertAllOk.InvokeAsync((FormModel.FormMode, FormModel.UpdatedItem.RelativeBrightness, FormModel.UpdatedItem.MatrixOptions));
                return;
            }
            OnNewSave.InvokeAsync(FormModel);
            /*
            if (EditingItem == null)
            {
                _logger.LogError($"{_logTag}: Cannot save PlayableItem; EditingItem is null");
                return;
            }
            if (selectedMatrixOptions == null)
            {
                _logger.LogError($"{_logTag}: Cannot save PlayableItem; selectedMatrixOptions is null");
                return;
            }
            // Copy mutable properties into the item to send back. For Import flows, Name is init-only,
            // so construct a new PlayableItem with the edited name.
            if (FormPage == EditPlayableItemFormPage.Import)
            {
                // Validate name via importNameModel; sanitize before creating PlayableItem
                var sanitized = FilenameValidator.Sanitize(importNameModel.Name ?? string.Empty);
                var updated = new PlayableItem(
                    sanitized,
                    EditingItem.ParentFolder,
                    EditingItem.MediaType,
                    EditingItem.SourceFileName,
                    (PlayMode)selectedPlayMode,
                    selectedPlayModeValue,
                    Math.Clamp(selectedRelativeBrightness, 1, 100),
                    adjustedBrightness,
                    selectedMatrixOptions.Clone()
                );
                OnSave.InvokeAsync((EditingIndex, updated, FormMode));
                return;
            }

            EditingItem.PlayMode = (PlayMode)selectedPlayMode;
            EditingItem.PlayModeValue = selectedPlayModeValue;
            EditingItem.RelativeBrightness = Math.Clamp(selectedRelativeBrightness, 1, 100);
            EditingItem.CurrentBrightness = adjustedBrightness;
            EditingItem.MatrixOptions = selectedMatrixOptions;
            OnSave.InvokeAsync((EditingIndex, EditingItem, FormMode));
            */
        }

        /// <summary>
        /// Called when user clicks Matrix Options button
        /// </summary>
        private void ShowMatrixOptions()
        {
            showMatrixOptionsForm = true;
        }

        /// <summary>
        /// Builds the title for the form based on mode and item type
        /// </summary>
        /// <returns>The page title</returns>
        private string BuildPageTitle()
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
                title += $" B{EditingIndex + 1}";
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
                EditPlayableItemFormPage.QuickMedia => $"Button {EditingIndex + 1} Matrix Options",
                _ => "Matrix Options"
            };
        }
    }
}