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

## Developer Scripts
These are intended for development, diagnostics, and soak-test analysis rather than day-to-day field operation.

### Memory Watch Scripts
`mem-watch.sh` is collector-only and writes CSV in real time.

Examples:
```
# 1 hour run, sample every 15s
./mem-watch.sh 3600 15 /root/dev/WearWare/logs/mem-watch-$(date +%Y%m%d-%H%M%S).csv

# run forever until stopped (Ctrl+C)
./mem-watch.sh 0 15 /root/dev/WearWare/logs/mem-watch-$(date +%Y%m%d-%H%M%S).csv
```

`mem-watch-analyze.sh` analyzes a completed CSV and generates the warnings log:
```
./mem-watch-analyze.sh /root/dev/WearWare/logs/mem-watch-20260806-150000.csv
```

This split allows check-ins during collection without stopping the run.

### mem-watch CSV Columns
| Column | Units | Meaning |
|---|---|---|
| `timestamp` | ISO-8601 | Sample timestamp from the Pi |
| `pid` | n/a | WearWare process ID being sampled |
| `vmrss_kb` | kB | Resident set size from `/proc/<pid>/status` (`VmRSS`) |
| `vmswap_kb` | kB | Swap usage from `/proc/<pid>/status` (`VmSwap`) |
| `vmdata_kb` | kB | Data segment size from `/proc/<pid>/status` (`VmData`) |
| `threads` | count | Thread count from `/proc/<pid>/status` |
| `fd_count` | count | Open file descriptor count from `/proc/<pid>/fd` |
| `rss_kb` | kB | Aggregate RSS from `/proc/<pid>/smaps_rollup` (`Rss`) |
| `anon_kb` | kB | Anonymous memory from `/proc/<pid>/smaps_rollup` (`Anonymous`) |
| `private_dirty_kb` | kB | Private dirty pages from `/proc/<pid>/smaps_rollup` (`Private_Dirty`) |
| `swap_kb` | kB | Smaps swap from `/proc/<pid>/smaps_rollup` (`Swap`) |