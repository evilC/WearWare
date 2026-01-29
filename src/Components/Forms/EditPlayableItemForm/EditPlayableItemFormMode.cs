namespace WearWare.Components.Forms.EditPlayableItemForm
{
    /// <summary>
    /// Specifies the mode the EditPlayableItemForm is in.
    /// Controls which fields are shown and how they behave.
    /// Also governs the behavior of the MatrixOptionsForm which can be opened from the EditPlayableItemForm
    /// Also used to build the title of the form.
    /// </summary>
    public enum EditPlayableItemFormMode
    {
        Add,
        Edit,
        // In this mode, the MatrixOptionsForm will be available
        ReConvertAllMatrix,
        ReConvertAllBrightness
    }
}