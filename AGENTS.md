# Context for AI agents

## Project information
Wearware is an application that drives a wearable LED Matrix tshirt.  
The tshirt displays images and GIF animations running in a playlist.  
It utilizes the rpi-rgb-led-matrix GitHub library as a submodule.  
The software runs on a Raspberry Pi (4B) with an LED matrix driver board HAT (Electrodragon Active-3).  
The application features a web interface which can be accessed via WiFi - this is used to configure the playlist, upload files etc.  
The software is open source, any mentioned hardware / environmental specs are of the author's system. Give primary concern to this configuration, but also concern to other configurations (eg desktop OS may vary, Pi hardware and OS may vary. It will always be LED matrix and always be the aforementioned submodule driving the matrix though).  
Software development occurs on a desktop computer (Windows PC) in VSCode.  
The Pi has buttons attached (Currently 4) via GPIO pins which can be pressed to trigger an animation and then return to the normal playlist ("QuickMedia" functionality).  
The Pi is powered by 4x 6000mAh 21700 batteries and lasts for about 10-11 hours.  

## Real-world considerations  
* Production ("prod") use is at nightclub events which typically last 8-10 hours, so there is not a huge amount of headroom in terms of battery life, dev use is at home and powered by a PSU.  
* Power (ie battery) consumption in prod is obviously an issue.  
* Keyboard / normal (ie HDMI) display access to the Pi is not available in prod. Access to the Pi directly (ie not via the UI) is via SSH in dev and possible but undesirable in prod.  
* In dev, networking is via LAN. In prod, networking is via WiFi (Typically the pi is connected to an Android phone's hotspot).  
* Anything which could cause the Playlist / QuickMedia functionality to stop working whilst in prod is considered a disaster, and should be avoided. The main playback loop needs to be robust. Losing access to the UI would be considered an annoyance, but not as disastrous.  
* Whilst in prod, the user may be intoxicated and is on a night out having fun, so any action which may need to be taken at that point (eg connecting a phone to the UI, acessing the UI, configuring what the tshirt is showing etc) should be simple - eg minimize typing, avoid the need for SSH etc in prod if at all possible.  

## Software considerations  
* Modification of the underlying rpi-rgb-led-matrix submodule is a possibility, but should never happen without warning the author.  
  If changes are made, he would generally be seeking to raise a PR once it is working.  
* Try and stick to existing conventions, libraries etc where at all possible.  
* Try to avoid complexity. If the author asks if something is feasible - temper your response in terms of these considerations and challenge whether the complexity or change to existing implementation is worth it.  
* Never, EVER use the "Em dash", "En dash" etc. Always use regular hyphens (-) instead.  
* If you remove some code, do not leave comments saying that you removed it. Same applies for changing code.  
  Explanations of why the code is doing what it is doing are fine. If code does something a little unusual, or to work around an issue, it is also acceptable to put in the comments why it is, or has to be, that way.  
* Avoid code duplication wherever reasonably possible
* If something has changed, and existing documentation / ToDo lists reference it, then warn the author. Do not update it yourself however until directed, but if you see the author finishing one task and moving onto another, flag it as being out of date.  
* Keep your responses fairly terse. Unless discussing what code would look like, avoid adding code blocks to your responses.  
  Summarize, lots of TLDRs, and let the author ask if he wants clarification.  
