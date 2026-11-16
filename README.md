# WearWare

An application to turn a Raspberry Pi with an LED Matrix into a wearable LED screen t-shirt.  

## Setup

Note: Some of the toolchain (Build tasks etc) are currently Windows-specific (WSL - Windows Subsytem for Linux is a requirement). If running a different OS, very minor adaptations will need to be made (Convert WSL commands to native linux)  

### On the Pi:

#### Dependencies

- .Net 10 SDK
  `wget https://packages.microsoft.com/config/debian/13/packages-microsoft-prod.deb -O packages-microsoft-prod.deb`
  `sudo dpkg -i packages-microsoft-prod.deb`
  `rm packages-microsoft-prod.deb`
  `sudo apt update && sudo apt install -y dotnet-sdk-10.0`
  You probably need the .NET SDK 8 on the Pi too, to build the RGB LED Matrix library  
  Installing .Net 10 SDK is probably best, as that covers all scenarios.  
- (For debugging) VSDBG  
  `curl -sSL https://aka.ms/getvsdbgsh | /bin/sh /dev/stdin -v latest -l ~/vsdbg`

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
- tools - Tools (eg the `frame-sequence-player` executable) will go here

#### Deploy assets

- Build the rpi-rgb-led-matrix library and utils.  
  The provided build tasks can be used to do this  
  (`remote-build-native-lib` followed by `remote-build-lib-utils`)
- Place a compiled copy of `frame-sequence-player` in the tools folder  
  This can be done with the `copy-util-remote-to-remote` task

### Dev machine setup

- .NET 10 SDK must be installed
- Set the environment variable `WEARWARE_REMOTE_MACHINE` to point to the hostname and username used to SSH into the Pi (eg `root@DietPi`)
- Set the environment varibale `WEARWARE_REMOTE_DIR` to point to the folder name where WearWare is to be deployed to on the Pi (eg `/root/dev/WearWare`)  
- Set the environment variable `WEARWARE_REMOTE_LIB_DIR` to point to the folder where the RGB LED Matrix repo lives, eg `/root/dev/rpi-rgb-led-matrix`  
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

- `remote-build-native-lib` will build the RGB LED Matrix library on the Pi via SSH  
- `remote-build-lib-utils` will build the RGB LED Matrix utils on the Pi via SSH
- `copy-util-remote-to-remote` will copy the `frame-sequence-player` utility to the `tools` folder of WareWare on the Pi
- `remote-build-bindings` will build the C# bindings on the Pi via SSH
- `wsl-deploy-pi-release` (Windows only) will deploy to the Pi in RELEASE mode  
  Be sure that the above two build tasks have been performed at least once before

### Hardware notes

#### Physical buttons
This app supports playing media on press of a button.  
Buttons can be connected via either the Pi's normal GPIO pins, or via an I2C GPIO expander.  
Currently, only the Adafruit MCP23017 is supported as an I2C expander, but more could be easily added.  
Button hardware is configured in `config/buttonsconfig.json`, which defaults to an empty `PiGpio` configuration. WearWare creates the same default if the file is absent. Configure the provider and `buttonPins` before using QuickMedia. The list order maps pins to QuickMedia slots, starting at slot 0.  

##### Via Pi GPIO
For Raspberry Pi GPIO, `buttonPins` contains BCM GPIO numbers. Use only pins that are available with the installed matrix HAT and other peripherals. With I2C enabled, set `excludeI2cPins` to `true` to reject a config that includes GPIO2 or GPIO3. The pins are not remapped. For example, with an Electrodragon Active-3 matrix driver board that has channel 3 disabled, and I2C is disabled:  

```json
{
  "provider": "PiGpio",
  "buttonPins": [2, 3, 14, 16, 21, 26],
  "excludeI2cPins": false
}
```

With an Electrodragon Active-3 matrix driver board that has channel 3 disabled, and I2C is enabled (Being used for something other than buttons):  

```json
{
  "provider": "PiGpio",
  "buttonPins": [14, 16, 21, 26],
  "excludeI2cPins": true
}
```

##### Via I2C
For an Adafruit MCP23017, the I2C bus is fixed to bus 1 and address `0x20`. Button pin numbers are MCP23017 driver pin numbers: 0-6 for A0-A6 and 8-14 for B0-B6. Pins 7 and 15 are kept as outputs. `interruptPins` has exactly two entries in IA, IB order; use `null` for an unconnected line:

```json
{
  "provider": "Mcp23017",
  "buttonPins": [0, 1],
  "interruptPins": [14, null]
}
```

Connect normally open buttons between A0/A1 and GND, and connect IA to Pi GPIO14 (physical header pin 8). Enable I2C on the Pi. The expander inputs use internal pull-ups.

Button events are ignored for 100ms after each handler is created to allow the input voltage to settle, and use 200ms debounce.

## Using the app

- Upload some GIFs or images to the `incoming` folder of the Pi
- Point a web browser at port 5000 (eg `http://dietpi:5000`)
- Go to the Options page, click the `Matrix Options` button and set the matrix options for your panel (You may need to restart the app after)  
- Go to the `Import` page and import files into the library. This will convert them to a stream
- Go to the `Playlist` page, create a playlist, and add items to it from the library
- Optionally, go to the `QuickMedia` page and assign items to the configured button slots.