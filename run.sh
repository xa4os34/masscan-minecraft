masscan $SCAN_NET \
    --excludefile=exclude.conf \
    -p25565 --open --rate RATE_LIMIT 2>/dev/null | stdbuf -oL cut -d' ' -f6 | python -u scanner.py

