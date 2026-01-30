using WearWare.Common.Media;

namespace WearWare.Components.Forms.EditPlayableItemForm
{
    public class EditPlayableItemFormModel
    {
        // When importing, this will be the new name
        public string NewName { get; set; } = default!;
        // The mode of the EditPlayableItemForm
        public EditPlayableItemFormMode FormMode { get; set; } = default;
        // The page that launched the EditPlayableItemForm
        public EditPlayableItemFormPage FormPage { get; set; } = default;
        // In Add mode, the index to insert the new item at
        // Or, in Add or Edit mode for QuickMedia items, the button number
        public int ItemIndex { get; set; } = default;
        // In Add mode, the library item
        // ToDo: Do we need both SelectedLibraryItem and OriginalItem?
        // public PlayableItem SelectedLibraryItem { get; set; } = default!;
        // The original item being edited
        public string ImageUrl { get; set; } = default!;
        public PlayableItem OriginalItem { get; set; } = default!;
        // The new or updated item
        public PlayableItem UpdatedItem { get; set; } = default!;
    }
}