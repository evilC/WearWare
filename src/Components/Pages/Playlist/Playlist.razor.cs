using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WearWare.Common.Media;
using WearWare.Components.Forms.AddCopyPlaylistForm;
using WearWare.Components.Forms.AddPlayableItemForm;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Services.Library;
using WearWare.Services.MatrixConfig;
using WearWare.Services.Playlist;
using WearWare.Utils;

namespace WearWare.Components.Pages.Playlist
{
    public partial class Playlist
    {
        [Inject] public IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] public PlaylistService PlaylistService { get; set; } = null!;
        [Inject] public LibraryService LibraryService { get; set; } = null!;
        [Inject] public ILogger<Playlist> Logger { get; set; } = null!;
        [Inject] public MatrixConfigService _matrixConfigService { get; set; } = null!;

        private EditPlayableItemFormModel? _addFormModel = null;
        private EditPlayableItemFormModel? _editFormModel = null;

        /// <summary> The list of LibraryItems to choose from when adding to the playlist </summary>
        private IReadOnlyList<PlayableItem>? libraryItems;
        /// <summary> Reference to the AddPlayableItemForm component </summary>
        private AddPlayableItemForm? _addFormRef;
        // Handler for AddPlayableItemForm component's OnAdd event

        private readonly string _logTag = "Playlist.razor";
        /// <summary> The list of available playlist names </summary>
        private List<string>? availablePlaylists;
        /// <summary> The playlist being edited </summary>
        private PlaylistItems? _playlist;

        /// <summary> The list of PlayableItems in the current playlist</summary>
        private List<PlayableItem>? _items;

        private string? activePlaylist;

        /// <summary> Reference to the EditPlayableItemForm component </summary>
        private EditPlayableItemForm? _editFormRef;

        // Dropdown for selecting which playlist to edit
        private string? _editingPlaylist;

        private AddCopyPlaylistFormModel _addCopyPlaylistModel = default!;

        // private bool showAddPlaylistModal = false;
        // private string newPlaylistName = string.Empty;
        // private string? addPlaylistError = null;

        protected override void OnInitialized()
        {
            availablePlaylists = PlaylistService.GetPlaylistNames();
            _editingPlaylist = PlaylistService.GetPlaylistBeingEdited()?.Name;
            // Set activePlaylist to the currently active playlist, if any
            activePlaylist = PlaylistService.GetActivePlaylistName();
            _playlist = PlaylistService.GetPlaylistBeingEdited();
            if (_playlist is not null)
            {
                _items = _playlist.GetPlaylistItems();
            }
            libraryItems = LibraryService.Items;
            PlaylistService.StateChanged += OnStateChanged;
        }

        /// <summary>
        /// Handler for PlaylistService StateChanged event.
        /// The MediaControllerService raises it's own StateChanged when playback fails for an item
        /// which PlaylistService listens to and raises it's own StateChanged event.
        /// This allows UIs to update when an item is disabled due to playback failure.
        /// </summary>
        private void OnStateChanged()
        {
            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            PlaylistService.StateChanged -= OnStateChanged;
        }

        private void OnActivePlaylistChanged(ChangeEventArgs e)
        {
            var newValue = e.Value?.ToString();
            if (newValue == "")
                newValue = null;
            if (activePlaylist != newValue)
            {
                activePlaylist = newValue;
                if (newValue is null)
                {
                    PlaylistService.SetActivePlaylistState(false);
                }
                else
                {
                    PlaylistService.SetActivePlaylistState(true, newValue);
                }
                // Optionally, you can load the playlist or update UI here
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

        // ToDo: Update PlaylistService.OnEditFormSubmit to accept EditPlayableItemFormModel
        async Task OnSavePlaylistItem(EditPlayableItemFormModel formModel)
        {
            if (_playlist is null){
                Logger.LogError($"{_logTag}: OnSavePlaylistItem called but _playlist is null");
                return;
            }
            _editFormModel = null;
            await PlaylistService.OnEditFormSubmit(_playlist, formModel);
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
            if (_playlist is null)
                return;
            var removed = PlaylistService.RemovePlaylistItem(_playlist, itemIndex);
            if (removed)
            {
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
            if (_playlist is null)
                return;
            var originalItem = item.Clone();    // Take a clone so that we can pass the original state (Enabled in original state) to the service
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
            await PlaylistService.OnEditFormSubmit(_playlist, formModel);
            StateHasChanged();
        }

        // ================================================== Item Image Clicked ==================================================
        /// <summary>
        /// Handles when a PlaylistItem image is clicked - jumps to that item in the playlist
        /// </summary>
        /// <param name="item"></param> The PlayableItem that was clicked
        /// <param name="index"></param> The index of the PlayableItem that was clicked
        void OnPlaylistItemImageClicked(PlayableItem item, int index)
        {
            if (_playlist is null)
                return;
            PlaylistService.JumpToPlaylistItem(_playlist, index);
        }

        // ============================================ Active Playlist controls ===============================================
        
        private void StartActivePlaylist()
        {
            if (!string.IsNullOrEmpty(activePlaylist))
            {
                PlaylistService.SetActivePlaylistState(true, activePlaylist);
            }
        }

        private void StopActivePlaylist()
        {
            if (!string.IsNullOrEmpty(activePlaylist))
            {
                PlaylistService.SetActivePlaylistState(false);
            }
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
                PlaylistService.OnEditingPlaylistChanged(_editingPlaylist);
                if (!string.IsNullOrEmpty(_editingPlaylist))
                {
                    _playlist = PlaylistService.GetPlaylist(_editingPlaylist);
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
            if (_editingPlaylist is null || _editingPlaylist == "") return;
            var model = new AddCopyPlaylistFormModel
            {
                Mode = mode,
            };
            if (mode == AddCopyPlaylistMode.Copy)
            {
                model.OldName = _editingPlaylist;
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
        private void OnAddCopyPlaylistFormSubmit(AddCopyPlaylistFormModel model)
        {
            _addCopyPlaylistModel = null!;
            var sanitized = FilenameValidator.Sanitize(model.NewName);
            // Replace spaces and invalid folder characters with '-'
            if (string.IsNullOrWhiteSpace(model.NewName))
            {
                return;
            }
            if (model.Mode == AddCopyPlaylistMode.Add)
            {
                PlaylistService.AddPlaylist(model.NewName);
            }
            else if (model.Mode == AddCopyPlaylistMode.Copy)
            {
                // PlaylistService.CopyPlaylist(model.OldName, model.NewName);
            }
            else
            {
                Logger.LogError($"{_logTag}: OnAddCopyPlaylistFormSubmit called with unknown mode {model.Mode}");
                return;
            }
            availablePlaylists = PlaylistService.GetPlaylistNames();
            // Automatically select the new playlist in the Editing DDL by firing the change event only
            OnEditingPlaylistChanged(new ChangeEventArgs { Value = sanitized });
            StateHasChanged();
        }

        /// <summary>
        /// Deletes the currently editing playlist
        /// </summary>
        private void DeletePlaylist()
        {
            if (!string.IsNullOrEmpty(_editingPlaylist))
            {
                // If the playlist being deleted is also active, reset the active DDL
                if (activePlaylist == _editingPlaylist)
                {
                    activePlaylist = null;
                }
                PlaylistService.DeletePlaylist(_editingPlaylist);
                availablePlaylists = PlaylistService.GetPlaylistNames();
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
                DeletePlaylist();
            }
        }

        /// <summary>
        /// Called when the Reconvert All (Matrix) is clicked
        /// </summary>
        private void ShowReConvertAllMatrix()
        {
            // _reconvertAllMode = EditPlayableItemFormMode.ReConvertAllMatrix;
            // _showReConvertAllDialog = true;
            _editFormModel = new EditPlayableItemFormModel()
            {
                FormMode = EditPlayableItemFormMode.ReConvertAllMatrix,
                FormPage = EditPlayableItemFormPage.Playlist,
                UpdatedItem = PlayableItem.CreateDummyItem(_matrixConfigService.CloneOptions()),
            };
        }

        /// <summary>
        /// Called when the Reconvert All (Brightness) is clicked
        /// </summary>
        private void ShowReConvertAllBrightness()
        {
            // _reconvertAllMode = EditPlayableItemFormMode.ReConvertAllBrightness;
            // _showReConvertAllDialog = true;
            _editFormModel = new EditPlayableItemFormModel()
            {
                FormMode = EditPlayableItemFormMode.ReConvertAllBrightness,
                FormPage = EditPlayableItemFormPage.Playlist,
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
            await PlaylistService.ReConvertAllItems(formModel.FormMode, formModel.UpdatedItem.RelativeBrightness, formModel.UpdatedItem.MatrixOptions);
            await InvokeAsync(StateHasChanged);
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