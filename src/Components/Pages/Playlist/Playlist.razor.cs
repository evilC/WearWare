using WearWare.Components.Base;
using WearWare.Components.Forms.AddCopyPlaylistForm;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Services.Library;
using WearWare.Services.OperationProgress;
using WearWare.Utils;
using WearWare.Services;
using WearWare.Services.Playlist;
using WearWare.Services.Mode;

namespace WearWare.Components.Pages.Playlist
{
    public partial class Playlist : DataPageBase
    {
        [Inject] public IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] public PlaylistManager PlaylistManager { get; set; } = null!;
        [Inject] public ModeHandler ModeHandler { get; set; } = null!;
        [Inject] public IOperationProgressService OperationProgress { get; set; } = null!;
        [Inject] public LibraryService LibraryService { get; set; } = null!;
        [Inject] public ILogger<Playlist> Logger { get; set; } = null!;

        private EditPlayableItemFormModel? _addFormModel = null;
        private EditPlayableItemFormModel? _editFormModel = null;

        /// <summary> The list of LibraryItems to choose from when adding to the playlist </summary>
        private IReadOnlyList<PlayableItem>? libraryItems;

        private readonly string _logTag = "Playlist.razor";
        /// <summary> The list of available playlist names </summary>
        private List<string>? availablePlaylists;
        /// <summary> The playlist being edited </summary>
        private PlaylistItems? _playlist;

        /// <summary> The list of PlayableItems in the current playlist</summary>
        private List<PlayableItem>? _items;

        private string? activePlaylist;

        // Dropdown for selecting which playlist to edit
        private string? _editingPlaylist;

        private AddCopyPlaylistFormModel _addCopyPlaylistModel = default!;

        private bool _subscribed = false;

        protected override Task InitializeDataAsync()
        {
            if (!_subscribed)
            {
                PlaylistManager.StateChanged += OnStateChanged;
                ModeHandler.StateChanged += OnStateChanged;
                _subscribed = true;
            }
            availablePlaylists = PlaylistManager.GetPlaylistNames();
            _editingPlaylist = PlaylistManager.EditingPlaylistName;
            activePlaylist = PlaylistManager.CurrentPlaylistName;
            _playlist = PlaylistManager.EditingPlaylist;
            if (_playlist is not null)
            {
                _items = _playlist.GetPlaylistItems();
            }
            libraryItems = LibraryService.Items;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            if (_subscribed)
            {
                PlaylistManager.StateChanged -= OnStateChanged;
                ModeHandler.StateChanged -= OnStateChanged;
                _subscribed = false;
            }
            base.Dispose();
        }

        /// <summary>
        /// Updates the page when the playlist manager state changes.
        /// </summary>
        private void OnStateChanged()
        {
            InvokeAsync(StateHasChanged);
        }

        private async Task OnActivePlaylistChanged(ChangeEventArgs e)
        {
            var newValue = e.Value?.ToString();
            if (newValue == "")
                newValue = null;
            if (activePlaylist != newValue)
            {
                activePlaylist = newValue;
                if (newValue is null)
                {
                    // Deactivate
                }
                else
                {
                    await ModeHandler.SelectPlaylistAsync(newValue);
                }
                StateHasChanged();
            }
        }

        /// <summary>
        /// Called to show the Add Item dialog to add an item at the specified index
        /// </summary>
        /// <param name="insertIndex"></param> The index to insert the item at
        void OnAddDialogShow(int insertIndex)
        {
            _addFormModel = new EditPlayableItemFormModel
            {
                FormMode = EditPlayableItemFormMode.Add,
                FormPage = EditPlayableItemFormPage.Playlist,
                ItemIndex = insertIndex,
            };
        }

        /// <summary>
        /// Called when Cancel is clicked in the AddPlayableItemForm
        /// </summary>
        async Task OnAddFormCancel()
        {
            _addFormModel = null;
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called after we click "Add" and a playable item is picked from the list
        /// </summary>
        /// <param name="args"></param> Tuple of (insertIndex, libItem)
        async Task OnAddDialogItemChosen(EditPlayableItemFormModel editFormModel)
        {
            if (_addFormModel is null || _playlist is null){
                Logger.LogError($"{_logTag}: OnAddDialogItemChosen called but _addFormModel or _playlist is null");
                return;
            }
            _addFormModel = null;
            editFormModel.UpdatedItem.PlayMode = PlayMode.Loop;
            editFormModel.UpdatedItem.PlayModeValue = 1;
            editFormModel.ImageUrl = BuildEditingImageURL(editFormModel);
            editFormModel.FormPage = EditPlayableItemFormPage.Playlist;
            editFormModel.FormMode = EditPlayableItemFormMode.Add;
            _editFormModel = editFormModel;
            await InvokeAsync(StateHasChanged);
            // await OnEditPlaylistItem(args.libItem, args.insertIndex, EditPlayableItemFormMode.Add);
        }

        /// <summary>
        /// Called when Edit is clicked on the Playlist page
        /// </summary>
        /// <param name="item"></param> The PlayableItem to edit
        /// <param name="itemIndex"></param> The index of the PlayableItem to edit
        async Task OnEditPlaylistItem(PlayableItem item, int itemIndex)
        {
            if (_playlist is null){
                Logger.LogError($"{_logTag}: OnEditPlaylistItem called but _playlist is null");
                return;
            }
            if (_editFormModel is not null && _editFormModel.FormMode != EditPlayableItemFormMode.Add){
                // It looks like this method was called from Add, but the _editFormModel is not set to Add mode
                Logger.LogError($"{_logTag}: OnEditPlaylistItem called but _editFormModel.FormMode is not Add");
                return;
            }
            // Edit was clicked from the Playlist page
            // Do not set _editFormModel until everything is ready, because form will show as soon as it's set
            var editFormModel = new EditPlayableItemFormModel() {
                FormMode = EditPlayableItemFormMode.Edit,
                FormPage = EditPlayableItemFormPage.Playlist,
                ItemIndex = itemIndex,
                OriginalItem = item,
                UpdatedItem = item.Clone(),
            };
            editFormModel.ImageUrl = BuildEditingImageURL(editFormModel);
            _editFormModel = editFormModel;

            await InvokeAsync(StateHasChanged);
        }

        async Task OnSavePlaylistItem(EditPlayableItemFormModel formModel)
        {
            if (_playlist is null || _editingPlaylist is null){
                Logger.LogError($"{_logTag}: OnSavePlaylistItem called but _playlist or _editingPlaylist is null");
                return;
            }
            _editFormModel = null;
            await PlaylistManager.UpsertPlaylistItemAsync(_editingPlaylist, formModel, OperationProgress);
            _playlist = PlaylistManager.GetPlaylist(_editingPlaylist);
            _items = _playlist?.GetPlaylistItems();
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

        /// <summary>
        /// Deletes the specified item from the playlist
        /// </summary>
        /// <param name="itemIndex"></param> The index of the item to delete
        void DeletePlaylistItem(int itemIndex)
        {
            if (_editingPlaylist is null)
                return;
            var removed = PlaylistManager.RemovePlaylistItem(_editingPlaylist, itemIndex);
            if (removed)
            {
                _playlist = PlaylistManager.GetPlaylist(_editingPlaylist);
                _items = _playlist?.GetPlaylistItems();
                StateHasChanged();
            }
        }

        private async Task ConfirmDeletePlaylistItem(int itemIndex)
        {
            if (_playlist is null) return;
            var name = _playlist.GetPlaylistItems()[itemIndex].Name;
            var ok = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete playlist item '{name}'?");
            if (ok)
            {
                DeletePlaylistItem(itemIndex);
            }
        }

        // ================================================== Enable Toggle ==================================================

        /// <summary>
        /// Called when the item's enabled checkbox is toggled
        /// </summary>
        /// <param name="item"></param> The PlayableItem being toggled
        /// <param name="itemIndex"></param> The index of the PlayableItem being toggled
        /// <param name="newState"></param> The new enabled state
        async void ItemEnableToggled(PlayableItem item, int itemIndex, bool newState)
        {
            if (_playlist is null || _editingPlaylist is null)
                return;
            var originalItem = item.Clone();
            item.Enabled = newState;
            var formModel = new EditPlayableItemFormModel()
            {
                FormMode = EditPlayableItemFormMode.Edit,
                FormPage = EditPlayableItemFormPage.Playlist,
                ItemIndex = itemIndex,
                OriginalItem = originalItem,
                UpdatedItem = item,
            };
            formModel.UpdatedItem.Enabled = newState;
            await PlaylistManager.UpsertPlaylistItemAsync(_editingPlaylist, formModel, OperationProgress);
            StateHasChanged();
        }

        // ================================================== Item Image Clicked ==================================================
        /// <summary>
        /// Handles when a PlaylistItem image is clicked - jumps to that item in the playlist
        /// </summary>
        /// <param name="item"></param> The PlayableItem that was clicked
        /// <param name="index"></param> The index of the PlayableItem that was clicked
        async Task OnPlaylistItemImageClicked(PlayableItem item, int index)
        {
            if (_editingPlaylist is null)
                return;
            
            // Only jump if editing the currently active playlist
            if (_editingPlaylist == PlaylistManager.CurrentPlaylistName)
            {
                await ModeHandler.JumpToPlaylistItemAsync(index);
            }
            else
            {
                // If editing a different playlist, just update the editing playlist position
                PlaylistManager.JumpToItemInPlaylist(_editingPlaylist, index);
            }
        }

        // ============================================ Active Playlist controls ===============================================
        
        private async Task StartActivePlaylist()
        {
            if (!string.IsNullOrEmpty(activePlaylist))
                await ModeHandler.SelectPlaylistAsync(activePlaylist);
            await ModeHandler.SetPlaylistRunningAsync(true);
        }

        private async Task StopActivePlaylist()
        {
            await ModeHandler.SetPlaylistRunningAsync(false);
        }

        /// <summary>
        /// Handles when the editing playlist selection is changed
        /// </summary>
        private void OnEditingPlaylistChanged(ChangeEventArgs e)
        {
            var newValue = e.Value?.ToString();
            if (_editingPlaylist != newValue)
            {
                _editingPlaylist = newValue;
                PlaylistManager.SetEditingPlaylist(_editingPlaylist);
                if (!string.IsNullOrEmpty(_editingPlaylist))
                {
                    _playlist = PlaylistManager.GetPlaylist(_editingPlaylist);
                    _items = _playlist?.GetPlaylistItems();
                }
                else
                {
                    _playlist = null;
                    _items = null;
                }
                StateHasChanged();
            }
        }

        /// <summary>
        /// Called when Add or Copy Playlist is clicked
        /// </summary>
        private void AddCopyPlaylistClicked(AddCopyPlaylistMode mode)
        {
            if (mode == AddCopyPlaylistMode.Copy && (_editingPlaylist is null || _editingPlaylist == "")) return;
            var model = new AddCopyPlaylistFormModel
            {
                Mode = mode,
            };
            if (mode == AddCopyPlaylistMode.Copy)
            {
                model.OldName = _editingPlaylist!;
                model.NewName = $"{_editingPlaylist}-";
            }
            _addCopyPlaylistModel = model;
            StateHasChanged();
        }

        // Called when Cancel is clicked in the Add/Copy Playlist form
        private void CancelAddCopyPlaylist()
        {
            _addCopyPlaylistModel = null!;
            StateHasChanged();
        }

        // Called when Submit is clicked in the Add/Copy Playlist form
        private async Task OnAddCopyPlaylistFormSubmit(AddCopyPlaylistFormModel model)
        {
            _addCopyPlaylistModel = null!;
            var sanitized = FilenameValidator.Sanitize(model.NewName);
            if (string.IsNullOrWhiteSpace(model.NewName))
            {
                return;
            }
            if (model.Mode == AddCopyPlaylistMode.Add)
            {
                PlaylistManager.AddPlaylist(model.NewName);
            }
            else if (model.Mode == AddCopyPlaylistMode.Copy)
            {
                await PlaylistManager.CopyPlaylistAsync(model.OldName, model.NewName);
            }
            else
            {
                Logger.LogError($"{_logTag}: OnAddCopyPlaylistFormSubmit called with unknown mode {model.Mode}");
                return;
            }
            availablePlaylists = PlaylistManager.GetPlaylistNames();
            OnEditingPlaylistChanged(new ChangeEventArgs { Value = sanitized });
            StateHasChanged();
        }

        /// <summary>
        /// Deletes the currently editing playlist
        /// </summary>
        private async Task DeletePlaylist()
        {
            if (!string.IsNullOrEmpty(_editingPlaylist))
            {
                await ModeHandler.DeletePlaylistAsync(_editingPlaylist);
                availablePlaylists = PlaylistManager.GetPlaylistNames();
                activePlaylist = PlaylistManager.CurrentPlaylistName;
                _editingPlaylist = null;
                _playlist = null;
                _items = null;
                StateHasChanged();
            }
        }

        private async Task ConfirmDeletePlaylist()
        {
            if (string.IsNullOrEmpty(_editingPlaylist)) return;
            var ok = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete playlist '{_editingPlaylist}' and all its files?");
            if (ok)
            {
                await DeletePlaylist();
            }
        }

        private string BuildEditingImageURL(EditPlayableItemFormModel formModel){
            if (formModel.FormMode == EditPlayableItemFormMode.Edit){
                if (_playlist is null){
                    Logger.LogError($"{_logTag}: BuildEditingImageURL called in EDIT mode but _playlist is null (We are not editing a Playlist)");
                    return string.Empty;
                }
                return $"/playlist-images/{_playlist.Name}/{formModel.OriginalItem.SourceFileName}";
            }
            return $"/library-image/{formModel.OriginalItem.SourceFileName}";        
        }
    }
}