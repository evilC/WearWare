using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WearWare.Common.Media;
using WearWare.Components.Forms;
using WearWare.Services.Library;
using WearWare.Services.MatrixConfig;
using WearWare.Services.QuickMedia;
using WearWare.Services.StreamConverter;

namespace WearWare.Components.Pages.QuickMedia
{
    public partial class QuickMedia
    {
        [Inject]
        private QuickMediaService QuickMediaService { get; set; } = null!;
        [Inject]
        private LibraryService LibraryService { get; set; } = null!;
        [Inject]
        private IStreamConverterService StreamConverterService { get; set; } = null!;
        [Inject]
        private IJSRuntime JSRuntime { get; set; } = null!;

        // ================================================== Common ==================================================
        private IReadOnlyList<IQuickMediaButton?> quickButtons = Array.Empty<IQuickMediaButton?>();
        private IReadOnlyList<PlayableItem>? _libraryItems;

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
        private bool _showAddDialog = false;
        private int _addDialogInsertIndex = 0;
        private AddPlayableItemForm? _addFormRef;
        private bool _showReConvertAllDialog = false;
        private PlayableItemFormMode _reconvertAllMode;

        void OnAddDialogShow(int index)
        {
            _addDialogInsertIndex = index;
            _showAddDialog = true;
        }

        void OnAddDialogCancel()
        {
            _showAddDialog = false;
            StateHasChanged();
        }

        /// <summary>
        /// Called after we click "Add" and a playable item is picked from the list
        /// </summary>
        /// <param name="args"></param> Tuple of (insertIndex, libItem)
        async Task OnAddDialogItemChosen((int insertIndex, PlayableItem libItem) args)
        {
            _showAddDialog = false;
            await InvokeAsync(StateHasChanged);
            if (_addFormRef is not null)
                await _addFormRef.UnlockScrollAsync();
            await OnEditQuickMediaItem(args.libItem, args.insertIndex, PlayableItemFormMode.Add);
        }

        // ================================================== Edit Dialog  ==================================================
        private Boolean _showEditDialog = false;
        private int _editingIndex = -1;
        private PlayableItem? _editingItem = null;
        private PlayableItem? _originalItem;
        private EditPlayableItemForm? _editFormRef;

        /// <summary>
        /// The mode of the EditPlayableItemForm
        /// Not used by the form itself, but when it returns we can know if we were adding or editing
        /// </summary>
        private PlayableItemFormMode _formMode;

        /// <summary>
        /// Called when Edit is clicked on the QuickMedia page
        /// </summary>
        /// <param name="button"></param>
        /// <param name="index"></param>
        async Task OnEditQuickMediaItem(PlayableItem item, int index, PlayableItemFormMode mode)
        {
            _editingIndex = index;
            _originalItem = item;
            _editingItem = item.Clone();
            if (mode == PlayableItemFormMode.Add)
            {
                // In Add mode, the item being passed in is from the library, so we need to modify some properties.
                // We need to do this BEFORE opening the Edit form so that the form shows the correct values.
                // DO NOT modify ParentFolder at this point - the Service will need this to get the source file from the library
                // Set PlayMode to LOOP and PlayModeValue to 1 by default...
                // ... because library items have FOREVER mode by default
                _editingItem.PlayMode = PlayMode.LOOP;
                _editingItem.PlayModeValue = 1;
            }
            _formMode = mode;
            _showEditDialog = true;
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called when Save is clicked in the EditPlayableItemForm
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task OnSaveQuickMediaItem((int editingIndex, PlayableItem item, PlayableItemFormMode formMode) args)
        {
            if (_editingItem is null || _originalItem is null || _editingIndex < 0) return; // ToDo: Error handling
            await QuickMediaService.OnEditFormSubmit(_editingIndex, _originalItem, args.item, args.formMode);
            _showEditDialog = false;
            _editingItem = null;
            _originalItem = null;
            await InvokeAsync(StateHasChanged);
            if (_editFormRef is not null)
                await _editFormRef.UnlockScrollAsync();
        }

        /// <summary>
        /// Hides the Edit Item dialog - called on Cancel
        /// </summary>
        async Task OnEditFormCancel()
        {
            _showEditDialog = false;
            _showReConvertAllDialog = false;
            _editingItem = null;
            _originalItem = null;
            await InvokeAsync(StateHasChanged);
            if (_editFormRef is not null)
                await _editFormRef.UnlockScrollAsync();

        }

        /// <summary>
        /// Called to reprocess (re-convert) the quickmedia button's item with new options
        /// </summary>
        /// <param name="options"></param>
        /// <param name="relativeBrightness"></param>
        /// <returns></returns>
        /// ToDo: Needs to be removed - Reprocessing should be handled by the Service instead.
        /// Once removed, also remove:
        /// - OnReprocessAsync parameter from EditPlayableItemForm in this page.
        /// - OnReprocessAsync parameter from EditPlayableItemForm.razor
        private async Task<WearWare.Common.ReConvertTaskResult> ReprocessQuickMediaButtonAsync(WearWare.Services.MatrixConfig.LedMatrixOptionsConfig? options, int relativeBrightness)
        {
            if (_editingIndex < 0)
            {
                return new WearWare.Common.ReConvertTaskResult { ExitCode = -1, Error = "Invalid", Message = "No quickmedia selected", ActualBrightness = 0 };
            }
            var btn = quickButtons.ElementAtOrDefault(_editingIndex);
            if (btn?.Item is null)
            {
                return new WearWare.Common.ReConvertTaskResult { ExitCode = -1, Error = "Invalid", Message = "Selected quickmedia button has no item", ActualBrightness = 0 };
            }
            try
            {
                var folder = System.IO.Path.Combine(WearWare.Config.PathConfig.QuickMediaPath, _editingIndex.ToString());
                return await StreamConverterService.ConvertToStream(folder, btn.Item.SourceFileName, folder, btn.Item.Name, relativeBrightness, options);
            }
            catch (Exception ex)
            {
                return new WearWare.Common.ReConvertTaskResult { ExitCode = -1, Error = ex.Message, Message = "ReConvert failed", ActualBrightness = 0 };
            }
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
            _reconvertAllMode = PlayableItemFormMode.ReConvertAllMatrix;
            _showReConvertAllDialog = true;
        }

        /// <summary>
        /// Called when the Reconvert All (Embedded) is clicked
        /// </summary>
        private void ShowReConvertAllEmbedded()
        {
            _reconvertAllMode = PlayableItemFormMode.ReConvertAllBrightness;
            _showReConvertAllDialog = true;
        }

        /// <summary>
        /// Called when the Reconvert All form is submitted
        /// </summary>
        /// <param name="args"></param> The arguments containing the relative brightness, options and form mode
        private async Task OnReconvertAll((PlayableItemFormMode formMode, int relativeBrightness, LedMatrixOptionsConfig? options) args)
        {
            _showReConvertAllDialog = false;
            await QuickMediaService.ReConvertAllItems(args.formMode, args.relativeBrightness, args.options);
            if (_editFormRef is not null)
                await _editFormRef.UnlockScrollAsync();
            await InvokeAsync(StateHasChanged);
        }
    }
}