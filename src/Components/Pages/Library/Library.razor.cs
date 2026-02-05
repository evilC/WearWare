using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WearWare.Common.Media;
using WearWare.Components.Base;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Services.Library;
using WearWare.Services.MatrixConfig;

namespace WearWare.Components.Pages.Library
{
    public partial class Library : DataPageBase
    {
        [Inject] private LibraryService LibraryService { get; set; } = null!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] private MatrixConfigService MatrixConfigService { get; set; } = null!;
        private EditPlayableItemFormModel? _editFormModel;
        private IReadOnlyList<PlayableItem>? items;
        private bool _subscribed;

        protected override Task InitializeDataAsync()
        {
            if (!_subscribed)
            {
                LibraryService.ItemsChanged += OnItemsChanged;
                _subscribed = true;
            }

            items = LibraryService.Items;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            if (_subscribed)
            {
                LibraryService.ItemsChanged -= OnItemsChanged;
                _subscribed = false;
            }
            base.Dispose();
        }

        /// <summary>
        /// Called when the library items have changed. Updates the local items list and triggers a re-render.
         /// Note that this may be called before the circuit is interactive, so we don't use any JS interop here.
         /// Instead, we just update state and let the normal render cycle show any errors or updated data once ready.
         /// This can be triggered by changes to the library folder, so it's important that it doesn't assume interactivity or loaded data.
        /// </summary>
        private void OnItemsChanged()
        {
            items = LibraryService.Items;
            InvokeAsync(StateHasChanged);
        }

        private async Task ConfirmDelete(PlayableItem item)
        {
            var ok = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete library item '{item.Name}' and its files?");
            if (ok)
            {
                LibraryService.DeleteLibraryItem(item);
            }
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called when Edit is clicked for a library item.
        /// </summary>
        /// <param name="item"></param>
        private void OnEditClicked(PlayableItem item)
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
        /// Called when the item card is clicked for a library item.
        /// Plays a preview of the item.
        /// </summary>
        /// <param name="item"></param>
        private void OnItemClicked(PlayableItem item)
        {
            LibraryService.PlayPreviewItem(item);
        }

        /// <summary>
        /// Called when the edit form is cancelled or closed.
        /// </summary>
        private async Task OnEditFormCancel()
        {
            _editFormModel = null;
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called when the edit form is submitted for a library item.
        /// </summary>
        /// <param name="args"></param> The arguments containing the updated item and form mode
        private async Task OnSaveLibraryItem(EditPlayableItemFormModel formModel)
        {
            _editFormModel = null;
            await LibraryService.OnEditFormSubmit(formModel);
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
        private async Task OnReconvertAll(EditPlayableItemFormModel formModel)
        {
            _editFormModel = null;
            await LibraryService.ReConvertAllItems(formModel.FormMode, formModel.UpdatedItem.RelativeBrightness, formModel.UpdatedItem.MatrixOptions);
            await InvokeAsync(StateHasChanged);
        }
    }
}