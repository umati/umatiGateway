# umati Gateway

Gateway connecting OPC UA servers implementing umati endorsed Companion Specifications with the umati Dashboard

## Description

umati Gateway connects to an OPC UA server, subscribes to values from one or more machine instances and publishes them via MQTT in JSON format used by [umati Dashboard](https://umati.app).

## Getting Started

For a more detailed description about the umatiGateway please take a look at the [User Manual](/docs/user/usage.md).

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
      - "127.0.0.1:8080:8080"
      - "[::1]:8080:8080"
    volumes:
      - ./umatiGateway.xml:/app/umatiGatewayConfig.xml
```

### GUI

Web-based interface is accessible on port 8080 by default. There the connection settings can be modified and applied on the fly, making it useful for initial setup and debugging. To use Web UI when running the gateway as a container, replace `127.0.0.1` with `0.0.0.0` (or `[::]` for IPv6) in the `WebUI` section of `umatiGatewayConfig.xml`.

> When running the gateway on a remote machine you can create a tunnel to it via SSH:
>
> `ssh -L 8080:localhost:8080 user@remote-server`
>
> The interface will be then available at [http://localhost:8080](http://localhost:8080).

## License

This Software is licensed under Apache v2 license.
