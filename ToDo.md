# ToDo list
- Summary of variations in new form for Harmonization task
  - Import
    - Clicking image is "Importing" (ie Add)
    - No PlayableItem exists - needs to be created
    - Dialog needs to be shown before edit form to pick media from Incoming folder
    - Filename field shows only on this page - needs to be validated, and could be changed
    - "PlayMode" options (PlayMode / PlayModeValue / RelativeBrightness) form not embedded.  
      Values always hard-coded to PlayMode=FOREVER, PlayModeValue=1, RelativeBrightness=100
    - Matrix options form shown with default values from MatrixOptions service
    - Always converts  
      - Copy from incoming, then convert
  - Library
    - ReConvert button instead launches new Edit form
    - Modifies existing PlayableItem
      - PlayMode form not embedded, hard-coded values from Import kept
      - Matrix options form embedded with values from PlayableItem
    - On submit:
      - Reconverts if RelativeBrightness or Matrix Options changed
  - Playlist / QuickMedia
    - Add
      - Add button shows dialog to pick PlayableItem from the library instead of separate Add form
        - Clone PlayableItem from item picked
        - After that, shows harmonized Edit form
          - PlayMode options form embedded with values from PlayableItem
          - Matrix options form embedded with values from PlayableItem
      - On submit:
        - Copy files from library
        - Reconvert the copy if RelativeBrightness or Matrix Options changed
        - Save PlayableItem
    - Edit
      - Edit button launches new harmonized Edit form
        - Modifies existing PlayableItem
          - PlayMode options form embedded with values from PlayableItem
          - Matrix options form embedded with values from PlayableItem
        - On submit:
          - Reconvert if RelativeBrightness or Matrix Options changed
          - Save PlayableItem
- Progress system
  Overlay to show progress (eg on ReConvert)  
  - Old unused overlay still left in `MatrixOptionsForm.razor` (`div class="mw-matrix-overlay"`)
  - Locked out scrollbars in underlying page - see `modalScrollLock` in `EditPlayableItemForm.razor`
  - AI recommended in chat `Nested Razor forms validation scenario`: `Service / pub-sub — OperationProgressService`
- Harmonization of Import / Add / Edit, and reconvert refactor  
  Same form used for all  
  Add (for both Playlist and QuickMedia) will need extra step to pick media to add first
  - ~~Remove LibraryItem~~  
    Do we now still need to pass all the parameters that were present in PlayableItem but not LibraryItem to eg `OnAddPlayableItem`  
    (When adding item to Playlist / QuickMedia). Can we not just pass a PlayableItem?  
  - ~~Store copy of used MatrixOptions in PlayableItem~~
  - Edit of PlayableItem should used stored MatrixOptions, not one from the service
  - Stream conversion should always be handled by the service  
    QuickMedia and Library do this in the form, Playlist does it in the service
  - Remove button to explicitly reconvert  
    Whether or not we reconvert should be decided by the service on save of the edit form
    - Import: always converts (Obviously, because it's not converted yet)
    - Playlist / QuickMedia:
    - Add / Edit automatically reconvert if matrix options or brightness changed  
      Add will need to copy from library before reconvert
  - Rework stream conversion success / fail handling + reporting?  
    TaskResult seems clunky. 
    Int error results yucky  
    Return enum?  
    Custom exceptions?  
    Either way, need to be able to tell if original file got overwritten or not
  - Remove playMode, playModeValue, relativeBrightness, currentBrightness from `AddPlayableItemForm` `EventCallback`
  - Can originalItem be stored in the `EditPlayableItemForm`?  
    That way, we do not need to implement storing it on each of the pages
  - Remove `MatrixOptionsFormOld`
  - Replace icky Task.Yield

- Changes needing PRs to go through  
  - Something odd going on with 1st start. Set MatrixOptions, load playlist, but screen is garbled  
    Restart and it is OK  
    This is because some Matrix options (eg RowAddressType) cannot be changed  
    PR `matrix-reset-01` submitted to rectify this
- Allow configuration of ButtonPins via GUI
- PlaylistService
  - A bunch of stuff in here should be moved to PlaylistItems
- ReConvert all functionality
  - Library, Playlists, QuickMedia
  - Reconvert with embedded options
  - Reconvert with default options
- No reprocess is done on Add of Playlist or QuickMedia items  
  Therefore, setting Relative Brightness here does nothing  
  Should be solved with Harmonization task
- Clicking on Buttons can be bouncy? - seen ?Import? ?Add Item to Playlist? show duplicates
- UI pages not responsive immediately after loading  
  Need some way to disable the page until it has fully loaded  
  Tried adding initialization service, but it got quite messy. Also broke repo when tried to revert out  
  Investigate "lazy loading" next?
- Reconvert all for library, playlist, quickmedia
- If we trigger QuickMedia while the playlist is stopped, after it finishes playing, it should not start
- Buttons  
  - Add button config  
  - QuickActions
    - Start / Stop playlist  
    - Shutdown
  - QuickActions and QuickMedia would need to share same GPIO handler  
    So that they do not try to subscribe to same button
- General refactor of MediaController Start / Stop etc  
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
- Crash hardening.  
  - Various places where null-forgiving operators are used
