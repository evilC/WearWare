# Business logic and runtime model

This document records the current working model for WearWare's responsibilities and playback behavior. It is a synthesis of current thinking, not a finalized implementation plan. Names such as `Manager` and `Handler` are working names.

## Responsibilities

### Web UI

Razor pages own layout, presentation, and UI-specific state. Pages call Managers to read available options and use the ModeHandler for operations that affect playback. Managers should not depend on Razor; some existing form operations still do.

### Managers

A Manager owns user-configurable rules, configuration, and persisted application state. It exposes the operations and information needed by the Web UI and other application components.

### Handlers

A Handler applies configuration at runtime to do the work. Mode Handlers control their mode's behavior, respond to logical input, and produce output on the matrix.

`Manager` and `Handler` are role names, not DI requirements. Any such component can be registered with DI, with lifetime chosen according to it's state and ownership.

## Components

### Playlist (Selectable Mode)

The default and most important mode - a list of items which play on the screen continuously.
Each item should be able to specify a `PlayMode` - current implementation allows `Loop`, `Duration`, `Forever`. `Forever` should probably be removed as it makes no sense. However, `PlayMode` is shared with QuickMedia, where it does make sense. It is, however, desirable to be able to "Pin" an item (*Temporarily* keep it always playing - eg someone want to take a photo of me and wants it showing a certain animation), but this is logically different from the playlist item always playing forever.  

Numerous playlists can be defined, and switched to in the Web UI.

Interruptible by QuickMedia.

Playlist Start/Stop controls whether the playlist should play when it has display control. It does not select Playlist mode or interrupt QuickMedia. The running/stopped state persists across restarts. When Playlist has control but is stopped, the display is blank.

#### Components

- `PlaylistManager` manages playlist definitions, edits, the selected playlist, the current playlist position, and the desired running/stopped state.
- `PlaylistHandler` runs playlist playback and handles playlist-specific logical input.

#### Buttons

* Next / Previous - manually moves to the next playlist or previous item

* Pin - temporarily locks playback to the currently playing item.  
  Press again to unpin



### QuickMedia (Interrupting Mode)

Contextual media triggered by a physical button. QuickMedia interrupts the current mode, plays the requested media, then returns control to the currently selected mode. If the selected mode changes during the interruption, QuickMedia continues and yields to the new selection when finished.

QuickMedia items may be of "Forever" type - they are set to play forever, until the button that triggered them is pressed again.

Temporary QuickMedia items can play over a Forever item. When the temporary item finishes, the Forever item resumes; QuickMedia yields only when it has no item left to play. A saved Forever item resumes after an app restart.

#### Components

- `QuickMediaManager` manages QuickMedia assignments and its active-item state.
- `QuickMediaHandler` runs the requested QuickMedia item and signals when playback has stopped and it has released the matrix.

#### Buttons

* Activate QuickMedia slot (One per solt) - Interrupts current mode and activates a given QuickMedia slot



### Physics (Selectable Mode)

A real-time physics simulation that reacts to data from an IMU (Accelerometer)

It is intended that various simulations be available.

Interruptible by QuickMedia.

#### Components

- `PhysicsHandler` currently runs the Fluid simulation and manages its runtime lifecycle. `ImuService` initializes and monitors the IMU separately.
- A `PhysicsManager`, simulation selection, and independently configurable simulation runners are planned.

#### Buttons

None currently planned



### Button Management

The current QuickMedia handler subscribes directly to the configured GPIO buttons. The routing described below is planned, not implemented.

#### Components

- `ButtonInputManager` Maps physical buttons to logical functions, eg
  
  - Button 1 is assigned to Mode and signals the ModeHandler to trigger it's "Next" action.
  
  - Button 5 is QuickMedia button 1 (But not what animation it triggers - when that physical button changes state, the ButtonInputHandler would notify the QuickMediaHandler that the logical button changed state, and it would then play whatever it has in slot 1)
  
  - To facilitate the above, on startup, the ButtonInputManager will query the ModeManager for a list of modes, then query the manager for each mode for a list of actions that it has available to map. eg QuickMediaManager would respond "Play QuickMedia , accepts an `int`". ModeManager would respond "Next and Previous, no parameter"
  
  - When the user assigns a button to, eg QuickMedia slot 1 - QuickMedia is then signalled to tell it that it has 1 slot available. The same should happen when the button is unassigned.  
    This way, the QuickMediaManager knows which slots to make available for the user to assign, and allows it to delete media files for slots that are no longer available etc.

- `ButtonInputHandler` owns GPIO input, translates physical presses to logical input, and routes them to the relevant handler.

### Mode

The mode system owns the selected mode and coordinates runtime transitions, including which mode Handler has control of the display.

- `CurrentMode` is the selected underlying mode, currently Playlist or Physics.
- `InterruptingMode` is QuickMedia while it holds control, or null otherwise.
- The interrupting mode controls the display when present. Otherwise, the selected mode controls it. If Physics is unavailable or fails, playback falls back to Playlist while the selected mode remains Physics for a future retry.

Playlist and Physics are interruptible. Future modes such as Preview and Settings may be non-interruptible.

#### Components

- `ModeManager` owns the current and interrupting mode state.
- `ModeHandler` owns runtime mode transitions and arbitrates which mode Handler has control of the display.

#### Buttons

* Next / Prevoius - changes currently active mode
  Note that only modes marked with `Selectable Mode` can be selected with these buttons (ie Playlist and Physics) - QuickMedia is triggered with a QuickMedia slot button.
  If QuickMedia is active, changing the selected mode changes the return destination without stopping QuickMedia. Physical mode-switch buttons are not yet implemented.



### Matrix

`MatrixDisplay` owns the physical matrix instance and its lifecycle, and locks access during matrix operations and replacement. `ModeHandler` coordinates which Handler may use the display, and waits for the current Handler to stop before starting another. Mode Handlers should not start or stop one another directly.

Structural matrix options changes still need coordinated suspension and resumption of playback; a Forever item can otherwise hold the matrix lock indefinitely.

These are working boundaries. They do not imply that every Manager and Handler must be separate if a responsibility does not justify another component.



### Preview

(To be implemented)

Not interruptible by QuickMedia.

To be used at home, when adding new animations. Facilitates seeing how they look etc.

Enable via the GUI, pick a raw source file (GIF, PNG etc) in a specific folder (`preview`?) and it is converted to fseq and played indefinitely. The file on disk is watched, and if it the file changes, it is re-converted and played.

Items in the preview folder are shown on the preview page, and each one has a "keep" button (Moves to the Import folder, imports to the Library) and a "Delete" button.



### Settings

(To be implemented)

Not interruptible by QuickMedia.

Allows changing of various options via a menu displayed on the Matrix.



## Persisted state

- `PlaylistManager` persists the selected playlist, current item, and running/stopped state. The running state is in `config/playlist-state.json`; when absent, Playlist defaults to running.
- `QuickMediaManager` persists the active Forever slot in `quickmedia/quickmedia-state.json`.
- `ModeManager` persists `CurrentMode` and `InterruptingMode` in `config/mode-state.json`.

The current mode remains the selected return destination during QuickMedia. On restart, a saved QuickMedia interruption resumes its Forever item if available. A temporary item is not restarted; if it was playing over a saved Forever item, that Forever item resumes.

## QuickMedia handoff example

This illustrates the current coordination. Button routing is still planned:

1. A GPIO button press reaches `QuickMediaHandler` directly.
2. `QuickMediaHandler` requests display control from `ModeHandler`.
3. `ModeHandler` asks the active mode Handler to stop or suspend, and waits for it to release the matrix.
4. `ModeHandler` persists `InterruptingMode = QuickMedia`, grants control, and QuickMedia starts the requested item. `QuickMediaManager` persists a Forever slot when one is selected.
5. While QuickMedia is active, selecting another mode updates `CurrentMode` without stopping QuickMedia.
6. When QuickMedia has no item left to play, it yields control. `ModeHandler` clears `InterruptingMode` and starts the currently selected mode, respecting Playlist's running/stopped state.

The same handoff applies when Physics, rather than Playlist, was active before QuickMedia. If QuickMedia cannot resume at startup, the selected mode is restored instead.



## Unresolved edge-cases

* Physics simulations in playlists
  It would be desirable, as part of the normal playlist, to allow a specific physics simulation to be inserted in the same way that a media item can be inserted (In this case, only `Duration` type would make any sense).  
  How would this be implemented? Currently, playlist is of type `PlayableItem`, which a physics simulation is not.

## 

## Open questions

- What should happen if persistence fails after a Handler successfully takes control?
- A sudden power loss can occur after a successful handoff but before the new state is persisted. Is a transition-intent or recovery mechanism needed?
- What should startup do if persisted mode state refers to a missing QuickMedia item, playlist, or simulation?
