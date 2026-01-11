#!/bin/bash
LOGFILE="log.txt"

while true; do
  if [ -f "$LOGFILE" ]; then
    tail -n 50 -F "$LOGFILE"
  else
    sleep 1
  fi
done