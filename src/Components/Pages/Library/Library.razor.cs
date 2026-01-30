using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WearWare.Common.Media;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Services.Library;
using WearWare.Services.MatrixConfig;

namespace WearWare.Components.Pages.Library
{
    public partial class Library
    {
        [Inject] private LibraryService LibraryService { get; set; } = null!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] private MatrixConfigService MatrixConfigService { get; set; } = null!;
        private EditPlayableItemFormModel? _editFormModel;
        private EditPlayableItemForm? _editFormRef;
        private IReadOnlyList<PlayableItem>? items;

        protected override void OnInitialized()
        {
            LibraryService.ItemsChanged += OnItemsChanged;
            items = LibraryService.Items;
        }

        private async Task UnlockScrollbar(){
            if (_editFormRef is not null)
                await _editFormRef.UnlockScrollAsync();
        }

        private void OnItemsChanged()
        {
            items = LibraryService.Items;
            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            LibraryService.ItemsChanged -= OnItemsChanged;
        }

        private async Task ConfirmDelete(PlayableItem item)
        {
            var ok = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete library item '{item.Name}' and its files?");
            if (ok)
            {
                LibraryService.DeleteLibraryItem(item);
            }
        }

        /// <summary>
        /// Called when Reconvert is clicked for a library item.
        /// </summary>
        /// <param name="item"></param>
        private void ShowReconvert(PlayableItem item)
        {
            _editFormModel = new EditPlayableItemFormModel()
            {
                FormMode = EditPlayableItemFormMode.Edit,
                FormPage = EditPlayableItemFormPage.Library,
                ImageUrl = $"/library-image/{item.SourceFileName}",
                OriginalItem = item,
                UpdatedItem = item.Clone(),
            };
        }

        /// <summary>
        /// Called when the edit form is cancelled or closed.
        /// </summary>
        private async Task OnEditFormCancel()
        {
            _editFormModel = null;
            await UnlockScrollbar();
        }

        /// <summary>
        /// Called when the edit form is submitted for a library item.
        /// </summary>
        /// <param name="args"></param> The arguments containing the updated item and form mode
        private async Task OnSaveLibraryItem(EditPlayableItemFormModel formModel)
        {
            _editFormModel = null;
            await LibraryService.OnEditFormSubmit(formModel);
            await UnlockScrollbar();
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called when the Reconvert All (Global) is clicked
        /// </summary>
        private void ShowReConvertAllGlobal()
        {
            _editFormModel = new EditPlayableItemFormModel()
            {
                FormMode = EditPlayableItemFormMode.ReConvertAllMatrix,
                FormPage = EditPlayableItemFormPage.Library,
                UpdatedItem = PlayableItem.CreateDummyItem(MatrixConfigService.CloneOptions()),
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
                FormPage = EditPlayableItemFormPage.Library,
                UpdatedItem = PlayableItem.CreateDummyItem(MatrixConfigService.CloneOptions()),
            };
        }

        /// <summary>
        /// Called when the Reconvert All form is submitted
        /// </summary>
        /// <param name="args"></param> The arguments containing the relative brightness, options and form mode
        private async Task OnReconvertAll((EditPlayableItemFormMode formMode, int relativeBrightness, LedMatrixOptionsConfig? options) args)
        {
            _editFormModel = null;
            await LibraryService.ReConvertAllItems(args.formMode, args.relativeBrightness, args.options);
            await UnlockScrollbar();
            await InvokeAsync(StateHasChanged);
        }
    }
}