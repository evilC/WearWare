using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WearWare.Common.Media;
using WearWare.Components.Forms.AddPlayableItemForm;
using WearWare.Components.Forms.EditPlayableItemForm;
using WearWare.Services.Library;
using WearWare.Services.MatrixConfig;
using WearWare.Services.Playlist;

namespace WearWare.Components.Pages.Playlist
{
    public partial class Playlist
    {
        [Inject] public IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] public PlaylistService PlaylistService { get; set; } = null!;
        [Inject] public LibraryService LibraryService { get; set; } = null!;
        [Inject] public ILogger<Playlist> Logger { get; set; } = null!;

        /// <summary> Whether to show the Add Item dialog </summary>
        // private bool showAddDialog = false;
        /// <summary> The index to insert the new item at when adding to the playlist </summary>
        // private int addDialogInsertIndex = 0;

        private EditPlayableItemFormDto? _addFormDto = null;
        private EditPlayableItemFormDto? _editFormDto = null;

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

        /// <summary> Whether to show the Edit Item dialog </summary>
        private bool _showEditDialog = false;

        /// <summary> The PlayableItem being edited in the Edit dialog </summary>
        private PlayableItem? _editingItem;

        /// <summary> A copy of the original PlayableItem (pre-editing) </summary>
        private PlayableItem? _originalItem;

        /// <summary> Reference to the EditPlayableItemForm component </summary>
        private EditPlayableItemForm? _editFormRef;

        /// <summary> The index of the PlayableItem being edited in the Edit dialog </summary>
        private int _editingIndex;

        /// <summary>
        /// The mode of the EditPlayableItemForm
        /// Not used by the form itself, but when it returns we can know if we were adding or editing
        /// </summary>
        private EditPlayableItemFormMode _formMode;

        private bool _showReConvertAllDialog = false;
        private EditPlayableItemFormMode _reconvertAllMode;

        // Dropdown for selecting which playlist to edit
        private string? _editingPlaylist;

        private bool showAddPlaylistModal = false;
        private string newPlaylistName = string.Empty;
        private string? addPlaylistError = null;

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
            _addFormDto = new EditPlayableItemFormDto
            {
                FormMode = EditPlayableItemFormMode.Add,
                FormPage = EditPlayableItemFormPage.Playlist,
                InsertIindex = insertIndex
            };
        }

        /// <summary>
        /// Called when Cancel is clicked in the AddPlayableItemForm
        /// </summary>
        async Task OnAddFormCancel()
        {
            _addFormDto = null;
            await InvokeAsync(StateHasChanged);
            if (_addFormRef is not null)
                await _addFormRef.UnlockScrollAsync();
        }

        /// <summary>
        /// Called after we click "Add" and a playable item is picked from the list
        /// </summary>
        /// <param name="args"></param> Tuple of (insertIndex, libItem)
        async Task OnAddDialogItemChosen((int insertIndex, PlayableItem libItem) args)
        {
            if (_addFormDto is null || _playlist is null) return;
            _editFormDto = _addFormDto;
            _addFormDto = null;
            await InvokeAsync(StateHasChanged);
            if (_addFormRef is not null)
                await _addFormRef.UnlockScrollAsync();
            await OnEditPlaylistItem(args.libItem, args.insertIndex, EditPlayableItemFormMode.Add);
        }

        /// <summary>
        /// Called when Edit is clicked on the Playlist page
        /// </summary>
        /// <param name="item"></param> The PlayableItem to edit
        /// <param name="itemIndex"></param> The index of the PlayableItem to edit
        async Task OnEditPlaylistItem(PlayableItem item, int itemIndex, EditPlayableItemFormMode mode)
        {
            if (_playlist is null){
                Logger.LogError($"{_logTag}: OnEditPlaylistItem called but _playlist is null");
                return;
            }
            _originalItem = item;   // Store the original item for comparison later
            _editingItem = item.Clone();
            if (mode == EditPlayableItemFormMode.Add)
            {
                // In Add mode, the item being passed in is from the library, so we need to modify some properties.
                // We need to do this BEFORE opening the Edit form so that the form shows the correct values.
                // DO NOT modify ParentFolder at this point - the Service will need this to get the source file from the library
                // Set PlayMode to LOOP and PlayModeValue to 1 by default...
                // ... because library items have FOREVER mode by default
                _editingItem.PlayMode = PlayMode.LOOP;
                _editingItem.PlayModeValue = 1;
            }
            _editingIndex = itemIndex;
            _formMode = mode;
            _showEditDialog = true;
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Called when Save is clicked in the EditPlayableItemForm
        /// </summary>
        /// <param name="args"></param> Tuple of (editingIndex, updatedItem, formMode)
        async Task OnSavePlaylistItem((int editingIndex, PlayableItem updatedItem, EditPlayableItemFormMode formMode) args)
        {
            if (_playlist is null || _editingItem is null || _originalItem is null || _editingIndex < 0) return; // ToDo: Error handling
            await PlaylistService.OnEditFormSubmit(_playlist, args.editingIndex, _originalItem, args.updatedItem, args.formMode);

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
            _editingItem = null;
            _showReConvertAllDialog = false;
            _originalItem = null;
            // Wait for the UI to update and the dialog to be removed
            await InvokeAsync(StateHasChanged);
            if (_editFormRef is not null)
                await _editFormRef.UnlockScrollAsync();
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
            await PlaylistService.OnEditFormSubmit(_playlist, itemIndex, originalItem, item, EditPlayableItemFormMode.Edit);
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

        private void AddPlaylist()
        {
            newPlaylistName = string.Empty;
            addPlaylistError = null;
            showAddPlaylistModal = true;
        }

        private void CancelAddPlaylist()
        {
            showAddPlaylistModal = false;
            newPlaylistName = string.Empty;
            addPlaylistError = null;
        }

        private void ConfirmAddPlaylist()
        {
            // Replace spaces and invalid folder characters with '-'
            if (string.IsNullOrWhiteSpace(newPlaylistName))
            {
                addPlaylistError = "Name cannot be empty.";
                return;
            }
            string sanitized = string.Concat(newPlaylistName.Select(c =>
                char.IsLetterOrDigit(c) ? c : '-'));
            sanitized = sanitized.Replace(' ', '-');
            sanitized = sanitized.Trim('-');
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                addPlaylistError = "Name must contain at least one valid character.";
                return;
            }
            PlaylistService.AddPlaylist(sanitized);
            availablePlaylists = PlaylistService.GetPlaylistNames();
            // Automatically select the new playlist in the Editing DDL by firing the change event only
            OnEditingPlaylistChanged(new ChangeEventArgs { Value = sanitized });
            showAddPlaylistModal = false;
            newPlaylistName = string.Empty;
            addPlaylistError = null;
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
        /// Called when the Reconvert All (Global) is clicked
        /// </summary>
        private void ShowReConvertAllGlobal()
        {
            _reconvertAllMode = EditPlayableItemFormMode.ReConvertAllMatrix;
            _showReConvertAllDialog = true;
        }

        /// <summary>
        /// Called when the Reconvert All (Embedded) is clicked
        /// </summary>
        private void ShowReConvertAllEmbedded()
        {
            _reconvertAllMode = EditPlayableItemFormMode.ReConvertAllBrightness;
            _showReConvertAllDialog = true;
        }

        /// <summary>
        /// Called when the Reconvert All form is submitted
        /// </summary>
        /// <param name="args"></param> The arguments containing the relative brightness, options and form mode
        private async Task OnReconvertAll((EditPlayableItemFormMode formMode, int relativeBrightness, LedMatrixOptionsConfig? options) args)
        {
            _showReConvertAllDialog = false;
            await PlaylistService.ReConvertAllItems(args.formMode, args.relativeBrightness, args.options);
            if (_editFormRef is not null)
                await _editFormRef.UnlockScrollAsync();
            await InvokeAsync(StateHasChanged);
        }


        /// <summary>
        /// Builds the image path for the editing item based on whether we are adding or editing
        /// Images on the Edit form in ADD mode come from the library, in EDIT mode come from the folder of the playlist
        /// </summary>
        /// <returns>The path to the URL for the editing image</returns>
        private string BuildEditingImageURL(){
            if (_editingItem is null){
                Logger.LogError($"{_logTag}: BuildEditingImageURL called but editingItem is null");
                return string.Empty;
            }
            if (_formMode == EditPlayableItemFormMode.Edit){
                if (_playlist is null){
                    Logger.LogError($"{_logTag}: BuildEditingImageURL called in EDIT mode but _playlist is null");
                    return string.Empty;
                }
                return $"/playlist-images/{_playlist.Name}/{_editingItem.SourceFileName}";
            }
            return $"/library-image/{_editingItem.SourceFileName}";        
        }
    }
}