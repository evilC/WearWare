# ToDo list

- Harmonization of Import / Add / Edit, and reconvert refactor  
  Same form used for all  
  Add (for both Playlist and QuickMedia) will need extra step to pick media to add first
  - Store copy of used MatrixOptions in PlayableItem
  - Stream conversion should always be handled by the service  
    QuickMedia and Library do this in the form, Playlist does it in the service
  - Remove button to explicitly reconvert  
    Whether or not we reconvert should be decided by the service on save of the edit form
    - Import: always converts (Obviously, because it's not converted yet)
    - Playlist / QuickMedia:
    - Add / Edit automatically reconvert if matrix options or brightness changed  
      Add will need to copy from library before reconvert
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
