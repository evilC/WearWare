# ToDo list

## High Priority
- Split up `Components\Shared`  
  - Form components into own folder  
  - Overlays into own folder
- Reduce using spam
  - global using for regular C# files  
  - _Imports.razor for razor file
- Error handling in Add / Edit (Copying files + converting stream)  
- Can I move back to stock page layout?  
  - Do I really need to do `ww-modal` in the way that I am?  
  - Maybe move to .NET 10 at same time?
- Move back to .NET 8? 
  Project template was Net8, nut I moved to NET9 to use ConcurrentDictionary. No longer used.  
  Differences between 8 and 9, so maybe not a good idea to be using 8  
  Either that or move to 10  
  Pi maybe not set up for 10 - when installing service, had to install a separate copy of 8  
  Tried moving back to 8, Pi crashed on app start.  

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

# Notes
## Blazor
- Project is currently .net9, but was created with .net8 template  
  Does not actually need to be .net9 any more.
  - There are differences between 8 and 9  
      [Detailed at 8:14 here](https://app.pluralsight.com/ilx/video-courses/9500d8a5-f365-44e2-be7f-5657c0622651/2066ef53-e33b-4ea1-95f4-f9559c1265a5/a9ad7c83-732d-4b7e-b8c6-d23d2f4e853f)  
      - net9 adds `app.MapStaticAssets()` just before `app.MapRazorComponents<App>()`
      - net9 adds `@Assets` pointing to CSS stuff (inc bootstrap) and an `ImportMap` component.
  - NET9 has unminified JS / CSS for bootstrap that may make it easier to do CSS overhaul
  - NET9 has same EOL date as NET8 (End of 2026)
  - NET10 has EOL of 2 years' time
