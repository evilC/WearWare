# WearWare
An application to turn a Raspberry Pi with an LED Matrix into a wearable LED screen t-shirt.  
This repo is very much a Work-in-progress and should not be considered ready for consumption by others

## Setup
Note: The toolchain (Build tasks etc) are currently Windows-specific (WSL - Windows Subsytem for Linux is a requirement). If running a different OS, you may need to adapt
### On the Pi:
#### Dependencies  
- .NET 10 runtime  
  You probably need the .NET SDK 8 on the Pi too, to build the RGB LED Matrix library  

#### Disk setup
Pick a folder to deploy to (eg `/root/dev/WearWare`)
This folder should contain the following subfolders:

- bin - WearWare binaries should be in here.  
  When developing, deploy to this folder, not to the parent folder.
  This is because the rsync will wipe everything in the deploy folder (Wipe your library and config etc)
- config - This is where configuration of the app will be stored
- incoming - Uploaded files will go here
- library - Imported (Converted) files will go here
- playlists - Playlists will be stored here
- quickmedia - QuickMedia items will be stored here
- tools - Tools (eg the `led-image-viewer` executable) will go here

#### Deploy assets
- Build the rpi-rgb-led-matrix library.  
- Place a compiled copy of `led-image-viewer` in the tools folder  
- .NET runtime must be installed on the Pi (Currently uses `net9.0`)

### Dev machine setup
- .NET 10 SDK must be installed
- Set the environment variable `WEARWARE_REMOTE_MACHINE` to point to the hostname and username used to SSH into the Pi (eg `root@DietPi`)
= Set the environment varibale `WEARWARE_REMOTE_DIR` to point to the folder name where WearWare is to be deployed to on the Pi (eg `/root/dev/WearWare`)  
= Set the environment variable `WEARWARE_REMOTE_LIB_DIR` to point to the folder where the RGB LED Matrix repo lives, eg `/root/dev/rpi-rgb-led-matrix`  
  Ensure the `bin` folder is created in this folder (eg `/root/dev/WearWare/bin`)

### Remote Dev (VSCode on local machine, debug on Pi)
- Passwordless SSH to the Pi as root must be configured  
#### Dev on Windows
Launch configurations are included in the repo  
- WSL must be installed, and Rsync must be added to WSL  
  This is to allow VSCode to transfer the built files to the Pi
- The Command Variable VSCode extension must be installed  
  This allows us to build linux style paths for use in `tasks.json`
- Select the `Debug (Remote Pi) - WSL` configuration
- Hit F5 to debug - code will be run on the Pi
#### Dev on Linux / Mac using VSCode
Launch configurations are not included in the repo  
However, the majority of tasks are absolutely compatible with linux with linux / mac  
You should only need to:
- Re-implement the `wsl-rsync-ww-to-pi` task (Very simple, just remove WSL)  
- Duplicate the `wsl-deploy-pi-debug` compound task, replace `wsl-rsync-ww-to-pi` with your new task.
- Duplicate the `Debug (Remote Pi) - WSL` configuration and point it at your new compound task 

### Local Dev (VSCode on local machine, Pi functionality mocked out)
In this mode, you can work on business logic etc without needing to run on the Pi  
It should work fine on any OS  
- Select the `Debug (Local with mocks)` configuration
- Hit F5 to run
- A debug interface will be available on http://localhost:5000/mocks  
  This will allow you to simulate GPIO button presses.
- In this mode, all LOOP type animations will only last for 1 second, as it has no way of knowing how long the animation is
- What the Pi would be playing on the screen is shown in the Debug Console of the IDE

### Sundry build tasks  
- `remote-build-native-libs` will build the RGB LED Matrix library on the Pi via SSH  
- `remote-build-bindings` will build the C# bindings on the Pi via SSH
- `wsl-deploy-pi-release` (Windows only) will deploy to the Pi in RELEASE mode  
  Be sure that the above two build tasks have been performed at least once before

### Hardware notes
This code is currently designed for a Pi (I use a Zero W 2) with an Electrodragon Active-3 matrix driver **which has channel 3 disabled** (De-solder the 3rd chip). **If you are using all 3 channels, then be sure to set the Pin list in `ButtonPins.cs` to empty, or do not use the QuickMedia functionality**  
Disabling the 3rd channel frees up GPIO pins 2, 3, 14, 16, 21 + 26  
Use pins 2, 3, 14 and 16 for buttons.  
Use pins 21 and 26 for an ATX power switch (I use the PetrockBlock Powerblock)  
** DO NOT ** try and use pins 2 and 3 for the PowerBlock! It will shut down as soon as it starts up (Because these pins default to high).  

## Using the app  
- Upload some GIFs or images to the `incoming` folder of the Pi
- Point a web browser at port 5000 (eg `http://dietpi:5000`)
- Go to the Options page, click the `Matrix Options` button and set the matrix options for your panel (You may need to restart the app after)  
- Go to the `Import` page and import files into the library. This will convert them to a stream
- Go to the `Playlist` page, create a playlist, and add items to it from the library
- Optionally, go to the `QuickMedia` page and assign items to the buttons (You may need to edit `ButtonPins.cs` to set which pins your buttons are connected to)