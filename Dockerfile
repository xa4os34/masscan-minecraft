FROM python:3.9-alpine
RUN mkdir -p /app
WORKDIR /app

RUN apk add masscan libpcap-dev coreutils
RUN python3 -m pip install mcstatus pymongo

COPY scanner.py .
COPY run.sh .
RUN chmod +x run.sh
COPY exclude.conf .

ENTRYPOINT ./run.sh
