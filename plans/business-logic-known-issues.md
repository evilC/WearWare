# Business logic known issues

These are known issues in the current Playback, QuickMedia, and mode-switching implementation. They are recorded for follow-up and are not fixed by this document.

## QuickMedia UI

### Editing a QuickMedia item can report success when saving fails

**Status:** Open  
**Impact:** Medium - the UI can report that an edit was saved when it was not.

The edit path ignores the return value of `QuickMediaManager.UpsertButtonItem` and reports success even when the manager returns `false`.

Relevant code: [QuickMedia.razor.cs](../src/Components/Pages/QuickMedia/QuickMedia.razor.cs) and [QuickMediaManager.cs](../src/Services/QuickMedia/QuickMediaManager.cs).
