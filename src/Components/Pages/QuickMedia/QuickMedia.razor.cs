using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WearWare.Common.Media;
using WearWare.Components.Forms.AddPlayableItemForm;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Services.Library;
using WearWare.Services.MatrixConfig;
using WearWare.Services.QuickMedia;

namespace WearWare.Components.Pages.QuickMedia
{
    public partial class QuickMedia
    {
        [Inject] private QuickMediaService QuickMediaService { get; set; } = null!;
        [Inject] private LibraryService LibraryService { get; set; } = null!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] private ILogger<QuickMedia> Logger { get; set; } = null!;
        [Inject] private MatrixConfigService _matrixConfigService { get; set; } = null!;

        private IReadOnlyList<IQuickMediaButton?> quickButtons = Array.Empty<IQuickMediaButton?>();
        private IReadOnlyList<PlayableItem>? _libraryItems;
        private EditPlayableItemFormModel? _addFormModel = null;
        private EditPlayableItemFormModel? _editFormModel = null;
        private AddPlayableItemForm? _addFormRef;
        private EditPlayableItemForm? _editFormRef;

        protected override void OnInitialized()
        {
            try
            {
                quickButtons = QuickMediaService?.GetQuickMediaButtons() ?? Array.Empty<IQuickMediaButton?>();
            }
            catch
            {
                quickButtons = Array.Empty<IQuickMediaButton?>();
            }
            _libraryItems = LibraryService.Items;
            if (QuickMediaService != null)
                QuickMediaService.StateChanged += OnStateChanged;
        }

        /// <summary>
        /// Handler for QuickMediaService StateChanged event.
        /// Currently, this is not used for anything (See notes in the Service)
        /// It's OK to remove the subscription, but do not remove the event itself from the service as the Mock page uses it.
        /// </summary>
        private void OnStateChanged()
        {
            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            if (QuickMediaService != null)
                QuickMediaService.StateChanged -= OnStateChanged;
        }

        // ================================================== Add Dialog  ==================================================
        void OnAddDialogShow(int index)
        {
            _addFormModel = new EditPlayableItemFormModel
            {
                FormMode = EditPlayableItemFormMode.Add,
                FormPage = EditPlayableItemFormPage.QuickMedia,
                ItemIndex = index,
            };
        }

        async Task OnAddDialogCancel()
        {
            _addFormModel = null;
            StateHasChanged();
        }

        /// <summary>
        /// Called after we click "Add" and a playable item is picked from the list
        /// </summary>
        /// <param name="args"></param> Tuple of (insertIndex, libItem)
        async Task OnAddDialogItemChosen(EditPlayableItemFormModel editFormModel)
        {
            _addFormModel = null;
            editFormModel.UpdatedItem.PlayMode = PlayMode.Loop;
            editFormModel.UpdatedItem.PlayModeValue = 1;
            editFormModel.ImageUrl = BuildEditingImageURL(editFormModel);
            _editFormModel = editFormModel;
            await InvokeAsync(StateHasChanged);
        }

        // ================================================== Edit Dialog  ==================================================

        /// <summary>
        /// Called when Edit is clicked on the QuickMedia page
        /// </summary>
        /// <param name="button"></param>
        /// <param name="index"></param>
        async Task OnEditQuickMediaItem(PlayableItem item, int index)
        {
            var editFormModel = new EditPlayableItemFormModel() {
                FormMode = EditPlayableItemFormMode.Edit,
                FormPage = EditPlayableItemFormPage.Playlist,
                ItemIndex = index,
                OriginalItem = item,
                UpdatedItem = item.Clone(),
            };
            editFormModel.ImageUrl = BuildEditingImageURL(editFormModel);
            _editFormModel = editFormModel;
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called when Save is clicked in the EditPlayableItemForm
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task OnSaveQuickMediaItem(EditPlayableItemFormModel formModel)
        {
            _editFormModel = null;
            await QuickMediaService.OnEditFormSubmit(formModel);
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Hides the Edit Item dialog - called on Cancel
        /// </summary>
        async Task OnEditFormCancel()
        {
            _editFormModel = null;
            await InvokeAsync(StateHasChanged);
        }

        // ================================================= Delete Item ==================================================
        void DeleteQuickMediaButton(int index)
        {
            QuickMediaService.DeleteQuickMediaButton(index);
            StateHasChanged();
        }

        private async Task ConfirmDeleteQuickMediaButton(int index)
        {
            var btn = quickButtons.ElementAtOrDefault(index);
            var name = btn?.Item?.Name ?? $"Button {index + 1}";
            var ok = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete quick-media button '{name}'?");
            if (ok)
            {
                DeleteQuickMediaButton(index);
            }
        }

        /// <summary>
        /// Called when the Reconvert All (Global) is clicked
        /// </summary>
        private void ShowReConvertAllGlobal()
        {
            _editFormModel = new EditPlayableItemFormModel()
            {
                FormMode = EditPlayableItemFormMode.ReConvertAllMatrix,
                FormPage = EditPlayableItemFormPage.QuickMedia,
                UpdatedItem = PlayableItem.CreateDummyItem(_matrixConfigService.CloneOptions()),
            };
        }

        /// <summary>
        /// Called when the Reconvert All (Embedded) is clicked
        /// </summary>
        private void ShowReConvertAllEmbedded()
        {
            _editFormModel = new EditPlayableItemFormModel()
            {
                FormMode = EditPlayableItemFormMode.ReConvertAllBrightness,
                FormPage = EditPlayableItemFormPage.QuickMedia,
                UpdatedItem = PlayableItem.CreateDummyItem(_matrixConfigService.CloneOptions()),
            };

        }

        /// <summary>
        /// Called when the Reconvert All form is submitted
        /// </summary>
        /// <param name="formModel"></param> The arguments containing the relative brightness, options and form mode
        private async Task OnReconvertAll(EditPlayableItemFormModel formModel)
        {
            _editFormModel = null;
            await QuickMediaService.ReConvertAllItems(formModel.FormMode, formModel.UpdatedItem.RelativeBrightness, formModel.UpdatedItem.MatrixOptions);
            await InvokeAsync(StateHasChanged);
        }

        private string BuildEditingImageURL(EditPlayableItemFormModel formModel){
            if (formModel.FormMode == EditPlayableItemFormMode.Edit){
                return $"/quickmedia-images/{formModel.ItemIndex}/{formModel.OriginalItem.SourceFileName}";
            }
            return $"/library-image/{formModel.OriginalItem.SourceFileName}";        
        }
    }
}