# umati Gateway

Gateway connecting OPC UA servers implementing umati endorsed Companion Specifications with the umati Dashboard

## Description

umati Gateway connects to an OPC UA server, subscribes to values from one or more machine instances and publishes them via MQTT in JSON format used by [umati Dashboard](https://umati.app).

## Getting Started

For a more detailed description about the umatiGateway please take a look at the [User Manual](/docs/user/usage.md) .

### Running in container

For starting the container with its default configuration:

`docker run -it ghcr.io/umati/umatigateway:develop`

Start the container directly with the configuration files mounted:

`docker run -it -v ./umatiGatewayConfig.xml:/app/umatiGatewayConfig.xml ghcr.io/umati/umatigateway:develop`

or via Compose with this config:

```yaml
services:
  umatigateway:
    image: ghcr.io/umati/umatigateway:develop
    container_name: umatigateway
    ports:
      - "127.0.0.1:7079:7079"
    volumes:
      - ./umatiGateway.xml:/app/umatiGatewayConfig.xml
```

### GUI

Web-based interface is accessible on port 8080 by default. There the connection settings can be modified and applied on the fly, making it useful for initial setup and debugging.

> When running the gateway on a remote machine you can create a tunnel to it via SSH:
>
> `ssh -L 7079:localhost:7079 user@remote-server`

The interface will be then available at [http://localhost:7079](http://localhost:7079).

### Developer Documentation

The developer documentation can be found her [Developer Documentation](/docs/developer/developerdoc.md) .

## License

This Software is licensed under Apache v2 license.
