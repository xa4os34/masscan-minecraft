FROM --platform=$BUILDPLATFORM alpine:3.21 AS build
RUN mkdir -p /app
WORKDIR /app

ARG TARGETPLATFORM
ARG TARGETARCH

RUN apk add dotnet8-sdk

COPY scanner scanner-src

RUN dotnet restore -v diag -a $TARGETARCH scanner-src/scanner.csproj --no-cache
RUN dotnet publish -v diag -a $TARGETARCH --no-cache -c Release --sc false scanner-src/scanner.csproj -o .  

RUN apk del dotnet8-sdk
RUN rm -rf scanner-src

COPY run.sh .
COPY config config
COPY config config

FROM alpine:3.21
COPY --from=build /app /app
VOLUME /app/config
WORKDIR /app

RUN apk add masscan libpcap-dev coreutils dotnet8-runtime netcat-openbsd

RUN chmod +x run.sh

ENTRYPOINT ./run.sh
