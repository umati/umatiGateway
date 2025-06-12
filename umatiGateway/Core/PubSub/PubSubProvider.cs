using Microsoft.AspNetCore.Hosting.Server;
using MQTTnet;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.LayoutRenderers;
using Opc.Ua;
using Opc.Ua.Client;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using umatiGateway.Core.Configuration;
using Opc.Ua.PubSub;
using UmatiGateway;
using umatiGateway.Core.OPC;
using umatiGateway.Core.Mqtt;

namespace umatiGateway.Core.PubSub
{
    public class PubSubProvider
    {
        string topic = "opcua/test";
        string metaTopic = "opcua/meta";

        private int counter = 0;
        //
        // Pre subscription list
        private List<NodeId> subscriptionIds = new List<NodeId>();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private bool keepAliveTimerStarted = false;
        private System.Timers.Timer keepAliveTimer = new System.Timers.Timer();
        private UmatiGatewayApp client;
        private Dictionary<NodeId, HierarchicalNode> rootNodes = new Dictionary<NodeId, HierarchicalNode>();
        private PubSubConfigurationDataType PubSubConfigurationDataType = new PubSubConfigurationDataType();
        private PubSubConnectionDataType PubSubConnectionDataType = new PubSubConnectionDataType();
        private UaPubSubApplication pubSubApp;
        private UaPubSubDataStore pubSubDataStore = new UaPubSubDataStore();
        private uint writerid = 1;
        private PublishedDataSetDataTypeCollection publishedDataSets = new();
        private DataSetWriterDataTypeCollection dataSetWriters = new();
        private WriterGroupDataTypeCollection writerGroups = new();
        private List<VirtualId> virtualIds = new List<VirtualId>();
        WriterGroupDataType WriterGroup = new WriterGroupDataType();
        private readonly System.Timers.Timer pubTestTimer = new System.Timers.Timer(5000);
        private ReferenceDescriptionResolver referenceDescriptionResolver;



        public PubSubProvider(UmatiGatewayApp client)
        {
            this.client = client;

            // Init Config
            PubSubConfigurationDataType = new PubSubConfigurationDataType
            {
                PublishedDataSets = publishedDataSets,
                Connections = new PubSubConnectionDataTypeCollection()
            };

            // WriterGroup direkt lokal anlegen
            var writerGroup = new WriterGroupDataType
            {
                Name = "AutoWriter",
                Enabled = true,
                PublishingInterval = 1000,
                DataSetWriters = dataSetWriters,
                MessageSettings = new ExtensionObject(new JsonWriterGroupMessageDataType
                {
                    NetworkMessageContentMask = (uint)(
                        JsonNetworkMessageContentMask.PublisherId |
                        JsonNetworkMessageContentMask.DataSetMessageHeader
                    )
                }),
            };

            // Verbindung mit MQTT-Ziel
            PubSubConnectionDataType pubSubConnection = new PubSubConnectionDataType
            {
                Name = "umati Gateway Mqtt Connection",
                Enabled = true,
                PublisherId = (ushort)1234,
                TransportProfileUri = Profiles.PubSubMqttJsonTransport,
                Address = new ExtensionObject(new NetworkAddressUrlDataType
                {
                    Url = "mqtt://localhost:1883"
                }),
                WriterGroups = new WriterGroupDataTypeCollection { writerGroup }
            };

            // Verbindung anhängen
            PubSubConfigurationDataType.Connections.Add(pubSubConnection);
        }
        public void Connect()
        {
            referenceDescriptionResolver = new ReferenceDescriptionResolver(client);
            CreateSubscriptions();
            client.SubscribeToDataChanges(subscriptionIds, updateDataValue);
            AddVirtualNodeIdsToStore();
            CreateApp();
            AddVirtualNodeIdsToStore();

        }

        private void AddVirtualNodeIdsToStore()
        {
            foreach (VirtualId virtualId in virtualIds) {
                pubSubDataStore.WritePublishedDataItem(virtualId.nodeId, Attributes.Value, virtualId.dv);
                Logger.Info($"Added Value to PubSUb DataStore: {virtualId.nodeId} \t {virtualId.dv}");
            }
        }
        public void Disconnect()
        {
            Logger.Info("Disconnecting");
            //AsyncHelper.RunSync(() => this.mqttClient.DisconnectAsync());
            Logger.Info("Disconnected");
        }
        public void Reconnect()
        {
            Connect();
        }
        private void KeepAliveEvent(object? source, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                Logger.Debug("Keep Alive");
                DataValue? dataValue = client.ReadValue(VariableIds.Server_ServerStatus_State);
                if (dataValue != null)
                {
                    ServerState serverState = (ServerState)(int)dataValue.Value;
                    Logger.Debug(serverState.ToString());
                }
            }
            catch (ServiceResultException opcException)
            {
                Logger.Info("Message:" + opcException.Message);
                if (opcException.Message == "BadNotConnected")
                {
                    Logger.Info("Reconnecting OPC");
                    _ = client.ConnectAsync(client.opcServerUrl).Result;
                }
            }
            catch (MQTTnet.Exceptions.MqttClientNotConnectedException mqttException)
            {
                Logger.Info(mqttException.ToString());
            }
            catch (Exception ex)
            {
                Logger.Info(ex.ToString());
            }
        }

        private void CreateSubscriptions()
        {
            PublishedNodeFilter publishedNodeFilter = new PublishedNodeFilter(client);
            List<PublishedNode> publishedNodes = client.ActiveConfiguration.PubSubProviderConfig.PublishedNodes;
            List<MachineNode> machineNodes = publishedNodeFilter.FilterMachineNodes(publishedNodes);
            foreach (MachineNode machineNode in machineNodes)
            {
                CreateSubscription(machineNode);
            }


        }
        private void CreateSubscription(MachineNode machineNode)
        {
            NodeId? nodeId = null;
            nodeId = machineNode.ResolvedNodeId;
            if (nodeId != null)
            {
                HierarchicalNode? hierarchicalNode = ReadNodeIdAsHierarchicalNode(null, nodeId);
                if (hierarchicalNode != null)
                {
                    rootNodes.Add(nodeId, hierarchicalNode);
                    Subscribe(hierarchicalNode);
                }
                else
                {
                    Logger.Error($"Unable to get HierarchicalNode for NodeId: {nodeId}");
                }
            }
            else
            {
                Logger.Error($"Unable to get node Id for Pub: {machineNode}");
            }
        }
        private void PreSubscribe(NodeId nodeId)
        {
            subscriptionIds.Add(nodeId);
        }
        public void Subscribe(HierarchicalNode hierarchicalNode)
        {   //Make a Pre subscription List
            PreSubscribe(hierarchicalNode.NodeId);
            //client.SubscribeToDataChanges(hierarchicalNode.NodeId, updateDataValue);
            foreach (HierarchicalNode hierarchicalChildNode in hierarchicalNode.hierarchicalChilds.Values)
            {
                Subscribe(hierarchicalChildNode);
            }
            createDataSetAndWritersNew(hierarchicalNode);
        }
       
        public string CreateTopic(HierarchicalNode hierarchicalProperty) 
        {
            UmatiConfiguration config = client.ActiveConfiguration;
            string topic = $"umati/v3/json/data/{config.MqttProviderConfig.ClientId}/{GetBrowsePath(hierarchicalProperty)}";
            return topic;
        }
        public string CreateMetaTopic(HierarchicalNode hierarchicalProperty)
        {
            UmatiConfiguration config = client.ActiveConfiguration;
            string topic = $"umati/v3/json/metadata/{config.MqttProviderConfig.ClientId}/{GetBrowsePath(hierarchicalProperty)}";
            return topic;
        }
        public string GetBrowsePath(HierarchicalNode hierarchicalNode, bool includeNamespaceIndex = false, string delimeter = "/")
        {
            string browsePath = "";
            if (includeNamespaceIndex)
            {
                browsePath = hierarchicalNode.BrowseName.ToString();
            } else
            {
                browsePath = hierarchicalNode.BrowseName.Name.ToString();
            }
            HierarchicalNode? parentNode = hierarchicalNode.Parent;
            while (parentNode != null)
            {
                if (includeNamespaceIndex)
                {
                    browsePath = parentNode.BrowseName.ToString() + delimeter + browsePath;
                }
                else
                {
                    browsePath = parentNode.BrowseName.Name.ToString() + delimeter + browsePath;
                }
                parentNode = parentNode.Parent;
            }
            return browsePath;
        }

        public void createDataSetAndWritersForObject(HierarchicalNode hierarchicalNode)
        {
            int uniqueint = ++counter;
            topic = CreateTopic(hierarchicalNode);
            metaTopic = CreateMetaTopic(hierarchicalNode);
            string dataSetName = $"DataSet_{publishedDataSets.Count + uniqueint}";
            PublishedVariableDataTypeCollection publishedVariableDataTypeCollection = new PublishedVariableDataTypeCollection();
            FieldMetaDataCollection fields = new FieldMetaDataCollection();
            if(hierarchicalNode.hierarchicalChilds.Count > 0)
            {
                foreach (KeyValuePair <NodeId, HierarchicalNode> hierarchicalChild in hierarchicalNode.hierarchicalChilds)
                {
                    if (NodeClass.Variable == hierarchicalChild.Value.NodeClass)
                    {
                        publishedVariableDataTypeCollection.Add( new PublishedVariableDataType
                        {
                            PublishedVariable = hierarchicalChild.Value.NodeId, AttributeId = Attributes.Value
                        });
                        fields.Add(hierarchicalChild.Value.fieldMetaData);
                    }
                }
            }
            //Add a Virtaul Id
            DataValue dataValue = new DataValue(GetBrowsePath(hierarchicalNode, true, "."));
            VirtualId virtualId = new VirtualId(new NodeId("virtualId_"+uniqueint, 1), dataValue);
            virtualIds.Add(virtualId);
            KeyValuePairCollection keyValuePairs = GetRealationsAsKeyValuePair(hierarchicalNode);
            FieldMetaData virtualIdMetaData = new FieldMetaData
            {
                Name = "virtualId",
                DataType = DataTypeIds.String,
                ValueRank = ValueRanks.Scalar,
                Description = "VirtualId used by the Gateway",
                BuiltInType = (byte)BuiltInType.String,
                DataSetFieldId = new Uuid(Guid.NewGuid()),
                Properties = keyValuePairs

            };
            fields.Add (virtualIdMetaData);
            publishedVariableDataTypeCollection.Add(new PublishedVariableDataType
            {
                PublishedVariable = virtualId.nodeId,
                AttributeId = Attributes.Value
            });

            var publishedDataItems = new PublishedDataItemsDataType
            {
                PublishedData = publishedVariableDataTypeCollection,
            };

            var publishedDataSet = new PublishedDataSetDataType
            {
                Name = "DataSet" + uniqueint,
                DataSetSource = new ExtensionObject(publishedDataItems),
                DataSetMetaData = new DataSetMetaDataType
                {
                    Name = "DataSet" + uniqueint,
                    Fields = fields,
                    ConfigurationVersion = new ConfigurationVersionDataType
                    {
                        MajorVersion = 1,
                        MinorVersion = 0
                    },
                    Namespaces = client.GetNamespaceTable().ToArray(),
                    StructureDataTypes = GetStructureDescriptions(hierarchicalNode),
                    EnumDataTypes = GetEnumDescriptions(hierarchicalNode),
                    SimpleDataTypes = GetSimpleTypeDescriptionCollection(hierarchicalNode),
                    Description = "MyDescription",
                }
            };

            // Create DataSetWriter
            var dataSetWriter = new DataSetWriterDataType
            {
                Name = "Writer" + uniqueint,
                DataSetWriterId = (ushort)uniqueint,
                DataSetFieldContentMask = (uint)DataSetFieldContentMask.RawData,
                DataSetName = "DataSet" + uniqueint,
                KeyFrameCount = 1,
                Enabled = true,
                MessageSettings = new ExtensionObject(new JsonDataSetWriterMessageDataType()),
                TransportSettings = new ExtensionObject(new BrokerDataSetWriterTransportDataType
                {
                    QueueName = "opcua/" + topic,
                    MetaDataQueueName = "opcua/" + metaTopic
                }),
            };
            // Create WriterGroup
            var writerGroup = new WriterGroupDataType
            {
                Name = "WriterGroup" + uniqueint,
                Enabled = true,
                PublishingInterval = 5000,
                MessageSettings = new ExtensionObject(new JsonWriterGroupMessageDataType
                {
                    NetworkMessageContentMask = (uint)(
                        JsonNetworkMessageContentMask.NetworkMessageHeader |
                        JsonNetworkMessageContentMask.DataSetMessageHeader |
                        JsonNetworkMessageContentMask.PublisherId
                    )
                }),
                TransportSettings = new ExtensionObject(new BrokerWriterGroupTransportDataType()),
                DataSetWriters = new DataSetWriterDataTypeCollection { dataSetWriter }
            };
            writerGroups.Add(writerGroup);
            publishedDataSets.Add(publishedDataSet);
        }

        private KeyValuePairCollection GetRealationsAsKeyValuePair(HierarchicalNode hierarchicalNode)
        {
            KeyValuePairCollection keyValuePairs = new KeyValuePairCollection();
            keyValuePairs.AddRange(referenceDescriptionResolver.ResolveReferences(hierarchicalNode));
            return keyValuePairs;
        }
        private StructureDescriptionCollection GetStructureDescriptions(HierarchicalNode hierarchicalNode)
        {
            StructureDescriptionCollection structureDescriptionCollection = new StructureDescriptionCollection();
            StructureDescription structureDescription = new StructureDescription();
            StructureFieldCollection structureFieldCollection = new StructureFieldCollection();
            StructureField structureField = new StructureField();
            /*structureField.BinaryEncodingId =;
            structureField.JsonEncodingId =;
            structureField.XmlEncodingId =;*/
            StructureDefinition structureDefinition = new StructureDefinition();
            structureDefinition.StructureType = StructureType.Structure;
            structureDefinition.Fields.Add(structureField);
            structureDescription.StructureDefinition = structureDefinition;
            return structureDescriptionCollection;
        }
        private EnumDescriptionCollection GetEnumDescriptions(HierarchicalNode hierarchicalNode)
        {
            EnumDescriptionCollection enumDescriptionCollection = new EnumDescriptionCollection();
            EnumDescription enumDescription = new EnumDescription();
            EnumDefinition enumDefinition = new EnumDefinition();
            EnumFieldCollection enumFieldCollection = new EnumFieldCollection();
            EnumField enumField = new EnumField();
            enumField.Name = "asd";
            enumField.Description = "Description";
            enumField.Value = 1;
            enumFieldCollection.Add(enumField);
            enumDefinition.Fields = enumFieldCollection;
            enumDescription.EnumDefinition = enumDefinition;
            enumDescriptionCollection.Add(enumDescription);
            return enumDescriptionCollection;
        }
        private SimpleTypeDescriptionCollection GetSimpleTypeDescriptionCollection(HierarchicalNode hierarchicalNode)
        {
            return new SimpleTypeDescriptionCollection();
        }
        public void createDataSetAndWritersForVariable(HierarchicalNode hierarchicalNode)
        {
            topic = CreateTopic(hierarchicalNode);
            metaTopic = CreateMetaTopic(hierarchicalNode);
            PublishedVariableDataTypeCollection publishedVariableDataTypeCollection = new PublishedVariableDataTypeCollection();
            FieldMetaDataCollection fields = new FieldMetaDataCollection();
            if (hierarchicalNode.hierarchicalChilds.Count > 0)
            {
                foreach (KeyValuePair<NodeId, HierarchicalNode> hierarchicalChild in hierarchicalNode.hierarchicalChilds)
                {
                    if (NodeClass.Variable == hierarchicalChild.Value.NodeClass)
                    {
                        publishedVariableDataTypeCollection.Add(new PublishedVariableDataType
                        {
                            PublishedVariable = hierarchicalChild.Value.NodeId,
                            AttributeId = Attributes.Value
                        });
                        fields.Add(hierarchicalChild.Value.fieldMetaData);
                    }
                }
            }
            if(publishedVariableDataTypeCollection.Count == 0)
            {
                return;
            }
            int uniqueint = ++counter;
            topic = CreateTopic(hierarchicalNode);
            string dataSetName = $"DataSet_{publishedDataSets.Count + uniqueint}";
            //Add a Virtaul Id
            DataValue dataValue = new DataValue(GetBrowsePath(hierarchicalNode, true, "."));
            VirtualId virtualId = new VirtualId(new NodeId("virtualId_" + uniqueint, 1), dataValue);
            virtualIds.Add(virtualId);
            KeyValuePairCollection keyValuePairs = GetRealationsAsKeyValuePair(hierarchicalNode);
            FieldMetaData virtualIdMetaData = new FieldMetaData
            {
                Name = "virtualId",
                DataType = DataTypeIds.String,
                ValueRank = ValueRanks.Scalar,
                Description = "VirtualId used by the Gateway",
                BuiltInType = (byte)BuiltInType.String,
                DataSetFieldId = new Uuid(Guid.NewGuid()),
                Properties = keyValuePairs

            };
            fields.Add(virtualIdMetaData);
            publishedVariableDataTypeCollection.Add(new PublishedVariableDataType
            {
                PublishedVariable = virtualId.nodeId,
                AttributeId = Attributes.Value
            });
            var publishedDataItems = new PublishedDataItemsDataType
            {
                PublishedData = publishedVariableDataTypeCollection,
            };

            var publishedDataSet = new PublishedDataSetDataType
            {
                Name = "DataSet" + uniqueint,
                DataSetSource = new ExtensionObject(publishedDataItems),
                DataSetMetaData = new DataSetMetaDataType
                {
                    Name = "DataSet" + uniqueint,
                    Fields = fields,
                    ConfigurationVersion = new ConfigurationVersionDataType
                    {
                        MajorVersion = 1,
                        MinorVersion = 0
                    },
                    Namespaces = client.GetNamespaceTable().ToArray(),
                    StructureDataTypes = GetStructureDescriptions(hierarchicalNode),
                    EnumDataTypes = GetEnumDescriptions(hierarchicalNode),
                    SimpleDataTypes = GetSimpleTypeDescriptionCollection(hierarchicalNode),
                    Description = "MyDescription",
                }
            };
            // Create DataSetWriter
            var dataSetWriter = new DataSetWriterDataType
            {
                Name = "Writer" + uniqueint,
                DataSetWriterId = (ushort)uniqueint,
                DataSetFieldContentMask = (uint)DataSetFieldContentMask.RawData,
                DataSetName = "DataSet" + uniqueint,
                KeyFrameCount = 1,
                Enabled = true,
                MessageSettings = new ExtensionObject(new JsonDataSetWriterMessageDataType()),
                TransportSettings = new ExtensionObject(new BrokerDataSetWriterTransportDataType
                {
                    QueueName = "opcua/" + topic,
                    MetaDataQueueName = "opcua/" + metaTopic
                })
            };
            // Create WriterGroup
            var writerGroup = new WriterGroupDataType
            {
                Name = "WriterGroup" + uniqueint,
                Enabled = true,
                PublishingInterval = 5000,
                MessageSettings = new ExtensionObject(new JsonWriterGroupMessageDataType
                {
                    NetworkMessageContentMask = (uint)(
                        JsonNetworkMessageContentMask.NetworkMessageHeader |
                        JsonNetworkMessageContentMask.DataSetMessageHeader |
                        JsonNetworkMessageContentMask.PublisherId
                    )
                }),
                TransportSettings = new ExtensionObject(new BrokerWriterGroupTransportDataType()),
                DataSetWriters = new DataSetWriterDataTypeCollection { dataSetWriter }
            };
            writerGroups.Add(writerGroup);
            publishedDataSets.Add(publishedDataSet);
        }
        public void createDataSetAndWritersNew(HierarchicalNode hierarchicalNode)
        {
            NodeClass? nodeClass = hierarchicalNode.NodeClass;
            switch (nodeClass)
            {
                case NodeClass.Variable: createDataSetAndWritersForVariable(hierarchicalNode); break;
                case NodeClass.Object: createDataSetAndWritersForObject(hierarchicalNode); break;
                case NodeClass.Method: 
                case NodeClass.ObjectType:
                case NodeClass.VariableType:
                case NodeClass.DataType:
                case NodeClass.Unspecified:
                case NodeClass.View:
                default: Logger.Trace($"No DataSet Created due to Nodeclass: {nodeClass}"); break;
            }
        }
        private void CreateApp()
        {
            var pubSubConnection = new PubSubConnectionDataType
            {
                Name = "MQTTConnection",
                Enabled = true,
                PublisherId = (ushort)1,
                TransportProfileUri = Profiles.PubSubMqttJsonTransport,
                Address = new ExtensionObject(new NetworkAddressUrlDataType
                {
                    Url = "mqtt://localhost:1883"
                }),
                ConnectionProperties = new KeyValuePairCollection
            {
                new Opc.Ua.KeyValuePair { Key = "mqtt.client.protocolVersion", Value = "5" }
            },
                WriterGroups = writerGroups
            };
            var pubSubConfiguration = new PubSubConfigurationDataType
            {
                PublishedDataSets = publishedDataSets,
                Connections = new PubSubConnectionDataTypeCollection { pubSubConnection }
            };


            pubSubDataStore = new UaPubSubDataStore();

            pubSubApp = UaPubSubApplication.Create(pubSubConfiguration, pubSubDataStore);
            pubSubApp.Start();
        }

        private void updateDataValue(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs monitoredItemsArgs)
        {
            
            if (monitoredItemsArgs.NotificationValue is MonitoredItemNotification valueNotification)
            {
                DataValue dv = valueNotification.Value;
                pubSubDataStore.WritePublishedDataItem(monitoredItem.ResolvedNodeId, Attributes.Value, dv);
                Logger.Info($"Added Value to PubSUb DataStore: {monitoredItem.ResolvedNodeId} \t {dv}");
            } 
            else
            {
                Logger.Error("Other notification");
            }
        }
        public NodeId? ResolveNodeId(PublishedNode publishedNode)
        {
            string type = publishedNode.Type.ToUpper();
            NodeId? nodeId = null;
            int namespaceIndex = client.GetNamespaceTable().GetIndex(publishedNode.NamespaceUrl);
            if (namespaceIndex != -1)
            {
                switch (type)
                {
                    case "NUMERIC": nodeId = new NodeId(Convert.ToUInt32(publishedNode.NodeId), (ushort)namespaceIndex); break;
                    case "STRING": nodeId = new NodeId(publishedNode.NodeId, (ushort)namespaceIndex); break;
                    default: break;
                }
            }
            else
            {
                Logger.Error($"Unable to get NamespaceIndex for NameSpaceUrl: {publishedNode.NamespaceUrl}");
            }
            return nodeId;
        }
        private HierarchicalNode? ReadNodeIdAsHierarchicalNode(HierarchicalNode? parent,NodeId nodeId)
        {
            HierarchicalNode? hierarchicalNode = null;
            Node? node = client.ReadNode(nodeId);
            
            if (node != null)
            {
                hierarchicalNode = new HierarchicalNode(nodeId, node.TypeId, node.BrowseName);
                hierarchicalNode.DisplayName = node.DisplayName;
                hierarchicalNode.Description = node.Description;
                hierarchicalNode.TypeDefinitionNodeId = node.TypeDefinitionId;
                hierarchicalNode.NodeClass = node.NodeClass;
                hierarchicalNode.Parent = parent;
                if(hierarchicalNode.TypeDefinitionNodeId == null)
                {
                    hierarchicalNode.TypeDefinitionNodeId = client.BrowseTypeDefinition(nodeId);
                }
                List<NodeId> childNodeIds = client.BrowseLocalNodeIds(nodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true);
                foreach(NodeId childNodeId in childNodeIds)
                {
                    NodeId? childTypeDefinition = client.BrowseTypeDefinition(childNodeId);
                    if(childTypeDefinition != null)
                    {
                        if(childTypeDefinition == VariableTypeIds.PropertyType)
                        {

                            HierarchicalNode? childNode = ReadNodeIdAsHierarchicalNode(hierarchicalNode, childNodeId);
                            if (childNode != null)
                            {
                                hierarchicalNode.hierarchicalChilds.Add(childNodeId, childNode);
                            }
                            else
                            {
                                Logger.Error($"Unable to read HierarchicalNode");
                            }
                        }
                        else
                        {
                            HierarchicalNode? childNode = ReadNodeIdAsHierarchicalNode(hierarchicalNode, childNodeId);
                            if (childNode != null)
                            {
                                hierarchicalNode.hierarchicalChilds.Add(childNodeId, childNode);
                            }
                            else
                            {
                                Logger.Error($"Unable to read HierarchicalNode");
                            }
                        }
                    } else
                    {
                        Logger.Error($"No TypeDefinition for ChildNodeId {childNodeId}");
                    }
                }
                if (node is VariableNode variableNode)
                {
                    FieldMetaData meta = new FieldMetaData
                    {
                        Name = variableNode.BrowseName.Name,
                        Description = variableNode.Description,
                        //BuiltInType = (byte)TypeInfo.GetBuiltInType(variableNode.DataType, session.TypeTree),
                        DataType = variableNode.DataType,
                        ValueRank = variableNode.ValueRank,
                        DataSetFieldId = new Uuid(Guid.NewGuid())
                    };
                    hierarchicalNode.fieldMetaData = meta;
            }
            } else
            {
                Logger.Error($"Unable to read Node for NodeId {nodeId}");
            }
            return hierarchicalNode;
        }
    }
    public class HierarchicalNode
    {
        public HierarchicalNode? Parent { get; set; }
        public NodeId NodeId { get; set; }
        public ExpandedNodeId TypeId { get; set; }
        public QualifiedName BrowseName { get; set; }
        public LocalizedText DisplayName { get; set; } = "";
        public LocalizedText Description { get; set; } = "";
        public ExpandedNodeId TypeDefinitionNodeId { get; set; } = "";

        public FieldMetaData? fieldMetaData = null;
        public TypeDefinitionNode? TypeDefinitionNode { get; set; } = null;
        public NodeClass? NodeClass { get; set; } = null;

        public Dictionary<NodeId, HierarchicalNode> hierarchicalChilds = new Dictionary<NodeId, HierarchicalNode>();
        public HierarchicalNode( NodeId nodeId, ExpandedNodeId typeId, QualifiedName browseName)
        {

            NodeId = nodeId;
            TypeId = typeId;
            BrowseName = browseName;
        }
    }

    public class TypeDefinitionNode
    {
        ExpandedNodeId NodeId { get; set; }
        IList<TypeChild> Children { get; set; } = new List<TypeChild>();
        public TypeDefinitionNode(NodeId nodeId)
        {
            NodeId = nodeId;
        }
    }
    public class TypeChild
    {
        NodeId ReferenceTypeId { get; set; }
        NodeId ModellingRule { get; set; }
        NodeId NodeId { get; set; }
        NodeId Origin { get; set; }
        NodeClass NodeClass { get; set; }

        public TypeChild(NodeId nodeId, NodeClass nodeClass, NodeId referenceTypeId, NodeId origin, NodeId modellingRule)
        {
            NodeId = nodeId;
            ReferenceTypeId = referenceTypeId;
            Origin = origin;
            NodeClass = nodeClass;
            ModellingRule = modellingRule;
        }
    }
    public class VirtualId
    {
        public NodeId nodeId;
        public DataValue dv;
        public VirtualId(NodeId nodeId, DataValue dv)
        {
            this.nodeId = nodeId;
            this.dv = dv;
        }
    }
}
