#!/usr/bin/env bash
set -euo pipefail

# Analyze a mem-watch CSV and generate warning log + summary stats.
# Usage:
#   ./mem-watch-analyze.sh <input_csv_path> [systemd_unit]
# Example:
#   ./mem-watch-analyze.sh /root/dev/WearWare/logs/mem-watch-20260806-150000.csv

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  echo "Usage: $0 <input_csv_path> [systemd_unit]" >&2
  exit 1
fi

INPUT_CSV="$1"
SYSTEMD_UNIT="${2:-wearware.service}"

if [ ! -f "$INPUT_CSV" ]; then
  echo "Input CSV not found: $INPUT_CSV" >&2
  exit 1
fi

if [ ! -s "$INPUT_CSV" ]; then
  echo "Input CSV is empty: $INPUT_CSV" >&2
  exit 1
fi

LINE_COUNT="$(wc -l < "$INPUT_CSV")"
if [ "$LINE_COUNT" -lt 3 ]; then
  echo "Input CSV must include header + at least 2 samples" >&2
  exit 1
fi

FIRST_DATA_LINE="$(sed -n '2p' "$INPUT_CSV")"
LAST_DATA_LINE="$(tail -n 1 "$INPUT_CSV")"

START_ISO="$(echo "$FIRST_DATA_LINE" | awk -F, '{print $1}')"
END_ISO="$(echo "$LAST_DATA_LINE" | awk -F, '{print $1}')"

if [ -z "$START_ISO" ] || [ -z "$END_ISO" ]; then
  echo "Failed to parse start/end timestamps from CSV" >&2
  exit 1
fi

START_EPOCH="$(date -d "$START_ISO" +%s)"
END_EPOCH="$(date -d "$END_ISO" +%s)"
ELAPSED_SECONDS="$((END_EPOCH - START_EPOCH))"
if [ "$ELAPSED_SECONDS" -lt 1 ]; then
  ELAPSED_SECONDS=1
fi
ELAPSED_MINUTES="$(awk -v s="$ELAPSED_SECONDS" 'BEGIN { printf "%.4f", s/60.0 }')"

START_VMRSS="$(echo "$FIRST_DATA_LINE" | awk -F, '{print $3}')"
END_VMRSS="$(echo "$LAST_DATA_LINE" | awk -F, '{print $3}')"
START_VMSWAP="$(echo "$FIRST_DATA_LINE" | awk -F, '{print $4}')"
END_VMSWAP="$(echo "$LAST_DATA_LINE" | awk -F, '{print $4}')"

if ! [[ "$START_VMRSS" =~ ^[0-9]+$ && "$END_VMRSS" =~ ^[0-9]+$ && "$START_VMSWAP" =~ ^[0-9]+$ && "$END_VMSWAP" =~ ^[0-9]+$ ]]; then
  echo "Failed to parse numeric memory fields from CSV" >&2
  exit 1
fi

DELTA_VMRSS="$((END_VMRSS - START_VMRSS))"
DELTA_VMSWAP="$((END_VMSWAP - START_VMSWAP))"

RSS_KB_PER_MIN="$(awk -v d="$DELTA_VMRSS" -v m="$ELAPSED_MINUTES" 'BEGIN { if (m>0) printf "%.4f", d/m; else print "0.0000" }')"
SWAP_KB_PER_MIN="$(awk -v d="$DELTA_VMSWAP" -v m="$ELAPSED_MINUTES" 'BEGIN { if (m>0) printf "%.4f", d/m; else print "0.0000" }')"

WARNINGS_FILE="${INPUT_CSV%.csv}-createframecanvas-warnings.log"
journalctl -u "$SYSTEMD_UNIT" --since "$START_ISO" --until "$END_ISO" --no-pager | grep -F "CreateFrameCanvas() called" > "$WARNINGS_FILE" || true
WARNING_COUNT="$(wc -l < "$WARNINGS_FILE")"
FIRST_WARNING="$(head -n 1 "$WARNINGS_FILE" || true)"

cat <<EOF
Analysis complete.
CSV: $INPUT_CSV
Samples (excluding header): $((LINE_COUNT - 1))
Start: $START_ISO
End: $END_ISO
Elapsed seconds: $ELAPSED_SECONDS
Elapsed minutes: $ELAPSED_MINUTES
VmRSS start/end/delta (kB): $START_VMRSS / $END_VMRSS / $DELTA_VMRSS
VmSwap start/end/delta (kB): $START_VMSWAP / $END_VMSWAP / $DELTA_VMSWAP
VmRSS slope (kB/min): $RSS_KB_PER_MIN
VmSwap slope (kB/min): $SWAP_KB_PER_MIN
Warnings file: $WARNINGS_FILE
CreateFrameCanvas warning count: $WARNING_COUNT
First warning line: ${FIRST_WARNING:-<none>}
EOF
