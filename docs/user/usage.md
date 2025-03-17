# User Manual

## Installation

--- TBD ---

### Installation as docker image

--- TBD ---

### Installation as executeable

--- TBD ---

# Running the software

### Running the umatiGateway as executable

--- TBD ---

### Running the umatiGateway as docker container
Start the container directly with the configuration files mounted:

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
## FAQ

