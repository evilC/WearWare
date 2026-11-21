# Business logic and runtime model

This document records the current working model for WearWare's responsibilities and playback behavior. It is a synthesis of current thinking, not a finalized implementation plan. Names such as `Manager` and `Handler` are working names.

## Responsibilities

### Web UI

Razor pages own layout, presentation, and UI-specific state. Pages call Managers to read available options or request changes. Managers do not depend on Razor.

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

Interruptable by QuickMedia.

#### Components

- `PlaylistManager` manages playlist definitions, edits, configuration, and the current playlist position.
- `PlaylistHandler` runs playlist playback and handles playlist-specific logical input.

#### Buttons

* Next / Previous - manually moves to the next playlist or previous item

* Pin - temporarily locks playback to the currently playing item.  
  Press again to unpin



### QuickMedia (Mode)

Contextual media which is triggered through the press of a physical button. Interrupts interruptable modes, plays the media, then returns to the interrupted mode on the same item that it was.

QuickMedia items may be of "Forever" type - they are set to play forever, until the button that triggered them is pressed again.

#### Components

- `QuickMediaManager` manages QuickMedia assignments and its active-item state.
- `QuickMediaHandler` runs the requested QuickMedia item and signals when playback has stopped and it has released the matrix.

#### Buttons

* Activate QuickMedia slot (One per solt) - Interrupts current mode and activates a given QuickMedia slot



### Physics (Selectable Mode)

A real-time physics simulation that reacts to data from an IMU (Accelerometer)

It is intended that various simulations be available.

(Should be) Interruptable by QuickMedia.

#### Components

- `PhysicsManager` manages general Physics configuration applicable to all simulations (eg which type of IMU the user has installed), as well as the current simulation.  
- `PhysicsHandler` runs the selected simulation and manages it's runtime lifecycle.  
  Initializes the IMU and passes an instance of the already warmed-up IMU class to any Physics runner that runs.
- Each simulation is it's own Runner, and is responsible for storing it's own configuration, if any, independently of the manager. (Eg number of balls in a ball simulation)

#### Buttons

None currently planned



### Button Management

#### Components

- `ButtonInputManager` Maps physical buttons to logical functions, eg
  
  - Button 1 is assigned to Mode and signals the ModeHandler to trigger it's "Next" action.
  
  - Button 5 is QuickMedia button 1 (But not what animation it triggers - when that physical button changes state, the ButtonInputHandler would notify the QuickMediaHandler that the logical button changed state, and it would then play whatever it has in slot 1)
  
  - To facilitate the above, on startup, the ButtonInputManager will query the ModeManager for a list of modes, then query the manager for each mode for a list of actions that it has available to map. eg QuickMediaManager would respond "Play QuickMedia , accepts an `int`". ModeManager would respond "Next and Previous, no parameter"
  
  - When the user assigns a button to, eg QuickMedia slot 1 - QuickMedia is then signalled to tell it that it has 1 slot available. The same should happen when the button is unassigned.  
    This way, the QuickMediaManager knows which slots to make available for the user to assign, and allows it to delete media files for slots that are no longer available etc.

- `ButtonInputHandler` owns GPIO input, translates physical presses to logical input, and routes them to the relevant handler.

### Mode

The mode system owns persisted mode state and coordinates runtime transitions, including which mode Handler has control of the display.

#### Components

- `ModeManager` owns persisted mode state, including the current mode and the mode to return to after QuickMedia.
- `ModeHandler` owns runtime mode transitions and arbitrates which mode Handler has control of the display.

#### Buttons

* Next / Prevoius - changes currently active mode
  Note that only modes marked with `Selectable Mode` can be selected with these buttons (ie Playlist and Physics) - QuickMedia is not selectable with these buttons as it is triggered with one of the QuickMedia slot buttons  
  Note that if QuickMedia is currently active in Forever mode, these buttons would change the return mode, not the currently active mode



### Matrix

There must be one owner for the physical matrix instance and its lifecycle. Mode Handlers use the matrix through a shared matrix-facing component. `ModeHandler` coordinates which Handler may use the display, and waits for the current Handler to stop before handing control to another. The component name and exact API are undecided. Mode Handlers should not start or stop one another directly.

These are working boundaries. They do not imply that every Manager and Handler must be separate if a responsibility does not justify another component.



### Preview

(To be implemented)

Not interruptable by QuickMedia.

To be used at home, when adding new animations. Facilitates seeing how they look etc.

Enable via the GUI, pick a raw source file (GIF, PNG etc) in a specific folder (`preview`?) and it is converted to fseq and played indefinitely. The file on disk is watched, and if it the file changes, it is re-converted and played.

Items in the preview folder are shown on the preview page, and each one has a "keep" button (Moves to the Import folder, imports to the Library) and a "Delete" button.



### Settings

(To be implemented)

Not interruptable by QuickMedia.

Allows changing of various options via a menu displayed on the Matrix.



## Persisted state

- `PlaylistManager` persists the selected playlist and current item. Save when the selection or current item changes, not on every frame.
- `QuickMediaManager` persists which QuickMedia item is active.
- `ModeManager` persists the current mode and, while QuickMedia is active, the mode to return to. `ReturnMode` or `InterruptedMode` is clearer than `LastMode`.

QuickMedia is represented as the current mode while active. Its active item belongs to QuickMedia state; the interrupted (return mode) belong to mode state.

## QuickMedia handoff example

This illustrates the intended responsibilities, not a finalized API:

1. `ButtonInputHandler` routes a QuickMedia button press to `QuickMediaHandler`.
2. `QuickMediaHandler` requests display control from `ModeHandler`.
3. `ModeHandler` asks the active mode Handler to stop or suspend, and waits for it to release the matrix.
4. `ModeHandler` gives `QuickMediaHandler` exclusive display control, and QuickMedia starts the requested item.
5. If QuickMedia starts successfully, `ModeHandler` tells `ModeManager` to persist `CurrentMode = QuickMedia` and the interrupted mode as the return mode. `QuickMediaManager` persists the active item.
6. If QuickMedia fails to start, `ModeHandler` restores the previous Handler and the failed transition is not persisted as the current mode.
7. When the QuickMedia playback ends (Animation finished), `QuickMediaHandler` requests release. `ModeHandler` stops QuickMedia, restores the return mode, and persists the resulting mode state.

The same handoff applies when Physics, rather than Playlist, was active before QuickMedia.



## Unresolved edge-cases

* Physics simulations in playlists
  It would be desirable, as part of the normal playlist, to allow a specific physics simulation to be inserted in the same way that a media item can be inserted (In this case, only `Duration` type would make any sense).  
  How would this be implemented? Currently, playlist is of type `PlayableItem`, which a physics simulation is not.

## 

## Open questions

- What should happen if persistence fails after a Handler successfully takes control?
- A sudden power loss can occur after a successful handoff but before the new state is persisted. Is a transition-intent or recovery mechanism needed?
- What should startup do if persisted mode state refers to a missing QuickMedia item, playlist, or simulation?
- What exact matrix-facing component and API should own the physical matrix resource?
