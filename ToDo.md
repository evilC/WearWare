# ToDo list

- Start from scratch  
  - Something odd going on with 1st start. Set MatrixOptions, load playlist, but screen is garbled  
    Restart and it is OK  
    This is because some Matrix options (eg RowAddressType) cannot be changed  
    PR `matrix-reset-01` submitted to rectify this
  - If cannot play items in playlist (Wrong matrix config), it goes forever in a loop and chugs whole system  
    This is because GetNext() in streamplayer returns false if it is an invalid stream.  
    It cannot detect the difference between end of stream and invalid stream  
    PR `c#-content-streamer-iscompatible` submitted to rectify this
- PlaylistService
  - A bunch of stuff in here should be moved to PlaylistItems
- Disaster recovery
  - Some way to restore last known good?
- Error handling
  - Load of JSON
  - If playback fails for a QuickMedia item...  
    ... we should maybe disable the QuickMedia item like we do for a Playlist item.  
    However, no enabled checkbox is present on the QuickMedia page  
    Do we need to deal with this?
- Should we not move to using the `ParentFolder` property of PlayableItem to get path?
- Lots of instances where it is possible to add duplicate items
  Where things are indexed by name etc
- Clicking on Buttons can be bouncy? - seen ?Import? ?Add Item to Playlist? show duplicates
- Mocked local dev  
  - Mock Import (Just copy, don't convert)
- C# Bindings
  - clear() on the matrix is not exposed in the bindings  
    because of this, we have to manually blank the canvas by writing all black in Stop()
- UI pages not responsive immediately after loading  
  Need some way to disable the page until it has fully loaded  
  Tried adding initialization service, but it got quite messy. Also broke repo when tried to revert out  
  Investigate "lazy loading" next?
