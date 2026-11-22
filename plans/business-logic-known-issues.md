# Business logic known issues

These are known issues in the current Playback, QuickMedia, and mode-switching implementation. They are recorded for follow-up and are not fixed by this document.

## Playback and mode transitions

### Mock buttons use incorrect slots and bypass mode switching

**Status:** Open  
**Impact:** High when using the mock buttons - playback handlers can compete for the matrix.

The mock page uses 0-based list indexes as QuickMedia button numbers, which are 1-based and may not be contiguous. It also starts QuickMedia directly without coordinating with `ModeHandler`, so Playlist playback may remain active.

Relevant code: [Mocks.razor](../src/Components/Pages/Mocks/Mocks.razor) and [Mocks.razor.cs](../src/Components/Pages/Mocks/Mocks.razor.cs).

## QuickMedia UI

### Editing a QuickMedia item can report success when saving fails

**Status:** Open  
**Impact:** Medium - the UI can report that an edit was saved when it was not.

The edit path ignores the return value of `QuickMediaManager.UpsertButtonItem` and reports success even when the manager returns `false`.

Relevant code: [QuickMedia.razor.cs](../src/Components/Pages/QuickMedia/QuickMedia.razor.cs) and [QuickMediaManager.cs](../src/Architecture/QuickMedia/QuickMediaManager.cs).
