/// <summary>
/// Model for the EditPlayableItemForm, which is used to add or edit PlayableItems
/// Holds state of the form, along with the original item being edited...
/// ... and the updated item being created or modified
/// </summary>
namespace WearWare.Components.Forms.EditPlayableItemForm
{
    public class EditPlayableItemFormModel
    {
        // The mode of the EditPlayableItemForm (eg Add or Edit)
        public EditPlayableItemFormMode FormMode { get; set; } = default;
        // The page that launched the EditPlayableItemForm (eg Playlist / QuickMedia)
        public EditPlayableItemFormPage FormPage { get; set; } = default;
        // Used for Playlists and Quickmedia.
        // When adding, it's the index to insert the new item at.
        // When editing, it's the index of the item being edited.
        public int ItemIndex { get; set; } = default;
        // The URL of the playable item.
        // Source will differ depending on FormPage
        public string ImageUrl { get; set; } = default!;
        // The original item being edited
        // Stored so we can tell if there have been changes, eg to Matrix Options
        public PlayableItem OriginalItem { get; set; } = default!;
        // The new or updated item
        public PlayableItem UpdatedItem { get; set; } = default!;
    }
}