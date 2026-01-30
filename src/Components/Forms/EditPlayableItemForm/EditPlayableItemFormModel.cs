using WearWare.Common.Media;

namespace WearWare.Components.Forms.EditPlayableItemForm
{
    public class EditPlayableItemFormModel
    {
        public EditPlayableItemFormMode FormMode { get; set; }
        public EditPlayableItemFormPage FormPage { get; set; } = default;
        public int InsertIindex { get; set; } = default;
        public PlayableItem SelectedLibraryItem { get; set; } = default!;
    }
}