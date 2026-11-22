# ToDo list

## Post-hotspot changes
- I no longer use hostapd (Pi now connects to phone's hotspot using NetworkManager)  
  `resources\pi-scripts\wearware.service` still references it - should be removed?

## Business Logic (New Architecture) changes
- On change of Matrix options: suspend playback and ignore QuickMedia triggers while reconfiguring, using a reconfiguration-in-progress flag. Then resume using the latest mode and Playlist state, preserving any Forever interruption.
- PlaylistItems refactor? Feels messy. Will also need back as well as forward once we implement back/forward with joystick
- Re-Add Library preview
- Add Physics page
- Re-implement mocks system
- Undo PlayableItem / QuickMediaItem separation?  
  I think all it really does currently is remove "Enabled" from QuickMediaItem?
- Relocate files / name things better 
  - Too many things called "config", when some are config, some are state  
  - Get rid of Architecture folder - move stuff back into Services
- MatrixService - move to new location?
- Move things from HostedService to Singleton?
- ButtonHandler should subscribe to GPIO buttons, not each Mode's handler?  
  ButtonHandler should handle subscribing to the physical GPIO button, and then route events to the handler.  
  eg ButtonHandler subscribes to GPIO pin 10, which is assigned to QuickMedia...  
  ... When pressed, should send "QuickMedia button pressed" event to QuickMedia Handler.
- ModeHandler should not store previous state, should instead store CurrentState and InterruptingState  
  That way if QM interrupts Playback with Forever item, and while playing we change mode to Physics...  
  ... When we end the forever item, it goes to Physics

## I2C support
- Battery voltage monitoring / logging

## IMU
- Config file for sensor model and address

## Physics sims
- Physics sims will need pitch of display to calculate real-world fall rate
- Config options for each sim?
  - Fluid:
    - See ToDo about Margin bodge
    - Particle spacing: Smaller gives finer detail and potentially less rounding, but uses more particles and CPU
    - Particle count: More particles at the same pool depth can give finer detail and less rounding, but increase CPU usage
    - Pool depth. Deeper uses more particles and leaves less room to slosh
    - Solver iterations: More can reduce wall buildup, but costs CPU

## Fseq conversion
- `frame-sequence-player.cc` width / height calculation is wrong - does not take into account pixel mapping.
  RGBLedMatrix defines width and height, I think this should be used instead.  
  https://github.com/hzeller/rpi-rgb-led-matrix/issues/1918
- FSeq converter does not need to pass most parameters  
  Only rows / cols / chain / parallel ?
- LedMatrixOptionsConfig no longer needs Relative Brightness in ToArgsString / ToArgsList ?

## High Priority
- Lots of edge cases with start/stop/quickmedia forever items etc. Whole system needs refactoring
- Too many Service singletons?  
  Some things maybe do not need to be - eg physics simulations
- Add Preview page
  Button to upload. File is instantly converted and played.  
  Uploaded files are stored in separate folder, and do not overwrite incoming / library.  
  Can upload numerous files for preview and quickly toggle between them.  
  Each preview file has move button to move original to incoming, and converted file to library.  
  Delete all button to remove all preview files when done.  
- Error handling in Add / Edit (Copying files + converting stream)  
- Make line-endings consistent throughout the repo
  - Massive mix of CRLF and LF, even among files of same file extension
  - Whole repo should default to LF  
    Except maybe .ahk files, which may need to be CRLF
  - Fix editor with `.editorconfig` in root of repo - is editor-agnostic  
    ```
    [*]
    end_of_line = lf

    [*.ahk]
    end_of_line = crlf
    ```
  - Fix Git with `.gitattributes`  
    ```
    # default behavior for all files
    * text eol=lf

    # override for AutoHotkey scripts
    *.ahk text eol=crlf
    ```
  - Custom handling may also be needed for shell scripts in `/resources/pi-scripts`  
    These have no extenstion, so would need to be done via path?  
    These should be LF, so we may not need custom handling if the default is LF
  - This will cause a lot of files to be re-committed


### Code overhaul
- Components
  - Parameters of components can be made non-nullable by setting to `default!`;
  - Components can be wrapped in an `<ErrorBoundary>`  
    If something goes wrong, alternate UI can be shown and error does not bubble up to parent
- Enable Streaming Rendering  
  `@attribute [StreamRendering(true)]` to start of all pages.  
  - Generally, the stub seems to be there (eg `if (importFiles == null)` on Import page)  
    However, even in that example, it's after another check. Should always be first?

## Med Priority
- Implement Max Brightness option  
  Limits maximum brightness that could ever be set.  
  eg BEC will not handle 100% brightness for full white images  
  Would require dynamic form validation?
- Implement nicer confirm dialog for delete
- Public everywhere! Change to internal where possible
- Reorder playlist
- If FOREVER mode item is playing, clicking `Save` in Matrix options does not close the page.  
  It does seem to save though, and manually navigating to another page does work.  

### Crash hardening.  
- Various places where null-forgiving operators are used

### Rework stream conversion success / fail handling + reporting?  
TaskResult seems clunky. 
Int error results yucky  
Return enum?  
Custom exceptions?  
Either way, need to be able to tell if original file got overwritten or not


## Low Priority
- If we trigger QuickMedia while the playlist is stopped, after it finishes playing, it should not start
- Allow configuration of button pins via GUI
- Clicking on Buttons can be bouncy? - seen ?Import? ?Add Item to Playlist? show duplicates

### General refactor of MediaController Start / Stop etc  
Note: This refers to the old MediaController, but some of these items may still be relevant
- New interface:
  - PlaylistRunning: true if playlist is set to play
  - QuickMediaPlaying: true if QuickMedia currently playing
  - MediaPlaying: true if Playlist or QuickMedia is playing
  - Start / Stop to private
  - SetPlaylistState starts or stops playlist
  - PlayQuickMedia plays QuickMedia
- Things that need to speak to MediaController:
  - Options page
    - Stop all playback, update options, then resume playback
  - QuickMediaService
    - Stop playback of button if Button is deleted while being playing
    - Stop playback of button if edited while playing, then restart
  - PlaylistService
    - Jump to playlist item
    - Add / Remove playlist item
      - If item is before currently playing item, needs to stop and restart, else current index could be off
    - Update item
      - If item set to disabled while playing, will need to move to next
    - Change of playlist
    - Delete playlist
  - Shutdown servive - stop all

### MatrixOptions hot reload
- Something odd going on with 1st start. Set MatrixOptions, load playlist, but screen is garbled  
  Restart and it is OK  
  This is because some Matrix options (eg RowAddressType) cannot be changed  
  PR `matrix-reset-01` submitted to rectify this