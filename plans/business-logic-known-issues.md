# Business logic known issues

These are known issues in the current Playback, QuickMedia, and mode-switching implementation. They are recorded for follow-up and are not fixed by this document.

## Playback and mode transitions

### QuickMedia UI

#### Delete confirmation can show the wrong item name

**Status:** Open  
**Impact:** Low - the confirmation text may identify a different button from the one being deleted.

The UI passes a 1-based button number to the confirmation handler, which uses it as a 0-based list index. This can display another item's name or fall back to a generic button label.

Relevant code: [QuickMedia.razor](../src/Components/Pages/QuickMedia/QuickMedia.razor) and [QuickMedia.razor.cs](../src/Components/Pages/QuickMedia/QuickMedia.razor.cs).
