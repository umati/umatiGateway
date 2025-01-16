# umati Gateway

Gateway connecting OPC UA servers implementing umati endorsed Companion Specifications with the umati Dashboard

## Description

umati Gateway connects to an OPC UA server, subscribes to values from one or more machine instances and publishes them via MQTT in JSON format used by [umati Dashboard](https://umati.app).

## Getting Started

### Configuration files

- Download gateway configuration file ([umatiGatewayConfig.xml](umatiGateway/Configuration/umatiGatewayConfig.xml))
  - Optionally replace `autostart="False"` with `autostart="True"` for automatic deployments. In this case also make sure the connection settings and list of published nodes are specified (see below).
- Download connection configuration file ([LocalConfigumatiApp.xml](umatiGateway/Configuration/Files/LocalConfigumatiApp.xml))
  - Optionally set your OPC UA server settings in `OPCConnection` section (see also [GUI - OPC UA configuration](#opc-ua-configuration))
  - Optionally change MQTT settings in `MqttConnection` section:
    - `serverendpoint="wss://umati.app/ws" user="USERNAME" password="PASSWORD" clientId="USERNAME" prefix="umati/v2"` (see also [GUI - MQTT configuration](#mqtt-configuration))
  - Optionally add machine nodes to be published in `PublishedNodes` section (see also [GUI - OPC UA Subscriptions](#opc-ua-subscriptions))

### Running in container

Start the container directly with the configuration files mounted:

`docker run -it -v ./LocalConfigumatiApp.xml:/app/Configuration/Files/LocalConfigumatiApp.xml -v ./umatiGatewayConfig.xml:/app/Configuration/umatiGatewayConfig.xml ghcr.io/umati/umatigateway`

or via Compose with this config:

```yaml
services:
  umatigateway:
    image: ghcr.io/umati/umatigateway
    container_name: umatigateway
    volumes:
      - ./LocalConfigumatiApp.xml:/app/Configuration/Files/LocalConfigumatiApp.xml
      - ./umatiGatewayConfig.xml:/app/Configuration/umatiGatewayConfig.xml
```

### Running standalone executable

--- TBD ---

### Building

--- TBD ---

### GUI

Web-based interface is accessible on port 8080 by default. There the connection settings can be modified and applied on the fly, making it useful for initial setup and debugging.

> When running the gateway on a remote machine you can create a tunnel to it via SSH:
>
> `ssh -L 8080:localhost:8080 user@remote-server`

The interface will be then available at [http://localhost:8080](http://localhost:8080).

#### OPC UA configuration

OPC UA connection is configured in [OPC Connection](http://localhost:8080/OPCConnection) page. When not using the autostart option, make sure the connection URL is correct and press _Connect_ button. _ConnectionStatus_ field should shortly change to _True_.

#### OPC UA Subscriptions

To select machine data to be published, go to [OPC Subscriptions](http://localhost:8080/OPCSubscriptions) page. Click _Root_ item in the _Address Space_ tree, navigate to your machine node (e.g. Root → Objects → Machines → MyMachine) and click _Publish_ button in the right panel.

#### MQTT configuration

Go to [MqttConfiguration](http://localhost:8080/umatiMqtt) page and make sure the credentials are correct. You should see a list of the nodes you subscribed to in _Published Nodes_ panel on the right. When not using the autostart option, click _Connect_ to start publishing.

## Components

--- TBD ---

## License

--- TBD ---
