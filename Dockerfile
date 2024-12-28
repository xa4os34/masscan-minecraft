FROM python:3.9-alpine
RUN mkdir -p /app
WORKDIR /app

RUN apk add masscan libpcap-dev coreutils
RUN python3 -m pip install mcstatus pymongo

COPY scanner.py .
COPY run.sh .
RUN chmod +x run.sh
COPY exclude.conf .

ENV RATE_LIMIT=$RATE_LIMIT 
ENV SCAN_NET=$SCAN_NET 
ENV MONGODB_USER=$MONGODB_USER
ENV MONGODB_PASSWORD=$MONGODB_PASSWORD 
ENV MONGODB_IP=$MONGODB_IP 

ENTRYPOINT ./run.sh
