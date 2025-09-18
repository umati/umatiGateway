# User Manual

## Running the software

### Running the umatiGateway as container with default configuration

Start the container directly with the default configuration:

```sh
docker run -it ghcr.io/umati/umatigateway:develop
```

You can use the default configuration to reach the configuration section via the Web GUI and adapt and download your configuration.

### Running the umatiGateway as container with mounted configuration files

Command line:

```sh
docker run -it -v ./umatiGatewayConfig.xml:/app/umatiGatewayConfig.xml ghcr.io/umati/umatigateway:develop
```

or via `docker/podman compose` with this `compose.yaml`:

```yaml
services:
  umatigateway:
    image: ghcr.io/umati/umatigateway:develop
    container_name: umatigateway
    ports:
      - "127.0.0.1:7079:7079"
    volumes:
      - ./umatiGatewayConfig.xml:/app/umatiGatewayConfig.xml
```

#### Running a local MQTT broker for testing

To start a local MQTT broker WITHOUT AUTHENTICATION use either

```yaml
docker run -it -p 1883:1883 eclipse-mosquitto:2.0.15 mosquitto -c /mosquitto-no-auth.conf
```

or extend the `compose.yaml` with this block

```yaml
broker:
  image: eclipse-mosquitto:2.0
  container_name: mosquitto
  ports:
    - "1883:1883"
  # make a comment if using volume mounts
  command: "/usr/sbin/mosquitto -c /mosquitto-no-auth.conf"
  # uncomment to use volume mounts and persist configuration and storage
  #volumes:
  #  - ./mosquitto.conf:/mosquitto/config/mosquitto.conf
  #  - ./mosquitto:/mosquitto/data
  #  - ./mosquitto/log:/mosquitto/log
```

## Configuration

### Configuration via config files

The umatiGateway app has one configuration file called `umatiGatewayConfig.xml` that is located in the root directory of the umatiGateway application.
The default configuration file looks like:

```xml
<?xml version="1.0" encoding="utf-8"?>
<umatiGatewayConfig version="2.0" logLevel="Debug">
  <StartConfiguration startWebUI="True" startOPCConnection="False" startMqttProvider="False" startPubSubProvider="False"/>
  <WebUI url="http://127.0.0.1:7079"></WebUI>
  <!-- <OPCConnection serverendpoint="opc.tcp://opcua.umati.app:4840" authentication="None" user ="" password="" ReadExtraLibs="False"/> -->
  <OPCConnection serverendpoint="opc.tcp://localhost:4840" authentication="None" user ="" password="" ReadExtraLibs="False"/>
  <MqttProvider serverendpoint="wss://umati.app/ws" user="" password="" clientId="company/client" prefix="umati/v2" includeStructuredComponents="False" publishInterval="5000">
    <PublishedNodes>
       <!-- <PublishedNode type="Numeric" namespaceurl="http://example.com/FullMachineTool/" nodeId="66382" baseType="" /> -->
       <PublishedNode type="Numeric" namespaceurl="http://example.com/BasicMachineTool/" nodeId="66382" baseType="" />
       <!-- <PublishedNode type="Numeric" namespaceurl="http://example.com/StringIdExample/" nodeId="StringId" baseType="" /> -->
    </PublishedNodes>
    <CustomEncodings>
      <CustomEncoding name="GMSResultDataTypeEncoding" active="False" />
      <CustomEncoding name="ProcessingCategoryDataTypeEncoding" active="False" />
    </CustomEncodings>
    <IgnoredPlaceholderTags>
      <IgnoredPlaceholderTag name="&lt;ParameterIdentifier&gt;"/>
    </IgnoredPlaceholderTags>
    </MqttProvider>
  <PubSubProvider serverendpoint="wss://umati.app/ws" user="" password="" clientId="company/client" prefix="umati/v3">
    <PublishedNodes>
      <!-- <PublishedNode type="Numeric" namespaceurl="http://example.com/FullMachineTool/" nodeId="66382" baseType="" /> -->
      <PublishedNode type="Numeric" namespaceurl="http://example.com/BasicMachineTool/" nodeId="66382" baseType="" />
      <!-- <PublishedNode type="String" namespaceurl="http://example.com/StringIdExample/" nodeId="StringId" baseType="" /> -->
    </PublishedNodes>
  </PubSubProvider>
</umatiGatewayConfig>
```

| Tag/Attribute                          | Description                                                                       | Possible Values                                                     |
| -------------------------------------- | --------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| **umatiGatewayConfig**                 | Root Tag of the configuration file for umatiGateway.                              | -                                                                   |
| →version                               | Version of the configuration format.                                              | `2.0`                                                               |
| →logLevel                              | Determines the global Log level the application runs with.                        | `Debug`                                                             |
| →**StartConfiguration**                | Tag configuring the start up behaviour of the gateway.                            | -                                                                   |
| →→startWebUi                           | Indicates if the WebUi should be started when gateway starts.                     | True \| False                                                       |
| →→startOPCConnection                   | Indicates if the OPC Connection should be started when gateway starts.            | True \| False                                                       |
| →→startMqttProvider                    | Indicates if the MQTT Provider should be started when gateway starts.             | True \| False                                                       |
| →→startPubSubProvider                  | Indicates if the PubSub Provider should be started when gateway starts.           | True \| False                                                       |
| →**WebUI**                             | Tag configuring the Web Ui of the gateway.                                        | -                                                                   |
| →→`url`                                | Sets the URL for the Web Ui.                                                      | e.g., `http:localhost:8080` or `https:127.0.0.1:80`                 |
| →**OPCConnection**                     | Tag Configuring the connection to the OPC Server.                                 | -                                                                   |
| →→serverendpoint                       | Host address of the OPC Ua Server.                                                | e.g., `opc.tcp://localhost:4840`                                    |
| →→authentication                       | Reserved for future use.                                                          | None                                                                |
| →→user                                 | User for basic User/Password authentication.                                      | e.g., admin                                                         |
| →→password                             | Password for basic User/Password authentication.                                  | e.g., password1                                                     |
| →→readExtraLibs                        | Experimental. Reads Certain BSD files locally instead from the server.            | True \| False                                                       |
| →**MqttProvider**                      | Tag that configures the MQTT Provider that publishes in umati/v2 format.          | -                                                                   |
| →→serverendpoint                       | Host address of the MQTT Broker.                                                  | e.g., `mqtt://localhost:1883` or `wss://umati.app/ws` for umati.app |
| →→user                                 | User for basic User/Password authentication.                                      | e.g., admin                                                         |
| →→password                             | Password for basic User/Password authentication.                                  | e.g., password1                                                     |
| →→clientId                             | Client id that is used to construct the MQTT topics.                              | e.g., company/client                                                |
| →→prefix                               | Prefix that is used to construct the MQTT topics.                                 | e.g., umati/v2                                                      |
| →→includeStructuredComponents          | Indicates if StructuredCompnents should be included in resulting JSON.            | True \| False                                                       |
| →→publishInterval                      | Determines the interval in ms in which the MQTT topics are published.             | e.g., 5000                                                          |
| →→**PublishedNodes**                   | Tag that holds a list of the Nodes that are published.                            | -                                                                   |
| →→→**PublishedNode**                   | Tag that holds the configuration for one published node.                          | -                                                                   |
| →→→→type                               | Defines the type of the NodeId of the PublishedNode.                              | Numeric \| String                                                   |
| →→→→namespaceurl                       | Defines the nsu of the PublishedNode.                                             | e.g., `http://example.com/BasicMachineTool/`                        |
| →→→→nodeId                             | Defines the id of the PublishedNode.                                              | e.g., 61982 or MyMachine (Numeric or String)                        |
| →→→→baseType                           | Alias name for the Typedefintion that is used in the resulting JSON.              | e.g., MachineToolType Empty if no alias should be used.             |
| →→**CustomEncodings**                  | Tag that holds possible CustomEncodings for certain DataStructures.               | -                                                                   |
| →→→GmsResultDataTypeEncoding           | Custom Encoding for the _GmsResultDataType_.                                      | True \| False                                                       |
| →→→ProcessingCategorytDataTypeEncoding | Custom Encoding for the _GmsResultDataType_.                                      | True \| False                                                       |
| →→**IgnoredPlaceHolderTags**           | Tag that holds PlaceholderTags that should be ignored in the Resulting JSON.      | -                                                                   |
| →→→**IgnoredPlaceHolderTag**           | Tag that configures the Placeholder that should be ignored.                       | -                                                                   |
| →→→→name                               | The name of the PlaceholderTag that should be ignored. <> have to be escaped.     | e.g., &lt;ParameterIdentifier&gt;                                   |
| →**PubSubProvider**                    | Tag that configures the PubSub Provider that publishes in umati/v3 format.        | -                                                                   |
| →→serverendpoint                       | Host address of the MQTT Broker.                                                  | e.g., `mqtt://localhost:1883` or `wss://umati.app/ws` for umati.app |
| →→user                                 | User for basic User/Password authentication.                                      | e.g., `admin` or                                                    |
| →→password                             | Password for basic User/Password authentication.                                  | e.g., `password1`                                                   |
| →→clientId                             | Client id that is used to construct the MQTT topics.                              | e.g., `company/client`                                              |
| →→prefix                               | Prefix that is used to construct the MQTT topics.                                 | e.g., `umati/v2`                                                    |
| →→allowUntrustedCertificates           | Allows to disable the certificate check during connection initiation.             | e.g., `false`                                                       |
| →→**PublishedNodes**                   | Tag that holds a list of the Nodes that are published.                            | -                                                                   |
| →→→**PublishedNode**                   | Tag that holds the configuration for one published node.                          | -                                                                   |
| →→→→type                               | Defines the type of the NodeId of the PublishedNode.                              | Numeric \| String                                                   |
| →→→→namespaceurl                       | Defines the nsu of the PublishedNode.                                             | e.g., `http://example.com/BasicMachineTool/`                        |
| →→→→nodeId                             | Defines the id of the PublishedNode.                                              | e.g., `61982` or `MyMachine` (`Numeric` or `String`)                |
| →→→→baseType                           | Alias name for the entry node _TypeDefintion_ that is used in the resulting JSON. | e.g., `MachineToolType` or empty if no alias should be used.        |

### Configuration via Web UI

The Web UI is accessible after starting the umatiGateway via `https://localhost:8080` . The address can be configured via the `url` attribute of the `WebUI` tag in the `umatiGatewayConfig.xml` file.

The Web UI consists of 5 different tabs:

1. The **OPC Connection Tab** which configures the OPC Connection.
2. The **OPC Subscription Tab** where you can define the nodes you want to subscribe to.
3. The **MQTT Configuration Tab** which configures the machines via the v2 format.
4. The **PubSub Tab** which configures the publishing of machines via the v3 format.
5. The **Configuration Tab** that holds the curretn configuration an allows to download the configuration files.

#### OPC Connection Tab

In the OPC Connection Tab you can configure the OPC Connection parameters and connect or disconnet to/from an OPC Server.

![OPC Connection](/docs/user/images/OPC_Connection_Tab.png)

#### OPC Subscription Tab

In the OPC Subscription Tab you can browse the Nodes in the OPC Server when you are connected and add the selected node to the nodes that should be published via MQTT (typically your machine). All child Nodes of the node will be published as well.

![OPC Subscriptions](/docs/user/images/OPCSubscriptions.png)

#### MQTT Connection Tab

In the MQTT Connection Tab you can configure the MQTT Connection. If you push the connect button the MQTT connection will be established and the Nodes in the Publsihed Nodes table will be published.

![MQTT Connection](/docs/user/images/MqttConnection.png)

#### Configuration Tab

In the Configuration Tab you can see all currently set configuration settings. You are able to change this settings there and download the resulting .xml configuration files as a .zip archive.

![Configuration](/docs/user/images/Configuration.png)

## FAQ

### How to change the port for the Web UI?

#### Changing port by configuring the application

The port for the Web UI can be changed by editing the `application.json` file in the root directory of the application in the follwoing way:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:8080"
      }
    }
  }
}
```

#### Changing port by configuring dotnet environment variables

You can change the port by using the ASPNETCore Environment Variable

```shell
# Windows(temporary):
set ASPNETCORE_URLS=http://localhost:8080

# Windows(permanent):
setx ASPNETCORE_URLS http://localhost:8080

# Linux:
export ASPNETCORE_URLS=http://localhost:8080

# Docker (All Interfaces):
docker run -e ASPNETCORE_URLS=http://0.0.0.0:8080 -p 8080:8080
```

### How to change the Web UI to use https?

You can configure the Web UI to use a https connection by editing the `application.json` file in the root directory of the application in the follwoing way:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:443",
        "Certificate": {
          "Path": "/etc/ssl/certs/mycert.pfx",
          "Password": "deinPasswort"
        }
      }
    }
  }
}
```

### How to handle with TLS Inspection / MQTT Connection Problems?

Problem:

In some corporate networks, TLS inspection replaces the broker's certificate with one signed by a company CA. This causes TLS errors.

Common error:

`The remote certificate is invalid according to the validation procedure.`

#### Solution 1: Install Company Root CA (Recommended)

Get the root certificate from your IT department.
Install it into the Trusted Root Certification Authorities store on your system.

This allows all applications to trust inspected TLS connections.

#### Solution 2: Use Custom CA in App Directory

Place the root certificate file (e.g., `custom_ca.crt`) next to the `.exe`.

The app will use it to validate TLS connections.
