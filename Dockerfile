# umati Gateway: OPC UA (Machines folder) -> MachineTool-DataModel over REST + MQTT, with a test web page.
#
#   docker build -t umatigateway .
#   docker run --rm -p 8080:8080 -e OPCUA_ENDPOINT=opc.tcp://host.docker.internal:4840/UA -e MQTT_URL=mqtt://host.docker.internal:1883 umatigateway

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY umatiGateway/umatiGateway.csproj umatiGateway/
RUN dotnet restore umatiGateway/umatiGateway.csproj
COPY umatiGateway/ umatiGateway/
RUN dotnet publish umatiGateway/umatiGateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# OPC UA client certificate / trust lists live in /data/pki (mount a volume to keep them)
RUN mkdir -p /data/pki && chown -R "$APP_UID" /data
ENV ASPNETCORE_URLS=http://+:8080 \
    PKI_PATH=/data/pki
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
VOLUME /data

HEALTHCHECK --interval=15s --timeout=5s --start-period=15s --retries=3 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health HTTP/1.0\r\n\r\n" >&3 && head -n1 <&3 | grep -q " 200 "' || exit 1

ENTRYPOINT ["dotnet", "umatiGateway.dll"]
