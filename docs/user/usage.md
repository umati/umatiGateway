# User Manual

## Running the software

### Running the umatiGateway as docker container with defailt configuration

Start the container directly with the default configuration:

`docker run -it ghcr.io/umati/umatigateway`

You can use the default configuration to reach the configuration section via the Web Gui and adapt and download your configuration.

#### Running the umatiGateway as docker container with mounted Configuration Files

`docker run -it -v ./LocalConfigumatiApp.xml:/app/Configuration/Files/LocalConfigumatiApp.xml -v ./umatiGatewayConfig.xml:/app/Configuration/umatiGatewayConfig.xml ghcr.io/umati/umatigateway`

or via Compose with this config:

```yaml
services:
  umatigateway:
    image: ghcr.io/umati/umatigateway
    container_name: umatigateway
    ports:
      - "127.0.0.1:8080:8080"
    volumes:
      - ./LocalConfigumatiApp.xml:/app/Configuration/Files/LocalConfigumatiApp.xml
      - ./umatiGatewayConfig.xml:/app/Configuration/umatiGatewayConfig.xml
```
## Configuration

### Configuration via config files
The umatiGateway app has two configuration files per default.
The first file called umatiGatewayConfig.xml is located in the *Configuration/* folder in the root directory of the umatiGateway app. It stores the basic application configuration.

```xml
<?xml version="1.0" encoding="utf-8"?>
<umatiGatewayConfig version="1.0" autostart="False" file="./Configuration/Files/LocalConfigLocalSampleServer.xml"
 logLevel="Debug" ReadExtraLibs="False" singleThreadPolling="False" pollTime="10000" />
```

| Node/Attribute           | Description                                                                                          | Example Value                                             |
|--------------------------|------------------------------------------------------------------------------------------------------|-----------------------------------------------------------|
| `umatiGatewayConfig`     | Root node of the configuration file for umatiGateway.                                                |                                                           |
| `version`                | Version of the configuration format.                                                                 | `1.0`                                                     |
| `autostart`              | Whether the gateway should start automatically when launched ('True', 'False').                      | `False`                                                   |
| `file`                   | Path to the referenced local configuration file containing node and connection details.              | `./Configuration/Files/LocalConfigLocalSampleServer.xml`  |
| `logLevel`               | Log level for the application (`Trace`, `Debug`, `Info`, `Warn`, `Error`).                           | `Debug`                                                   |
| `ReadExtraLibs`          | Flag to enable loading of extra libraries.                                                           | `False`                                                   |
| `singleThreadPolling`    | Whether to use single-threaded polling for connected devices. (**currently not used**)               | `False`                                                   |
| `pollTime`               | Minimum Publishing intervall for the Mqtt Topic. The Data Topic is also published on data change.    | `10000` (10 seconds)                                      |


The second file is called LocalConfigumatiApp.xml and it is stored in the *Configuration/Files* folder in the root directory of the umatiGateway app. It stores the configuration for the connections to the OPC Server and to the Mqtt server as well as the nodes that are published.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Configuration version="1.0">
    <!--For authentication with username and password use the line below.-->
    <!--<OPCConnection serverendpoint="opc.tcp://opcua.umati.app:4843" authentication="None" user ="admin" password="pw1"/>-->
    <OPCConnection serverendpoint="opc.tcp://opcua.umati.app:4840" authentication="None" user ="" password=""/>
    <MqttConnection serverendpoint="mqtt://localhost:1883" user="" password="" clientId="fva/matthias2" prefix="umati/v2"/>
    <PublishedNodes>
        <PublishedNode type="Numeric" namespaceurl="http://example/instance/" nodeId="5003" BaseType=""/>
    </PublishedNodes>
    <CustomEncodings>
    <CustomEncoding name="GMSResultDataTypeEncoding" active="False" />
        <CustomEncoding name="ProcessingCategoryDataTypeEncoding" active="False" />
        </CustomEncodings>
</Configuration>
```

| Node/Attribute                  | Description                                                                                     | Example Value                          |
|---------------------------------|-------------------------------------------------------------------------------------------------|----------------------------------------|
| `Configuration`                 | Root element containing all connection and node publishing settings.                            |                                        |
| `version`                       | Version of the configuration format.                                                            | `1.0`                                  |
| `OPCConnection`                 | Settings for the OPC UA server connection.                                                      |                                        |
| `serverendpoint` (OPC)          | OPC UA server endpoint URL.                                                                     | `opc.tcp://opcua.umati.app:4840`       |
| `authentication`                | Authentication method (e.g., None, UsernamePassword).                                           | `None`                                 |
| `user` (OPC)                    | Username for authentication. **If empty, no authentication is used.**                           | (empty)                                |
| `password` (OPC)                | Password for authentication. **If empty, no authentication is used.**                           | (empty)                                |
| `MqttConnection`                | Settings for the MQTT server connection.                                                        |                                        |
| `serverendpoint` (MQTT)         | MQTT broker address.                                                                            | `mqtt://localhost:1883`                |
| `user` (MQTT)                   | MQTT username (optional). **If empty, no authentication is used.**                              | (empty)                                |
| `password` (MQTT)               | MQTT password (optional). **If empty, no authentication is used.**                              | (empty)                                |
| `clientId`                      | MQTT client ID that is used for the topic generation.                                           | `fva/matthias2`                        |
| `prefix`                        | Topic prefix for published MQTT messages.                                                       | `umati/v2`                             |
| `PublishedNodes`                | Collection of nodes to be published to MQTT.                                                    |                                        |
| `PublishedNode`                 | A single node entry to be published.                                                            |                                        |
| `type`                          | Node type (e.g., `Numeric` or `String`).                                                        | `Numeric`                              |
| `namespaceurl`                  | Namespace URI for the node.                                                                     | `http://example/instance/`             |
| `nodeId`                        | Node ID to identify the OPC UA node.                                                            | `5003`                                 |
| `BaseType`                      | Base type of the published node (**set if Node should use display Typedefinition**).            | (empty)                                |
| `CustomEncodings`               | List of custom data encodings used. **fixed list of custom encodings for legacy machines**      |                                        |
| `CustomEncoding`                | A single custom encoding entry.                                                                 |                                        |
| `name`                          | Name of the custom encoding.                                                                    | `GMSResultDataTypeEncoding`            |
| `active`                        | Whether this custom encoding is active (`True`/`False`). **True for legacy only**               | `False`                                |



### Configuration via Web UI

The Web UI is accessible after starting the umatiGateway via http://localhost:8080 . The address can be configured via an application.json file in the applications root directory. An example is statet in the FAQs.
The Web UI consists of 4 different tabs:
1. The *OPC Connection Tab* which deals with the OPC Connection
2. The *OPC Subscription Tab* where you can define the nodes you want to subscribe to.
3. The *Mqtt Configuration Tab* which deals with the Mqtt Connection.
4. The *Configuration Tab* that holds the curretn configuration an allows to download the configuration files.

#### OPC Connection Tab

In the OPC Connection Tab you can configure the OPC Connection parameters and connect or disconnet to/from an OPC Server.

![OPCConnection](/docs/user/images/OpcConnection.png)

#### OPC Subscription Tab

In the OPC Subscription Tab you can browse the Nodes in the OPC Server when you are connected and add the selected node to the nodes that should be published via Mqtt (typically your machine). All child Nodes of the node will be published as well.

![OPCSubscriptions](/docs/user/images/OPCSubscriptions.png)

#### Mqtt Connection Tab

In the Mqtt Connection Tab you can configure the Mqtt Connection. If you push the connect button the Mqtt connection will be established and the Nodes in the Publsihed Nodes table will be published.

![MqttConnection](/docs/user/images/MqttConnection.png)

#### Configuration Tab

In the Configuration Tab you can see all currently set configuration settings. You are able to change this settings there and download the resulting .xml configuration files as a .zip archive.

![Configuration](/docs/user/images/Configuration.png)

## FAQ

### How to change the port for the Web UI?

The port for the Web UI can be changed by editing the `application.json` file in the root directory of the application in the follwoing way:

``` json
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

### How to change the Web UI to use https?

You can configure the Web UI to use a https connection by editing the `application.json` file in the root directory of the application in the follwoing way:

``` json
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
