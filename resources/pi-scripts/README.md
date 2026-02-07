# Utility scripts  
These provide useful tools for managing WareWare
They intentionally have no extension and have short names to make it quick and easy to type on a phone in the field.  

## Pre-install
1. Edit `vars-include` to point to where you installed WareWare  
1. Edit `wearware.service` top point to where you installed WearWare

## Installation
1. Copy `wearware.service` to `/etc/systemd/system/wearware.service`
1. Copy all the other files to `/usr/local/bin`  
1. Make each script executable  
    eg `sudo chmod +x ww`  

See `wwhelp` for a list of the scripts and what they do  

For the backup script (`bw`) to work, ensure that `backup-WareWare` exists as a peer to the WareWare folder  
eg, my setup:
```
/root/dev
 ├─ WareWare
 │   ├── bin
 │   ├── playlists
 │   └── etc...
 └─ backup-WareWare
```

## Installation of the service  
1. Do the install process as above
2. Execute `sudo systemctl daemon-reload`
4. Type `ww` to start WearWare as a service. It will auto-start on system boot.  
    Typing `ww` again will toggle this off.  
    You can explicitly set it to be enabled or disabled with `ww start` or `ww stop` respectively