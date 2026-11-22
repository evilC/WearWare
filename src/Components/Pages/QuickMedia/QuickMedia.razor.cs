using WearWare.Components.Base;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Services.Library;
using WearWare.Services.QuickMedia;
using WearWare.Services.OperationProgress;
using WearWare.Architecture;
using WearWare.Architecture.QuickMedia;
using WearWare.Utils;

namespace WearWare.Components.Pages.QuickMedia
{
    public partial class QuickMedia : DataPageBase
    {
        [Inject] private QuickMediaManager QuickMediaManager { get; set; } = null!;
        [Inject] private LibraryService LibraryService { get; set; } = null!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] private ILogger<QuickMedia> Logger { get; set; } = null!;
        [Inject] private IOperationProgressService OperationProgress { get; set; } = null!;

        private List<PlayableItem?> _quickMediaItems = [];
        private List<int> _quickMediaButtonNumbers = [];
        private IReadOnlyList<PlayableItem>? _libraryItems;
        private EditPlayableItemFormModel? _addFormModel = null;
        private EditPlayableItemFormModel? _editFormModel = null;
        private bool _subscribed;

        protected override Task InitializeDataAsync()
        {
            if (!_subscribed)
            {
                QuickMediaManager.StateChanged += OnManagerStateChanged;
                _subscribed = true;
            }
            LoadQuickMediaItems();
            _libraryItems = LibraryService.Items;
            return Task.CompletedTask;
        }

        private void LoadQuickMediaItems()
        {
            _quickMediaItems = new List<PlayableItem?>();
            _quickMediaButtonNumbers = QuickMediaManager.GetQuickMediaButtonNumbers();
            foreach (var buttonNumber in _quickMediaButtonNumbers)
            {
                _quickMediaItems.Add(QuickMediaManager.GetButtonItem(buttonNumber));
            }
        }

        public override void Dispose()
        {
            if (_subscribed)
            {
                QuickMediaManager.StateChanged -= OnManagerStateChanged;
                _subscribed = false;
            }
            base.Dispose();
        }

        private void OnManagerStateChanged()
        {
            InvokeAsync(() =>
            {
                LoadQuickMediaItems();
                StateHasChanged();
            });
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
        async Task OnEditQuickMediaItem(PlayableItem item, int index)
        {
            var editFormModel = new EditPlayableItemFormModel() {
                FormMode = EditPlayableItemFormMode.Edit,
                FormPage = EditPlayableItemFormPage.QuickMedia,
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
        public async Task OnSaveQuickMediaItem(EditPlayableItemFormModel formModel)
        {
            _editFormModel = null;
            var opId = await OperationProgress.StartOperation($"{(formModel.FormMode == EditPlayableItemFormMode.Add ? "Adding" : "Editing")} Quick Media Item");
            
            try
            {
                if (formModel.FormMode == EditPlayableItemFormMode.Edit)
                {
                    var originalItem = formModel.OriginalItem;
                    originalItem.UpdateFromClone(formModel.UpdatedItem);
                    QuickMediaManager.UpsertButtonItem(formModel.ItemIndex, originalItem);
                    OperationProgress.CompleteOperation(opId, true, "Done");
                }
                else
                {
                    // ADD mode
                    var newItem = formModel.UpdatedItem;
                    newItem.ParentFolder = Path.Combine(PathConfig.QuickMediaFolder, formModel.ItemIndex.ToString());
                    
                    // Copy fseq file
                    var copyFrom = formModel.OriginalItem.GetFseqFilePath();
                    var copyTo = newItem.GetFseqFilePath();
                    await FileUtils.CopyFileAsync(copyFrom, copyTo).ConfigureAwait(false);
                    
                    // Copy source file
                    var srcCopyFrom = formModel.OriginalItem.GetSourceFilePath();
                    var srcCopyTo = newItem.GetSourceFilePath();
                    if (!File.Exists(srcCopyTo))
                    {
                        await FileUtils.CopyFileAsync(srcCopyFrom, srcCopyTo).ConfigureAwait(false);
                    }
                    
                    if (!QuickMediaManager.UpsertButtonItem(formModel.ItemIndex, newItem))
                        throw new InvalidOperationException("Could not save Quick Media item. See the log for details.");
                    OperationProgress.CompleteOperation(opId, true, "Done");
                }
            }
            catch (Exception ex)
            {
                OperationProgress.CompleteOperation(opId, false, $"Error: {ex.Message}");
                Logger.LogError(ex, "Error saving quick media item");
            }
            
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
        async Task DeleteQuickMediaButton(int buttonNumber)
        {
            await QuickMediaManager.DeleteButtonItemAsync(buttonNumber);
            StateHasChanged();
        }

        private async Task ConfirmDeleteQuickMediaButton(int buttonNumber)
        {
            var ok = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete QuickMedia item in slot B{buttonNumber}?");
            if (ok)
            {
                await DeleteQuickMediaButton(buttonNumber);
            }
        }

        private string BuildEditingImageURL(EditPlayableItemFormModel formModel){
            if (formModel.FormMode == EditPlayableItemFormMode.Edit){
                return $"/quickmedia-images/{formModel.ItemIndex}/{formModel.OriginalItem.SourceFileName}";
            }
            return $"/library-image/{formModel.OriginalItem.SourceFileName}";        
        }
    }
}