// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using NLog;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.PubSub;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.Mqtt;
using umatiGateway.Core.OPC;

namespace umatiGateway.Core.PubSub
{
    public class PubSubProvider
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string topic = "";
        private string metaTopic = "";
        private int counter = 0;
        private List<NodeId> subscriptionIds = new List<NodeId>();
        private System.Timers.Timer keepAliveTimer = new System.Timers.Timer();
        private UmatiGatewayApp app;
        private IOpcUaClient client;
        private Dictionary<NodeId, HierarchicalNode> rootNodes = new Dictionary<NodeId, HierarchicalNode>();
        private PubSubConfigurationDataType PubSubConfigurationDataType = new PubSubConfigurationDataType();
        private PubSubConnectionDataType PubSubConnectionDataType = new PubSubConnectionDataType();
        private UaPubSubApplication? pubSubApp = null;
        private UaPubSubDataStore pubSubDataStore = new UaPubSubDataStore();
        private PublishedDataSetDataTypeCollection publishedDataSets = new();
        private DataSetWriterDataTypeCollection dataSetWriters = new();
        private WriterGroupDataTypeCollection writerGroups = new();
        private List<VirtualId> virtualIds = new List<VirtualId>();
        private WriterGroupDataType WriterGroup = new WriterGroupDataType();
        private readonly System.Timers.Timer pubTestTimer = new System.Timers.Timer(5000);
        private ReferenceDescriptionResolver referenceDescriptionResolver;
        private bool alwaysIncludeBrowsePathIndex = false;
        public List<MachineNode> MachineNodes { get; set; } = new List<MachineNode>();

        public PubSubProvider(UmatiGatewayApp app)
        {
            this.app = app;
            this.client = app.OpcUaClient;
            this.referenceDescriptionResolver = new ReferenceDescriptionResolver(client);
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
            foreach (VirtualId virtualId in virtualIds)
            {
                pubSubDataStore.WritePublishedDataItem(virtualId.nodeId, Attributes.Value, virtualId.dv);
                Logger.Info($"Added value to PubSub DataStore: {virtualId.nodeId} \t {virtualId.dv}");
            }
        }
        public void Disconnect()
        {
            Logger.Info("Disconnecting");
            if (pubSubApp != null)
            {
                this.pubSubApp.Stop();
                this.pubSubApp.Dispose();
                this.pubSubApp = null;
                this.pubSubDataStore = new UaPubSubDataStore();
                this.publishedDataSets = new();
                this.dataSetWriters = new();
                this.writerGroups = new();
                this.virtualIds = new List<VirtualId>();
                this.WriterGroup = new WriterGroupDataType();
                this.subscriptionIds = new List<NodeId>();
                this.rootNodes = new Dictionary<NodeId, HierarchicalNode>();
            }
            Logger.Info("Disconnected");
        }

        private void CreateSubscriptions()
        {
            PublishedNodeFilter publishedNodeFilter = new PublishedNodeFilter(app);
            List<PublishedNode> publishedNodes = app.ActiveConfiguration.PubSubProviderConfig.PublishedNodes;
            this.MachineNodes = publishedNodeFilter.FilterMachineNodes(publishedNodes);
            foreach (MachineNode machineNode in MachineNodes)
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
                Logger.Error($"Unable to get NodeId for publishing: {machineNode}");
            }
        }
        private void PreSubscribe(NodeId nodeId)
        {
            subscriptionIds.Add(nodeId);
        }
        public void Subscribe(HierarchicalNode hierarchicalNode)
        {
            PreSubscribe(hierarchicalNode.NodeId);
            foreach (HierarchicalNode hierarchicalChildNode in hierarchicalNode.hierarchicalChilds.Values)
            {
                Subscribe(hierarchicalChildNode);
            }
            createDataSetAndWritersNew(hierarchicalNode);
        }
        public string CreateTopic(HierarchicalNode hierarchicalProperty)
        {
            UmatiConfiguration config = app.ActiveConfiguration;
            string topic = $"{config.PubSubProviderConfig.Prefix}/json/data/{config.PubSubProviderConfig.ClientId}/{GetBrowsePath(hierarchicalProperty)}";
            return topic;
        }
        public string CreateMetaTopic(HierarchicalNode hierarchicalProperty)
        {
            UmatiConfiguration config = app.ActiveConfiguration;
            string topic = $"{config.PubSubProviderConfig.Prefix}/json/metadata/{config.PubSubProviderConfig.ClientId}/{GetBrowsePath(hierarchicalProperty)}";
            return topic;
        }
        public string GetBrowsePath(HierarchicalNode hierarchicalNode, bool includeNamespaceIndex = false, string delimeter = "/")
        {
            string browsePath = "";
            if (includeNamespaceIndex || alwaysIncludeBrowsePathIndex)
            {
                browsePath = hierarchicalNode.BrowseName.ToString();
            }
            else
            {
                browsePath = hierarchicalNode.BrowseName.Name.ToString();
            }
            HierarchicalNode? parentNode = hierarchicalNode.Parent;
            while (parentNode != null)
            {
                if (includeNamespaceIndex || alwaysIncludeBrowsePathIndex)
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
            if (hierarchicalNode.hierarchicalChilds.Count > 0)
            {
                foreach (KeyValuePair<NodeId, HierarchicalNode> hierarchicalChild in hierarchicalNode.hierarchicalChilds)
                {
                    if (NodeClass.Variable == hierarchicalChild.Value.NodeClass)
                    {
                        publishedVariableDataTypeCollection.Add(
                        new PublishedVariableDataType
                        {
                            PublishedVariable = hierarchicalChild.Value.NodeId,
                            AttributeId = Attributes.Value
                        });
                        fields.Add(hierarchicalChild.Value.fieldMetaData);
                    }
                }
            }
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
            Opc.Ua.KeyValuePair keyValuePair = new Opc.Ua.KeyValuePair();
            keyValuePair.Key = new QualifiedName($"relations", 0);
            keyValuePair.Value = new Variant(keyValuePairs.Select(kv => kv.Value.Value).ToArray());
            KeyValuePairCollection relationsAsArray = new KeyValuePairCollection();
            relationsAsArray.Add(keyValuePair);
            return relationsAsArray;
        }
        private StructureDescriptionCollection GetStructureDescriptions(HierarchicalNode hierarchicalNode)
        {
            StructureDescriptionCollection structureDescriptionCollection = new StructureDescriptionCollection();
            StructureDescription structureDescription = new StructureDescription();
            StructureFieldCollection structureFieldCollection = new StructureFieldCollection();
            StructureField structureField = new StructureField();
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
            if (publishedVariableDataTypeCollection.Count == 0)
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
                default: Logger.Trace($"No DataSet created due to NodeClass: {nodeClass}"); break;
            }
        }
        private void CreateApp()
        {
            KeyValuePairCollection connectionProperties = new KeyValuePairCollection();
            if (!string.IsNullOrEmpty(this.app.ActiveConfiguration.PubSubProviderConfig.UserName))
            {
                Opc.Ua.KeyValuePair user = new Opc.Ua.KeyValuePair
                {
                    Key = "UserName",
                    Value = new Variant(this.app.ActiveConfiguration.PubSubProviderConfig.UserName)
                };
                Opc.Ua.KeyValuePair password = new Opc.Ua.KeyValuePair
                {
                    Key = "Password",
                    Value = new Variant(this.app.ActiveConfiguration.PubSubProviderConfig.Password)
                };
                connectionProperties.Add(user);
                connectionProperties.Add(password);
            }
            Opc.Ua.KeyValuePair protocolVersion = new Opc.Ua.KeyValuePair
            {
                Key = "ProtocolVersion",
                Value = new Variant(5)
            };
            connectionProperties.Add(protocolVersion);
            if (app.ActiveConfiguration.PubSubProviderConfig.AllowUntrustedCertificates)
            {
                Opc.Ua.KeyValuePair tlsAllowUntrustedCertificates = new Opc.Ua.KeyValuePair
                {
                    Key = "TlsAllowUntrustedCertificates",
                    Value = new Variant(true)
                };
                connectionProperties.Add(tlsAllowUntrustedCertificates);
                Opc.Ua.KeyValuePair tlsIgnoreCertificateChainErrors = new Opc.Ua.KeyValuePair
                {
                    Key = "TlsIgnoreCertificateChainErrors",
                    Value = new Variant(true)
                };
                connectionProperties.Add(tlsIgnoreCertificateChainErrors);
                Opc.Ua.KeyValuePair tlsIgnoreRevocationListErrors = new Opc.Ua.KeyValuePair
                {
                    Key = "TlsIgnoreRevocationListErrors",
                    Value = new Variant(true)
                };
                connectionProperties.Add(tlsIgnoreRevocationListErrors);
            }
            var pubSubConnection = new PubSubConnectionDataType
            {
                Name = "MQTTConnection",
                Enabled = true,
                PublisherId = (ushort)1,
                TransportProfileUri = Profiles.PubSubMqttJsonTransport,
                Address = new ExtensionObject(new NetworkAddressUrlDataType
                {
                    Url = this.app.ActiveConfiguration.PubSubProviderConfig.ServerEndpoint
                }),
                ConnectionProperties = connectionProperties,
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
                Logger.Info($"Updated Value in PubSub DataStore: {monitoredItem.ResolvedNodeId} \t {dv}");
            }
            else
            {
                Logger.Info($"Unexpected Notification type: {monitoredItemsArgs.NotificationValue}");
            }
        }

        private HierarchicalNode? ReadNodeIdAsHierarchicalNode(HierarchicalNode? parent, NodeId nodeId)
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
                if (hierarchicalNode.TypeDefinitionNodeId == null)
                {
                    hierarchicalNode.TypeDefinitionNodeId = client.BrowseTypeDefinition(nodeId);
                }
                List<NodeId> childNodeIds = client.BrowseLocalNodeIds(nodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true);
                foreach (NodeId childNodeId in childNodeIds)
                {
                    NodeId? childTypeDefinition = client.BrowseTypeDefinition(childNodeId);
                    if (childTypeDefinition != null)
                    {
                        if (childTypeDefinition == VariableTypeIds.PropertyType)
                        {

                            HierarchicalNode? childNode = ReadNodeIdAsHierarchicalNode(hierarchicalNode, childNodeId);
                            if (childNode != null)
                            {
                                if (!hierarchicalNode.hierarchicalChilds.Keys.Contains(childNodeId))
                                {
                                    hierarchicalNode.hierarchicalChilds.Add(childNodeId, childNode);
                                }
                                else
                                {
                                    Logger.Warn($"Double child NodeId {childNodeId} in HierarchicalNode {hierarchicalNode.NodeId}");
                                }
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
                                if (!hierarchicalNode.hierarchicalChilds.Keys.Contains(childNodeId))
                                {
                                    hierarchicalNode.hierarchicalChilds.Add(childNodeId, childNode);
                                }
                                else
                                {
                                    Logger.Warn($"Double child NodeId {childNodeId} in HierarchicalNode {hierarchicalNode.NodeId}");
                                }
                            }
                            else
                            {
                                Logger.Error($"Unable to read HierarchicalNode");
                            }
                        }
                    }
                    else
                    {
                        Logger.Error($"No TypeDefinition for child NodeId {childNodeId}");
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
            }
            else
            {
                Logger.Error($"Unable to read Node for NodeId {nodeId}");
            }
            return hierarchicalNode;
        }
    }
}
