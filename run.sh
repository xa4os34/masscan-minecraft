#!/usr/bin/env sh

dotnet scanner.dll &

sleep 1

(masscan -c config/masscan.conf --excludefile config/exclude.conf \
    | stdbuf -oL cut -d' ' -f6 \
    | nc -U /var/run/scanner.sock) &

wait
