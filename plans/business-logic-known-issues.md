# Business logic known issues

These are known issues in the current Playback, QuickMedia, and mode-switching implementation. They are recorded for follow-up and are not fixed by this document.

## Playback and mode transitions

### QuickMedia does not clear its active slot after playback

**Status:** Open  
**Impact:** Medium - the active item can remain selected and may be started again on a later QuickMedia transition.

After playback finishes and the interrupted mode is restored, the active slot remains selected and persisted. Starting QuickMedia mode later can play that item again. The earlier attempt to clear it using `SetActiveButton(0)` has been removed.

Relevant code: [ModeHandler.cs](../src/Architecture/Mode/ModeHandler.cs) and [QuickMediaManager.cs](../src/Architecture/QuickMedia/QuickMediaManager.cs).

## QuickMedia UI

### Delete confirmation can show the wrong item name

**Status:** Open  
**Impact:** Low - the confirmation text may identify a different button from the one being deleted.

The UI passes a 1-based button number to the confirmation handler, which uses it as a 0-based list index. This can display another item's name or fall back to a generic button label.

Relevant code: [QuickMedia.razor](../src/Components/Pages/QuickMedia/QuickMedia.razor) and [QuickMedia.razor.cs](../src/Components/Pages/QuickMedia/QuickMedia.razor.cs).
