// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using MQTTnet.Packets;
using NLog;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.PubSub;
using System;
using System.Collections.Concurrent;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.External.Opc.Ua.PubSub.Encoding;
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
        private Dictionary<string, string> metaTopics = new Dictionary<string, string>();
        private Dictionary<string, string> topics = new Dictionary<string, string>();
        private List<Replacement> topicReplacements = new List<Replacement>();
        private List<Replacement> metaTopicReplacements = new List<Replacement>();
        private readonly BlockingCollection<(MonitoredItem, MonitoredItemNotificationEventArgs)> notificationqueue = new BlockingCollection<(MonitoredItem, MonitoredItemNotificationEventArgs)>();
        public List<MachineNode> MachineNodes { get; set; } = new List<MachineNode>();

        public PubSubProvider(UmatiGatewayApp app)
        {
            this.app = app;
            this.client = app.OpcUaClient;
            this.referenceDescriptionResolver = new ReferenceDescriptionResolver(client);
            this.StartWorker();
        }
        public void Connect()
        {
            this.metaTopics.Clear();
            this.topics.Clear();
            this.metaTopicReplacements.Clear();
            this.topicReplacements.Clear();
            JsonEncoding jsonEncoding = this.app.ActiveConfiguration.PubSubProviderConfig.JsonEncoding;
            switch (jsonEncoding)
            {
                case JsonEncoding.REVERSIBLE:
                    JsonEncodingConfiguration.UseCustomizedEncoding = true;
                    JsonEncodingConfiguration.jsonEncodingType = JsonEncodingType.Reversible;
                    break;
                case JsonEncoding.NON_REVERSIBLE:
                    JsonEncodingConfiguration.UseCustomizedEncoding = true;
                    JsonEncodingConfiguration.jsonEncodingType = JsonEncodingType.NonReversible;
                    break;
                case JsonEncoding.COMPACT:
                    JsonEncodingConfiguration.UseCustomizedEncoding = true;
                    JsonEncodingConfiguration.jsonEncodingType = JsonEncodingType.Compact;
                    break;
                case JsonEncoding.VERBOSE:
                    JsonEncodingConfiguration.UseCustomizedEncoding = true;
                    JsonEncodingConfiguration.jsonEncodingType = JsonEncodingType.Verbose;
                    break;
                case JsonEncoding.LEGACY:
                default:
                    JsonEncodingConfiguration.UseCustomizedEncoding = false;
                    JsonEncodingConfiguration.jsonEncodingType = JsonEncodingType.Compact;
                    break;
            }
            Session session = this.client.CheckSession();
            session.FetchTypeTree(DataTypeIds.BaseDataType);
            session.FetchTypeTree(ObjectTypeIds.BaseObjectType);
            var typeSystem = new Opc.Ua.Client.ComplexTypes.ComplexTypeSystem(session);
            typeSystem.LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            referenceDescriptionResolver = new ReferenceDescriptionResolver(client);
            CreateSubscriptions();
            client.SubscribeToDataChanges(subscriptionIds, updateDataValue);
            this.AddStatusTopic();
            this.PrepareConnection();
            this.AddConnectionTopic();
            AddVirtualNodeIdsToStore();
            CreateApp();


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
            createDataSetAndWritersNew(hierarchicalNode);
            foreach (HierarchicalNode hierarchicalChildNode in hierarchicalNode.hierarchicalChilds.Values)
            {
                Subscribe(hierarchicalChildNode);
            }
            //createDataSetAndWritersNew(hierarchicalNode);
        }
        public string CreateTopic(HierarchicalNode hierarchicalProperty)
        {
            UmatiConfiguration config = app.ActiveConfiguration;
            string nstopic = $"{config.PubSubProviderConfig.Prefix}/json/data/{config.PubSubProviderConfig.ClientId}/{GetBrowsePath(hierarchicalProperty, true)}";
            string topic = $"{config.PubSubProviderConfig.Prefix}/json/data/{config.PubSubProviderConfig.ClientId}/{GetBrowsePath(hierarchicalProperty)}";
            foreach (KeyValuePair<string, string> knownTopic in this.topics)
            {
                if (topic == knownTopic.Value)
                {
                    this.topicReplacements.Add(new Replacement(nstopic, topic, $"{config.PubSubProviderConfig.Prefix}/json/data/{config.PubSubProviderConfig.ClientId}/{GetBrowsePath(hierarchicalProperty, true, true)}"));
                }
            }
            topics.TryAdd(nstopic, topic);
            for (int i = this.topicReplacements.Count - 1; i >= 0; i--)
            {
                Replacement replacement = this.topicReplacements[i];
                if (nstopic.StartsWith(replacement.nsTopic))
                {
                    topic = topic.Substring(replacement.topic.Length);
                    topic = replacement.replaceTopic + topic;
                }
            }
            return topic;
        }
        public string CreateMetaTopic(HierarchicalNode hierarchicalProperty)
        {
            UmatiConfiguration config = app.ActiveConfiguration;
            string nsMetaTopic = $"{config.PubSubProviderConfig.Prefix}/json/metadata/{config.PubSubProviderConfig.ClientId}/{GetBrowsePath(hierarchicalProperty, true)}";
            string metaTopic = $"{config.PubSubProviderConfig.Prefix}/json/metadata/{config.PubSubProviderConfig.ClientId}/{GetBrowsePath(hierarchicalProperty)}";
            foreach (KeyValuePair<string, string> knownMetaTopic in this.metaTopics)
            {
                if (metaTopic == knownMetaTopic.Value)
                {
                    this.metaTopicReplacements.Add(new Replacement(nsMetaTopic, metaTopic, $"{config.PubSubProviderConfig.Prefix}/json/metadata/{config.PubSubProviderConfig.ClientId}/{GetBrowsePath(hierarchicalProperty, true, true)}"));
                }
            }
            this.metaTopics.TryAdd(nsMetaTopic, metaTopic);
            for (int i = this.metaTopicReplacements.Count - 1; i >= 0; i--)
            {
                Replacement replacement = this.metaTopicReplacements[i];
                if (nsMetaTopic.StartsWith(replacement.nsTopic))
                {
                    metaTopic = metaTopic.Substring(replacement.topic.Length);
                    metaTopic = replacement.replaceTopic + metaTopic;
                }
            }
            return metaTopic;
        }
        public string GetBrowsePath(HierarchicalNode hierarchicalNode, bool includeNamespaceIndex = false, bool onlyIndexCurrentNode = false, string delimiter = "/")
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
                if ((includeNamespaceIndex && !onlyIndexCurrentNode) || alwaysIncludeBrowsePathIndex)
                {
                    browsePath = parentNode.BrowseName.ToString() + delimiter + browsePath;
                }
                else
                {
                    browsePath = parentNode.BrowseName.Name.ToString() + delimiter + browsePath;
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
            string dataSetName = new ExpandedNodeId(hierarchicalNode.NodeId).ToString();
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
            //Add a Virtual Id
            DataValue dataValue = new DataValue(GetBrowsePath(hierarchicalNode, true, false, "."));
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
                Name = dataSetName,
                DataSetSource = new ExtensionObject(publishedDataItems),
                DataSetMetaData = new DataSetMetaDataType
                {
                    Name = dataSetName,
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
                    Description = hierarchicalNode.Description,
                }
            };

            // Create DataSetWriter
            var dataSetWriter = new DataSetWriterDataType
            {
                Name = "Writer" + uniqueint,
                DataSetWriterId = (ushort)uniqueint,
                DataSetFieldContentMask = (uint)DataSetFieldContentMask.RawData,
                DataSetName = dataSetName,
                KeyFrameCount = 1,
                Enabled = true,
                MessageSettings = new ExtensionObject(new JsonDataSetWriterMessageDataType()),
                TransportSettings = new ExtensionObject(new BrokerDataSetWriterTransportDataType
                {
                    QueueName = "opcua/" + topic,
                    MetaDataQueueName = "opcua/" + metaTopic,
                    MetaDataUpdateTime = app.ActiveConfiguration.PubSubProviderConfig.MetaDataUpdateTime
                }),
            };
            // Create WriterGroup
            var writerGroup = new WriterGroupDataType
            {
                Name = "WriterGroup" + uniqueint,
                Enabled = true,
                PublishingInterval = app.ActiveConfiguration.PubSubProviderConfig.PublishInterval,
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
        private void AddStatusTopic()
        {
            int uniqueint = ++counter;
            UmatiConfiguration config = this.app.ActiveConfiguration;
            string topic = $"{config.PubSubProviderConfig.Prefix}/json/status/{config.PubSubProviderConfig.ClientId}";
            string dataSetName = "StatusDataSet";
            PublishedVariableDataTypeCollection publishedVariableDataTypeCollection = new PublishedVariableDataTypeCollection();
            FieldMetaDataCollection fields = new FieldMetaDataCollection();
            PropertyState<PubSubState> state = new PropertyState<PubSubState>(null)
            {
                SymbolicName = "State",
                ReferenceTypeId = ReferenceTypeIds.HasProperty,
                TypeDefinitionId = VariableTypeIds.PropertyType,
                DataType = DataTypeIds.PubSubState,
                ValueRank = ValueRanks.Scalar,
                BrowseName = new QualifiedName("State", 100),
                DisplayName = new LocalizedText("State"),
                Description = new LocalizedText("The state of the PubSubConnection"),
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentRead,
                Value = PubSubState.Operational, // or Disabled, etc.
                StatusCode = Opc.Ua.StatusCodes.Good,
                Timestamp = DateTime.UtcNow

            };
            DataValue dataValue = new DataValue(state.Value.ToString());
            VirtualId virtualId = new VirtualId(new NodeId("status", 100), dataValue);
            virtualIds.Add(virtualId);
            publishedVariableDataTypeCollection.Add(
                new PublishedVariableDataType
                {
                    PublishedVariable = virtualId.nodeId,
                    AttributeId = Attributes.Value
                });
            fields.Add(new FieldMetaData
            {
                Name = state.BrowseName.Name,
                Description = state.Description,
                DataType = state.DataType,
                ValueRank = state.ValueRank,
                DataSetFieldId = new Uuid(Guid.NewGuid())
            });

            var publishedDataItems = new PublishedDataItemsDataType
            {
                PublishedData = publishedVariableDataTypeCollection,
            };

            var publishedDataSet = new PublishedDataSetDataType
            {
                Name = dataSetName,
                DataSetSource = new ExtensionObject(publishedDataItems),
                DataSetMetaData = new DataSetMetaDataType
                {
                    Name = dataSetName,
                    Fields = fields,
                    ConfigurationVersion = new ConfigurationVersionDataType
                    {
                        MajorVersion = 1,
                        MinorVersion = 0
                    },
                    Namespaces = client.GetNamespaceTable().ToArray(),
                    Description = "MyDescription",
                }
            };

            // Create DataSetWriter
            var dataSetWriter = new DataSetWriterDataType
            {
                Name = "Writer" + uniqueint,
                DataSetWriterId = (ushort)uniqueint,
                DataSetFieldContentMask = (uint)DataSetFieldContentMask.RawData,
                DataSetName = dataSetName,
                KeyFrameCount = 1,
                Enabled = true,
                MessageSettings = new ExtensionObject(new JsonDataSetWriterMessageDataType()),
                TransportSettings = new ExtensionObject(new BrokerDataSetWriterTransportDataType
                {
                    QueueName = "opcua/" + topic,
                }),
            };
            // Create WriterGroup
            var writerGroup = new WriterGroupDataType
            {
                Name = "WriterGroup" + uniqueint,
                Enabled = true,
                PublishingInterval = app.ActiveConfiguration.PubSubProviderConfig.PublishInterval,
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

        private void AddConnectionTopic()
        {
            int uniqueint = ++counter;
            UmatiConfiguration config = this.app.ActiveConfiguration;
            string topic = $"{config.PubSubProviderConfig.Prefix}/json/connection/{config.PubSubProviderConfig.ClientId}";
            string dataSetName = "ConnectionDataSet";
            PublishedVariableDataTypeCollection publishedVariableDataTypeCollection = new PublishedVariableDataTypeCollection();
            FieldMetaDataCollection fields = new FieldMetaDataCollection();
            PubSubConnectionDataType pubSubConnectionDataType = PubSubConnectionDataType;
            PubSubConnectionDataType pubSubConnectionDataTypeWithoutPassword = (PubSubConnectionDataType)pubSubConnectionDataType.Clone();
            this.RemoveConnectionProperty(pubSubConnectionDataTypeWithoutPassword, new QualifiedName("UserName"));
            this.RemoveConnectionProperty(pubSubConnectionDataTypeWithoutPassword, new QualifiedName("Password"));
            PropertyState<PubSubConnectionDataType> connection = new PropertyState<PubSubConnectionDataType>(null)
            {
                SymbolicName = "Connection",
                ReferenceTypeId = ReferenceTypeIds.HasProperty,
                TypeDefinitionId = VariableTypeIds.PropertyType,
                DataType = DataTypeIds.PubSubConnectionDataType,
                ValueRank = ValueRanks.Scalar,
                BrowseName = new QualifiedName("connection", 100),
                DisplayName = new LocalizedText("connection"),
                Description = new LocalizedText("The PubSubConnection"),
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentRead,
                Value = pubSubConnectionDataTypeWithoutPassword,
                StatusCode = Opc.Ua.StatusCodes.Good,
                Timestamp = DateTime.UtcNow

            };
            DataValue dataValue = new DataValue(connection.WrappedValue);
            VirtualId virtualId = new VirtualId(new NodeId("connection", 100), dataValue);
            virtualIds.Add(virtualId);
            publishedVariableDataTypeCollection.Add(
                new PublishedVariableDataType
                {
                    PublishedVariable = virtualId.nodeId,
                    AttributeId = Attributes.Value
                });
            fields.Add(new FieldMetaData
            {
                Name = connection.BrowseName.Name,
                Description = connection.Description,
                DataType = connection.DataType,
                ValueRank = connection.ValueRank,
                DataSetFieldId = new Uuid(Guid.NewGuid())
            });

            var publishedDataItems = new PublishedDataItemsDataType
            {
                PublishedData = publishedVariableDataTypeCollection,
            };

            var publishedDataSet = new PublishedDataSetDataType
            {
                Name = dataSetName,
                DataSetSource = new ExtensionObject(publishedDataItems),
                DataSetMetaData = new DataSetMetaDataType
                {
                    Name = dataSetName,
                    Fields = fields,
                    ConfigurationVersion = new ConfigurationVersionDataType
                    {
                        MajorVersion = 1,
                        MinorVersion = 0
                    },
                    Namespaces = client.GetNamespaceTable().ToArray(),
                    Description = "MyDescription",
                }
            };

            // Create DataSetWriter
            var dataSetWriter = new DataSetWriterDataType
            {
                Name = "Writer" + uniqueint,
                DataSetWriterId = (ushort)uniqueint,
                DataSetFieldContentMask = (uint)DataSetFieldContentMask.RawData,
                DataSetName = dataSetName,
                KeyFrameCount = 1,
                Enabled = true,
                MessageSettings = new ExtensionObject(new JsonDataSetWriterMessageDataType()),
                TransportSettings = new ExtensionObject(new BrokerDataSetWriterTransportDataType
                {
                    QueueName = "opcua/" + topic,
                }),
            };
            // Create WriterGroup
            var writerGroup = new WriterGroupDataType
            {
                Name = "WriterGroup" + uniqueint,
                Enabled = true,
                PublishingInterval = app.ActiveConfiguration.PubSubProviderConfig.PublishInterval,
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
        // remove one specific key
        void RemoveConnectionProperty(PubSubConnectionDataType conn, QualifiedName keyToRemove)
        {
            KeyValuePairCollection kvpc = conn.ConnectionProperties;
            for (int i = 0; i < kvpc.Count; i++)
            {
                if (kvpc[i].Key.Equals(keyToRemove))
                {
                    kvpc.RemoveAt(i);
                }
            }
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
        private EnumDescriptionCollection GetEnumDescriptions(HierarchicalNode hierarchicalNode, bool includeDirectChilds = true)
        {
            EnumDescriptionCollection enumDescriptionCollection = new EnumDescriptionCollection();
            FieldMetaData? fieldMetaData = hierarchicalNode.fieldMetaData;
            if (fieldMetaData != null)
            {
                if (fieldMetaData != null && fieldMetaData.BuiltInType == (byte)BuiltInType.Enumeration)
                {
                    NodeId dataType = fieldMetaData.DataType;
                    if (dataType != null)
                    {
                        DataTypeNode? dataTypeNode = this.client.ReadNode(dataType) as DataTypeNode;
                        if (dataTypeNode != null)
                        {

                            EnumFieldCollection enumFieldCollection = new EnumFieldCollection();
                            BrowseResultCollection browseResultCollection = this.client.BrowseNode(dataType, BrowseDirection.Forward, (int)NodeClass.Variable, ReferenceTypeIds.HasProperty, true);
                            foreach (BrowseResult browseResult in browseResultCollection)
                            {
                                ReferenceDescriptionCollection referenceDescriptionCollection = browseResult.References;
                                foreach (ReferenceDescription referenceDescription in referenceDescriptionCollection)
                                {
                                    if (referenceDescription.BrowseName == BrowseNames.EnumValues)
                                    {
                                        EnumValueType[] enumValues = new EnumValueType[0];
                                        ExpandedNodeId enumValuesNodeIdExp = referenceDescription.NodeId;
                                        NodeId enumValuesNodeId = new NodeId(enumValuesNodeIdExp.Identifier, enumValuesNodeIdExp.NamespaceIndex);
                                        DataValue? dv = this.client.ReadValue(enumValuesNodeId);
                                        if (dv != null)
                                        {
                                            if (dv.Value is ExtensionObject[] exArr)
                                            {
                                                enumValues = new EnumValueType[exArr.Length];
                                                for (int i = 0; i < exArr.Length; i++)
                                                    enumValues[i] = (EnumValueType)exArr[i].Body;
                                            }
                                            else if (dv.Value is EnumValueType[] direct)
                                            {
                                                enumValues = direct;
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException($"Unerwarteter Typ für EnumValues: {dv.Value?.GetType().FullName}");
                                            }
                                        }
                                        foreach (EnumValueType ev in enumValues)
                                        {
                                            enumFieldCollection.Add(new EnumField
                                            {
                                                Name = ev.DisplayName?.Text ?? $"V_{ev.Value}",
                                                Value = ev.Value,
                                                Description = ev.Description ?? LocalizedText.Null
                                            });
                                        }
                                    }
                                    else if (referenceDescription.BrowseName == BrowseNames.EnumStrings)
                                    {
                                        ExpandedNodeId enumStringsNodeIdExp = referenceDescription.NodeId;
                                        NodeId enumStringsNodeId = new NodeId(enumStringsNodeIdExp.Identifier, enumStringsNodeIdExp.NamespaceIndex);

                                        DataValue? dv = this.client.ReadValue(enumStringsNodeId);
                                        if (dv != null)
                                        {
                                            LocalizedText[] ltArr = Array.Empty<LocalizedText>();

                                            if (dv.Value is LocalizedText[] localizedTexts)
                                            {
                                                ltArr = localizedTexts;
                                            }
                                            else if (dv.Value is string[] strArr)
                                            {
                                                ltArr = strArr.Select(s => new LocalizedText(s)).ToArray();
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException($"Unerwarteter Typ für EnumStrings: {dv.Value?.GetType().FullName}");
                                            }

                                            for (int i = 0; i < ltArr.Length; i++)
                                            {
                                                enumFieldCollection.Add(new EnumField
                                                {
                                                    Name = ltArr[i].Text ?? $"V_{i}",
                                                    Value = (long)i,
                                                    Description = LocalizedText.Null
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                            EnumDefinition enumDefinition = new EnumDefinition { Fields = enumFieldCollection };
                            EnumDescription enumDescription = new EnumDescription
                            {
                                DataTypeId = dataType,
                                Name = dataTypeNode.BrowseName,
                                EnumDefinition = enumDefinition
                            };
                            enumDescriptionCollection.Add(enumDescription);
                        }
                    }
                }
            }
            if (includeDirectChilds)
            {
                foreach (KeyValuePair<NodeId, HierarchicalNode> childEntry in hierarchicalNode.hierarchicalChilds)
                {
                    enumDescriptionCollection.AddRange(GetEnumDescriptions(childEntry.Value, false));
                }
            }
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
            string dataSetName = new ExpandedNodeId(hierarchicalNode.NodeId).ToString();
            //Add a Virtual Id
            DataValue dataValue = new DataValue(GetBrowsePath(hierarchicalNode, true, false, "."));
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
                Name = dataSetName,
                DataSetSource = new ExtensionObject(publishedDataItems),
                DataSetMetaData = new DataSetMetaDataType
                {
                    Name = dataSetName,
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
                    Description = hierarchicalNode.Description,
                }
            };
            // Create DataSetWriter
            var dataSetWriter = new DataSetWriterDataType
            {
                Name = "Writer" + uniqueint,
                DataSetWriterId = (ushort)uniqueint,
                DataSetFieldContentMask = (uint)DataSetFieldContentMask.RawData,
                DataSetName = dataSetName,
                KeyFrameCount = 1,
                Enabled = true,
                MessageSettings = new ExtensionObject(new JsonDataSetWriterMessageDataType()),
                TransportSettings = new ExtensionObject(new BrokerDataSetWriterTransportDataType
                {
                    QueueName = "opcua/" + topic,
                    MetaDataQueueName = "opcua/" + metaTopic,
                    MetaDataUpdateTime = app.ActiveConfiguration.PubSubProviderConfig.MetaDataUpdateTime
                })
            };
            // Create WriterGroup
            var writerGroup = new WriterGroupDataType
            {
                Name = "WriterGroup" + uniqueint,
                Enabled = true,
                PublishingInterval = app.ActiveConfiguration.PubSubProviderConfig.PublishInterval,
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
        private void PrepareConnection()
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
            PubSubConnectionDataType = new PubSubConnectionDataType
            {
                Name = "umatiGatewayConnection",
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
            PubSubConfigurationDataType = new PubSubConfigurationDataType
            {
                PublishedDataSets = publishedDataSets,
                Connections = new PubSubConnectionDataTypeCollection { PubSubConnectionDataType }
            };
        }
        private void CreateApp()
        {
            pubSubApp = UaPubSubApplication.Create(PubSubConfigurationDataType, pubSubDataStore);
            pubSubApp.Start();
        }

        private void updateDataValue(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs monitoredItemsArgs)
        {
            if (!this.notificationqueue.TryAdd((monitoredItem, monitoredItemsArgs), 0))
            {
                Logger.Error("Notification dropped, queue full");
            }

        }
        public void StartWorker()
        {
            Task.Run(() =>
            {
                foreach (var (monitoredItem, args) in this.notificationqueue.GetConsumingEnumerable())
                {
                    if (args.NotificationValue is MonitoredItemNotification valueNotification)
                    {
                        try
                        {
                            DataValue dv = valueNotification.Value;
                            pubSubDataStore.WritePublishedDataItem(monitoredItem.ResolvedNodeId, Attributes.Value, dv);
                            Logger.Info($"Updated Value in PubSub DataStore: {monitoredItem.ResolvedNodeId}\t {dv}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Exception handling notification.");
                        }

                    }
                    else
                    {
                        Logger.Info($"Unexpected Notification type: {args.NotificationValue}");
                    }
                }
            });
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
                    Session session = app.OpcUaClient.CheckSession();
                    FieldMetaData meta = new FieldMetaData
                    {
                        Name = variableNode.BrowseName.Name,
                        Description = variableNode.Description,
                        BuiltInType = (byte)TypeInfo.GetBuiltInType(variableNode.DataType, session.TypeTree),
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
    public class Replacement
    {
        public string nsTopic = "";
        public string topic = "";
        public string replaceTopic = "";
        public Replacement(string nsTopic, string topic, string replaceTopic)
        {
            this.nsTopic = nsTopic;
            this.topic = topic;
            this.replaceTopic = replaceTopic;
        }
    }
}
