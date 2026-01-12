# ToDo list

- Changes needing PRs to go through  
  - Something odd going on with 1st start. Set MatrixOptions, load playlist, but screen is garbled  
    Restart and it is OK  
    This is because some Matrix options (eg RowAddressType) cannot be changed  
    PR `matrix-reset-01` submitted to rectify this
  - If cannot play items in playlist (Wrong matrix config), it goes forever in a loop and chugs whole system  
    This is because GetNext() in streamplayer returns false if it is an invalid stream.  
    It cannot detect the difference between end of stream and invalid stream  
    PR `c#-content-streamer-iscompatible` submitted to rectify this
- Allow configuration of ButtonPins via GUI
- PlaylistService
  - A bunch of stuff in here should be moved to PlaylistItems
- Disaster recovery
  - Some way to restore last known good?
- Clicking on Buttons can be bouncy? - seen ?Import? ?Add Item to Playlist? show duplicates
- Mock Import (Just copy, don't convert)
- UI pages not responsive immediately after loading  
  Need some way to disable the page until it has fully loaded  
  Tried adding initialization service, but it got quite messy. Also broke repo when tried to revert out  
  Investigate "lazy loading" next?
- Preview for library items  
  Would need some way to cancel. Cannot just new up a PlayableItem on each click, as instance would not be the same.  
  Also would maybe need to handle the scenario where no playlist is playing?
- Change `ParentFolder` in `PlayableItem` to relative path (eg `playlist/default`)  
  Will enable
  - Duplication of playlists (Will need to rewrite with new location)  
  - Moving folder
- Add `GetPath()` to `PlayableItem`?  
  Less combines elsewhere in code  
  Maybe add `GetStreamPath()` and `GetSourceFilePath()`?  
  Maybe add equivalent to `LibraryItem`?