# umati Gateway

Gateway between **OPC UA servers** and the **umati 2.0 [MachineTool-DataModel](https://github.com/umati/MachineTool-DataModel)** (v0.13.1).
It connects to any OPC UA server that offers its machines in the `Machines` folder (OPC 40001-1 Machinery), finds the
machines there on its own, and offers every machine

* as a **REST resource** (`/api/machines/{id}/...`),
* as **retained MQTT topics** (`{machine id}/State/Status`, `{machine id}/Consumption/Electricity/Main/...`),
* in a small **web page** to try both out.

```
OPC UA server --(Machines folder, subscriptions)--> umatiGateway --+--> MQTT broker  (retained values, events, Last Will)
                                                                   +--> REST API     (values, manifest, events, SSE streams)
                                                                   +--> web page     (http://localhost:8080)
```

The former gateway functionality (PubSub provider, umati-dashboard JSON, Razor/SignalR configuration UI) was removed;
this version only implements the data model. The OPC UA → data-model translation is described by a mapping file
([`umatiGateway/mappings/machinetool-default.json`](umatiGateway/mappings/machinetool-default.json), see [docs/mapping.md](docs/mapping.md)).

## Quick start

### Docker (sample server + MQTT broker + gateway)

From the repository that contains this folder next to `Sample-Server-node-opcua` (the hackathon repository):

```bash
docker compose -f docker-compose.gateway.yml up --build
```

* Web page / REST API: <http://localhost:8080>
* MQTT broker: `mqtt://localhost:1883` (WebSocket `ws://localhost:9001`)
* OPC UA sample server: `opc.tcp://localhost:4840/UA`

Only the gateway: `docker build -t umatigateway .` and
`docker run --rm -p 8080:8080 -e OPCUA_ENDPOINT=opc.tcp://host.docker.internal:4840/UA -e MQTT_URL=mqtt://host.docker.internal:1883 umatigateway`.

### Local (.NET 8)

```bash
cd umatiGateway
dotnet run                      # listens on http://0.0.0.0:8080
OPCUA_ENDPOINT=opc.tcp://myserver:4840 MQTT_URL=mqtt://broker:1883 dotnet run
```

The OPC UA endpoint, the broker and the other connection settings can also be changed at runtime in the web page
(or with `PUT /api/config`). Without a reachable broker the gateway keeps running (REST and web page work) and
publishes as soon as the broker is there.

## How machines are found

1. Connect to the endpoint (anonymous or user/password; `OPCUA_USE_SECURITY=true` selects the most secure endpoint,
   server certificates are accepted automatically).
2. Browse `Objects/Machines` (configurable). Every Object below it is a machine; the folder is rescanned every
   `OPCUA_RESCAN_INTERVAL` seconds, so machines that appear later are picked up and vanished machines are removed.
3. Per machine the mapping is applied to the machine's address space: input nodes are resolved **by browse name**
   (namespace independent). Elements whose inputs do not exist on that machine are *not supported* and are left out of the
   machine's manifest. `Channel.<n>` / `Spindle.<n>` elements are expanded per instance found.
4. The machine id is the percent-encoded `ProductInstanceUri` (`MACHINE_ID_SOURCE=SerialNumber|BrowseName` alternatively).
   If two machines report the same URI the second one gets `#<browse name>` appended.

`GET /api/machines` tells for every machine whether all mandatory Core elements are available (`coreComplete`, `missingCore`).
Machines of other companion specifications (plastics, woodworking, robotics ...) are listed as well but only provide
what the default mapping can find on them (identification, connection state, ...); give them their own mapping file
(`MAPPING_FILE`) to publish more.

## REST API

All responses are JSON. `{machine}` is the machine id (percent-encoded, exactly as in the MQTT topic), the
`ProductInstanceUri` or the browse name of the machine.

| Request | Content |
|---|---|
| `GET /api/machines` | all machines: id, name, type, connection state, profiles, `coreComplete`, MQTT topic root, links |
| `GET /api/machines/{machine}` | one machine incl. all elements with REST path and MQTT topic, events, unsupported elements |
| `GET /api/machines/{machine}/manifest` | manifest of the data model (`modelVersion`, `profiles`, `elements`, `events`, `instances`, ...) |
| `GET /api/machines/{machine}/values` | `{path: {value, timestamp, correlationId?, unit?}}` for all elements |
| `GET /api/machines/{machine}/values/{path}` | one value, e.g. `State.Status`; a group prefix returns everything below it (`Consumption.CompressedAir`) |
| `GET /api/machines/{machine}/events?limit=50` | recent events (`ProgramExecutionStarted/Interrupted/Resumed/Ended/Aborted`), `/events/{name}` filters |
| `GET /api/machines/{machine}/stream` | server-sent events: everything published for this machine |
| `GET /api/stream?machine=` | server-sent events: everything the gateway publishes (what an MQTT subscriber to `#` sees) |
| `GET /api/status`, `GET /health` | state of the OPC UA connection, MQTT clients, machine count |
| `GET/PUT /api/config` | read / change the connection settings (applied immediately, reconnects) |
| `POST /api/connect`, `/disconnect`, `/rescan` | control |

```bash
curl http://localhost:8080/api/machines
curl http://localhost:8080/api/machines/SampleMachineTool_Energy/values/State.Status
curl "http://localhost:8080/api/machines/http%3A%2F%2Fumati.org%2FUA%2Finstances%2F2025-0815-4711/values/Consumption.Electricity.Main.AcActivePowerTotal"
```

## MQTT topics

`{TOPIC_PREFIX/}{machine id}/{path with . replaced by /}` - one MQTT connection per machine (own Last Will).

| Topic | Payload | Flags |
|---|---|---|
| `{id}/_manifest` | manifest | retained, QoS 1 |
| `{id}/State/Status`, `{id}/Identification/Manufacturer`, `{id}/Consumption/Electricity/Main/AcActivePowerTotal`, ... | value envelope `{value, timestamp, correlationId?, unit?}` (numbers carry `unit`) | retained, QoS 1 |
| `{id}/Connection/State` | `Connected` / `Disconnected`; Last Will = `Disconnected` | retained, QoS 1 |
| `{id}/Connection/Heartbeat` | counter every `HEARTBEAT_INTERVAL` s | QoS 0 |
| `{id}/Production/ProgramExecutionStarted` ... | event `{event, timestamp, value}` | not retained, QoS 1 |

Behaviour required by the model: on connection loss to the OPC UA server the last values stay (retained) and
`Connection.State` becomes `Disconnected`; after reconnecting the gateway republishes the manifest and **all** values first and then
reports `Connected`. `Production.ProgramExecution`, its `ExecutionId` (used as `correlationId`) and its events are derived by the
gateway from the state of the active program.

## Configuration

Environment variables (or `Gateway:*` in `appsettings.json`):

| Variable | Default | |
|---|---|---|
| `OPCUA_ENDPOINT` | `opc.tcp://localhost:4840/UA` | OPC UA server |
| `OPCUA_USER`, `OPCUA_PASSWORD` | - | user identity (anonymous if empty) |
| `OPCUA_USE_SECURITY` | `false` | `true` = most secure endpoint instead of SecurityMode None |
| `OPCUA_MACHINES_FOLDER` | `Machines` | browse path below Objects |
| `OPCUA_AUTOCONNECT` | `true` | connect on startup |
| `OPCUA_PUBLISHING_INTERVAL` / `OPCUA_SAMPLING_INTERVAL` | 500 / 250 ms | subscription |
| `OPCUA_RESCAN_INTERVAL` | 60 s | look for new machines (0 = off) |
| `MQTT_ENABLED`, `MQTT_URL` | `true`, `mqtt://localhost:1883` | `mqtt://`, `mqtts://`, `ws://`, `wss://` |
| `MQTT_USERNAME`, `MQTT_PASSWORD`, `MQTT_CLIENT_ID` | - / - / `umatigateway` | |
| `TOPIC_PREFIX` | - | prefix in front of the machine id |
| `MACHINE_ID_SOURCE` | `ProductInstanceUri` | `SerialNumber` or `BrowseName` |
| `HEARTBEAT_INTERVAL`, `HEARTBEAT_RETAIN` | 10 s, `false` | |
| `MAPPING_FILE` | built-in `machinetool-default.json` | own mapping |
| `PKI_PATH` | `pki` | OPC UA client certificates |
| `ASPNETCORE_URLS` | `http://0.0.0.0:8080` | web page / REST |

## Scope and limitations

* Implemented from the data model: Core (identification, connection, `State.Status`, `State.FeedOverride`, `OperationCounters.*`,
  `Notification.ActiveAlerts`), profiles IdentificationExtended (partly), Production (`ActiveProgram`, `ProgramExecution` + events),
  Channel (`ChannelState`, `FeedOverride`, `Name`), Spindle, Energy (electricity and compressed air at the `Main` measuring point).
* Not yet: `Production.JobOrders` and its events, alarm events, Stacklight, `PartsProducedInLifetime`. `Notification.ActiveAlerts`
  is always the empty list (no alarm source mapped yet).
* The Last Will carries the connect time as its timestamp. Where a server does not advance the SourceTimestamp together with a
  value (structures in the sample server) the gateway uses its receive time.
* The default mapping follows the layout of the umati sample server (`Identification`, `MachineryBuildingBlocks`, `Monitoring`,
  `Production`); servers using other names need a mapping file.

## Tests

```bash
dotnet test        # transforms, program execution tracker, topics, envelope, mapping validation
```

## License

Apache License 2.0, see [LICENSE](LICENSE).
