# ToDo list

## High Priority
- Error handling in Add / Edit (Copying files + converting stream)  

### Code overhaul
- Components
  - More re-use of components.
    - Library and AddPlayableItemForm do not use PlayableItemCard (But would need PlayMode hidden)
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
- Allow configuration of ButtonPins via GUI
- PlaylistService tidy
  - A bunch of stuff in here should be moved to PlaylistItems
- Clicking on Buttons can be bouncy? - seen ?Import? ?Add Item to Playlist? show duplicates

### Extra button functionality
- Add button config  
- QuickActions
  - Start / Stop playlist  
  - Shutdown
- QuickActions and QuickMedia would need to share same GPIO handler  
  So that they do not try to subscribe to same button


### General refactor of MediaController Start / Stop etc  
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