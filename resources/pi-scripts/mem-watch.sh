#!/usr/bin/env bash
set -euo pipefail

# Sample WearWare memory behavior over time with minimal overhead.
# This script is collector-only: it writes CSV rows in real time.
# Usage:
#   ./mem-watch.sh [duration_seconds] [interval_seconds] [output_csv_path]
# Example:
#   ./mem-watch.sh 3600 15 /root/dev/WearWare/logs/mem-watch-$(date +%Y%m%d-%H%M%S).csv
# Forever mode example:
#   ./mem-watch.sh 0 15 /root/dev/WearWare/logs/mem-watch-$(date +%Y%m%d-%H%M%S).csv

DURATION_SECONDS="${1:-3600}"
INTERVAL_SECONDS="${2:-15}"
OUTPUT_PATH="${3:-/root/dev/WearWare/logs/mem-watch-$(date +%Y%m%d-%H%M%S).csv}"

if ! [[ "$DURATION_SECONDS" =~ ^[0-9]+$ ]] || ! [[ "$INTERVAL_SECONDS" =~ ^[0-9]+$ ]]; then
  echo "duration_seconds and interval_seconds must be integers" >&2
  exit 1
fi

if [ "$INTERVAL_SECONDS" -le 0 ]; then
  echo "interval_seconds must be > 0" >&2
  exit 1
fi

FOREVER_MODE=0
if [ "$DURATION_SECONDS" -eq 0 ]; then
  FOREVER_MODE=1
fi

PID="$(pgrep -n WearWare || true)"
if [ -z "$PID" ]; then
  echo "WearWare process not found" >&2
  exit 1
fi

START_EPOCH="$(date +%s)"

mkdir -p "$(dirname "$OUTPUT_PATH")"

echo "timestamp,pid,vmrss_kb,vmswap_kb,vmdata_kb,threads,fd_count,rss_kb,anon_kb,private_dirty_kb,swap_kb" > "$OUTPUT_PATH"

if [ "$FOREVER_MODE" -eq 1 ]; then
  echo "Sampling PID $PID forever every ${INTERVAL_SECONDS}s"
else
  echo "Sampling PID $PID for ${DURATION_SECONDS}s every ${INTERVAL_SECONDS}s"
fi
echo "Writing to $OUTPUT_PATH"

while true; do
  if [ "$FOREVER_MODE" -eq 0 ]; then
    NOW_EPOCH="$(date +%s)"
    ELAPSED="$((NOW_EPOCH - START_EPOCH))"
    if [ "$ELAPSED" -gt "$DURATION_SECONDS" ]; then
      break
    fi
  fi

  TS="$(date -Iseconds)"
  STATUS_PATH="/proc/$PID/status"
  SMAPS_PATH="/proc/$PID/smaps_rollup"

  if [ ! -r "$STATUS_PATH" ] || [ ! -r "$SMAPS_PATH" ]; then
    echo "Process $PID exited during sampling" >&2
    break
  fi

  VMRSS_KB="$(awk '/VmRSS:/ {print $2}' "$STATUS_PATH")"
  VMSWAP_KB="$(awk '/VmSwap:/ {print $2}' "$STATUS_PATH")"
  VMDATA_KB="$(awk '/VmData:/ {print $2}' "$STATUS_PATH")"
  THREADS="$(awk '/Threads:/ {print $2}' "$STATUS_PATH")"
  FD_COUNT="$(ls "/proc/$PID/fd" | wc -l)"

  RSS_KB="$(awk '/^Rss:/ {print $2}' "$SMAPS_PATH")"
  ANON_KB="$(awk '/^Anonymous:/ {print $2}' "$SMAPS_PATH")"
  PRIVATE_DIRTY_KB="$(awk '/^Private_Dirty:/ {print $2}' "$SMAPS_PATH")"
  SWAP_KB="$(awk '/^Swap:/ {print $2}' "$SMAPS_PATH")"

  echo "$TS,$PID,$VMRSS_KB,$VMSWAP_KB,$VMDATA_KB,$THREADS,$FD_COUNT,$RSS_KB,$ANON_KB,$PRIVATE_DIRTY_KB,$SWAP_KB" >> "$OUTPUT_PATH"

  sleep "$INTERVAL_SECONDS"
done

echo "Done."
echo "CSV: $OUTPUT_PATH"
echo "Run mem-watch-analyze.sh on this CSV to generate summary and warning log."
