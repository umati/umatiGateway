# OPC UA PubSub Interface Description for umati

This document defines the Interface of the umati Dashboard based on OPC UA PubSub.
It describes how a OPC UA server _AddressSpace_ must be mapped to OPC UA PubSub, so that the information can map to the umati Dashboard templates and displayed.

## Requirements for the Mapping of the _AddressSpace_ to PubSub

(Reference: [umati/umatiGateway#51](https://github.com/umati/umatiGateway/issues/51))

- Ability to react to dynamic changes in the _AddressSpace_ (nodes added/deleted)
  - Changes should be detected by monitoring NodeManagement events.
- Resolve references to nodes (NodeId as values)
- Hierarchy references must be mapped
- Non-hierarchy references should be mapped if necessary
  - Non-hierarchy references are mapped if relevant to the data semantics.
- Provide type information
- Methods should be considered
- Online detection (possibly as separate topics)
- Allow empty objects
- Events/alarms must be mappable
- Coordinate the completeness of the run
- Encoding should be UA JSON
- Dimensioning for approximately 350 subscribers (machines + dashboard users)
- Custom DataTypes must be included
- The _AddressSpace_ includes at least OPC UA Machinery and possibly other Companion Specifications based on it.
- The entry point of the mapping is the machine object in the Machines folder of Machinery.

## Status of the Publisher

As defined in OPC UA Part 14 the status should be send to the following topic:

`opcua/json/status/<PublisherId>` with `<PublisherId>` as the name of the Client

- The value of the Field _`IsCyclic`_ should be `true` and
- the Status should be published at **least every 90 sec** a higher frequency is possible.

## Application Description

The Application can be send optional.

## Connection

As defined in OPC UA Part 14 the connection should be send to the following topic:

`opcua/json/connection/<PublisherId>` with `<PublisherId>` as the name of the Client.

The following parameter must be set:

- `WriterGroups.DataSetWriters.QueueName`
- `WriterGroups.DataSetWriters.MetaDataQueueName`

## Mapping

### General

- For identification of nodes, the name of the _DataSet_ with _ExpandedNodeId_ shall be used.

### Mapping of Objects and Variables

- An _Object_ is mapped to a _DataSet_.
- A _Variable_ is mapped as a field to the _DataSet_. If the _Variable_ contains children (other variables), a _DataSet_ with the children is created.

#### Example

![image](../screenshots/mapping.png)

The image shows a part of an _AddressSpace_. Each _Object_ is mapped to a _DataSet_:

- Production
- ActiveProgram
- State

The properties _Name_, _NumberInList_ are fields of one _DataSet_ (ActiceProgram) and _Id_, and _Number_ are fields of an other DataSet(State). The value of _CurrentState_ is mapped as a field of _State_ and has a _DataSet_ with its properties:

- Production (DataSet)
- ActiveProgram (DataSet)
  - Name (Field)
  - NumberInList (Field)
- State (DataSet)
  - CurrentState (Field)
- CurrentState (DataSet)
  - Id (Field)
  - Number (Field)

### Explanation

A field can also contain properties but no other variables, even though the _AddressSpace_ can contain more hierarchies. Properties of objects also cannot be mapped to the properties of a field. Therefore, a uniform mapping is used for objects and variables so that access is always consistent.

## Mapping of Events

_Events_ are also mapped to a _DataSet_. Because _Events_ may have no _BrowsePath_, the _BrowsePath_ of the _SourceName_ and the _EventName_ is used. The mapping itself is analogous to the mapping of objects.

## Topic Structure of DataSet and DataSetMetaData

This mapping use the connection topic with the `WriterGroups.DataSetWriters.QueueName` need to define the topic trees for the DataSet and DataSetMetaData.
That means a client need to check the connection before it can "connect" to a machine.

In context of the umati Dashboard the following topic Strutuce must be used because of access restiction but because of the Connection Topic in other use cases (or on other brokers) other topic structure can be used.

The generic topic structure for OPC UA is:

`<Prefix>/<Encoding>/<MqttMessageType>/<PublisherId>/[<WriterGroup>[/<DataSetWriter>]]`

The mapping is based on the structure but deviates from it if necessary e.g., for the unified namespace. To the new topic structure for this mapping is the following:

`<Prefix>/<Encoding>/<MqttMessageType>/<PublisherId>/[<UNS>]/[<WriterGroup>[/<DataSetWriter>]]`

| Key                                 | Description                                                |
| ----------------------------------- | ---------------------------------------------------------- |
| `<Prefix>`                          | umati/v3                                                   |
| `<Encoding>`                        | json                                                       |
| `<MqttMessageType>`                 | as defined in OPC UA PubSub                                |
| `<PublisherId>`                     | Name of the Client.                                        |
| `<UNS>`                             | The location of the device in the ISA-95 common data model |
| `[<WriterGroup>[/<DataSetWriter>]]` | PathToTheNode                                              |

The `<UNS>` the be create as following sturucture `Enterprise:Site:Area:Line:Cell`.

The PathToTheNode is the Path from the _0:Objects_ node to the _Node_ that is connected to the _DataSet_.
Generally, only hierarchical references are used. If a node occurs in two places, the message should be sent to both topics.
The use of _Organizes_ references can lead to loops in the path. In this case, only the shortest path should be transmitted.
Each _Node_ is a topic. The Topic name is build from the `name` field of the _BrowseName_. All character except `[A-Za-z0-9]` need to encode by [URL-Encoding](https://de.wikipedia.org/wiki/URL-Encoding) using an underscore instead of a '%'.
If two nodes have the same _BrowsePath_ an iterator (".Number") can be send to avoid collisions (e.g, `Parent/Tool.1`, `Parent.3/Tool.2` `Parent3/Tool.3` )

For _Events_ the _SourceNode_ and the _Name_ are used for the PathToTheNode

### Examples

_DataSet_ Topic

```text
umati/v3/json/data/example_publisher_1/Enterprise1:Plant1:Area3:Line4:Cell2/machines/ShowcaseMachineTool/Identification
umati/v3/json/data/example_publisher_1/Enterprise1:Plant1:Area3:Line4:Cell2/machines/ShowcaseMachineTool/Production/ActiveProgram
umati/v3/json/data/example_publisher_1/machines/ShowcaseMachineTool/Production/ActiveProgram/State
```

_DataSet_ Event Topic

```text
umati/v3/json/data/example_publisher_1/machines/ShowcaseMachineTool/Production/ActiveProgram/State/TransitionEventType
umati/v3/json/data/example_publisher_1/Enterprise1:Plant1:Area3:Line4:Cell2/machines/ShowcaseMachineTool/Production/ActiveProgram/State/TransitionEventType

```

_DataSetMetaData_ Topic

```text
umati/v3/json/metadata/example_publisher_1/Enterprise1:Plant1:Area3:Line4:Cell2/machines/ShowcaseMachineTool/Identification
umati/v3/json/metadata/example_publisher_1/machines/ShowcaseMachineTool/Production/ActiveProgram
umati/v3/json/metadata/example_publisher_1/machines/ShowcaseMachineTool/Production/ActiveProgram/State
```

## How to annotate the DataSet with semantics (e.g. TypeDefinition, References)

The principle of the semantic mapping is done with an additional (virtual) field that contains the ID of the object. The metadata (particularly the references) for the object will be set as properties on this field.

### DataSetMetaData

The fields of _DataSetMetaData_ should be mapped as follows:

- _Namespaces_ = as defined in Part 14
- _StructureDataTypes_ = as defined in Part 14
- _Name_ = ExtendedNodeID
- _Description_ = as defined in Part 14
- _Fields_ = Contains the field description of the variables/properties
  - _Name_ = ExpandedBrowseName (NamespaceUri#Name)
  - Properties = only used for "virutal_id"
  - Other properties as defined in Part 14
- _DataSetClassId_ = as defined in Part 14
- \_ConfigurationVersion = as defined in Part 14

### Additional Fields

Each DataSet need an Field called "virual_id" which contains the BrowsePath.
(e.g.; `5:Production.5:ActiveProgram.5:State`)

If two nodes have the same _BrowsePath_ an iterator (".Number") can be send to avoid collisions (e.g., `Parent/Tool.1`, `Parent.3/Tool.2` `Parent3/Tool.3` )

The metadata of the object is write to the Properties.

| Key          | DataType             | ModellingRule | Description                |
| ------------ | -------------------- | ------------- | -------------------------- |
| Reference_No | ReferenceDescription | Optional      | References from the object |

Reference_No (e.g., Reference_1, Reference_23) should be counted to that each reference has a different key.
The following Reference must be set, other Reference can be send:

- HasSubtype
- GenerateEvent

HasComponent Refernce must not be send because this is mapped via the topic structure.

### DataSetMetaData Example

```json
{
  "Namespaces": [
    "http://opcfoundation.org/UA/",
    "urn:DEMO-5:UA Sample Server",
    "http://opcfoundation.org/UA/DI/",
    "http://opcfoundation.org/UA/Machinery/",
    "http://opcfoundation.org/UA/IA/",
    "http://opcfoundation.org/UA/MachineTool/",
    "urn:Demo:MachineTool:myMachine/"
  ],
  "StructureDataTypes": "...",
  "Description": "my Description",
  "Name": "nsu=urn:Demo:MachineTool:myMachine/;i=1001",
  "Fields": [
    {
      "Name": "virual_id",
      "Description": "as in Part 14",
      "FieldFlags": "as in Part 14",
      "BuiltInType": "12",
      "DataType": { "id": 12 },
      "ValueRank": -1,
      "ArrayDimensions": "as in Part 14",
      "MaxStringLength": "as in Part 14",
      "DataSetFieldId": "as in Part 14",
      "Properties": [
        {
          "key": "Reference_1",
          "value": {
            "referenceTypeId": "nsu=http://opcfoundation.org/UA/;i=45",
            "isForward": "False",
            "nodeId": "http://opcfoundation.org/UA/MachineTool/;i=1001",
            "browseName1": "5:ProductionProgramStateMachineType",
            "displayName": {
              "locale": "en",
              "text": "ProductionProgramStateMachineType"
            },
            "nodeClass1": "ObjectType",
            "typeDefinition1": ""
          }
        },
        {
          "key": "Reference_2",
          "value": {
            "referenceTypeId": "nsu=http://opcfoundation.org/UA/;i=2311",
            "isForward": "False",
            "nodeId": "nsu=http://opcfoundation.org/UA/Machinery/;i=3444",
            "browseName1": "0:TransitionEventType",
            "displayName": "TransitionEventType",
            "nodeClass1": "ObjectType",
            "typeDefinition1": ""
          }
        }
      ]
    },
    {
      "Name": "CurrentState",
      "Description": "as in Part 14",
      "FieldFlags": "as in Part 14",
      "BuiltInType": "21",
      "DataType": { "id": 21 },
      "ValueRank": -1,
      "ArrayDimensions": "as in Part 14",
      "MaxStringLength": "as in Part 14",
      "DataSetFieldId": "as in Part 14",
      "Properties": []
    }
  ]
}
```

#### DataSet Example

The _DataSet_ follows the _DataSetMetaData_ and other definitions of the specification. A _DataSet_ for the umati Dashboard needs to contain at least the Payload as RowData and a _DataSet_ message header with the following fields:

- Timestamp
- Status
- Name

```json
{
  "Timestamp": "2021-09-27T18:45:19.555Z",
  "Status": 1073741824,
  "Name": "nsu=urn:Demo:MachineTool:myMachine/;i=1001",
  "Payload": {
    "virutal_id": "5:Production.5:ActiveProgram.5:State",
    "CurrentState": {
      "local": "en",
      "text": "Basic Program"
    }
  }
}
```
