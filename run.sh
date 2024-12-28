masscan 0.0.0.0/0 \
    --excludefile=exclude.conf \
    -p25565 --open --rate 16384 2>/dev/null | stdbuf -oL cut -d' ' -f6 | python -u scanner.py

