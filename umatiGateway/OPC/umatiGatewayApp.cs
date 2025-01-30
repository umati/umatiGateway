// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using MQTTnet.Exceptions;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;
using Opc.Ua.Schema.Binary;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace UmatiGateway.OPC
{
    public class UmatiGatewayApp
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public List<UmatiGatewayAppListener> UmatiGatewayAppListeners = new List<UmatiGatewayAppListener>();
        public BlockingTransition blockingTransition = new BlockingTransition("", "", "", false);
        #region Constructors

        public void AddUmatiGatewayAppListener(UmatiGatewayAppListener UmatiGatewayAppListener)
        {
            this.UmatiGatewayAppListeners.Add(UmatiGatewayAppListener);
        }
        public UmatiGatewayApp(ApplicationConfiguration configuration, TextWriter writer, Action<IList, IList> validateResponse)
        {
            this.ConfigureLogging();
            m_validateResponse = validateResponse;
            m_output = writer;
            m_configuration = configuration;
            m_configuration.CertificateValidator.CertificateValidation += CertificateValidation;
            this.TypeDictionaries = new TypeDictionaries(this);
            this.MqttProvider = new MqttProvider(this);

        }
        private void ConfigureLogging()
        {
            LoggingConfiguration config = new LoggingConfiguration();
            ConsoleTarget logconsole = new ConsoleTarget("logconsole")
            {
                Layout = "${longdate} ${uppercase:${level}} ${message}"
            };
            config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logconsole);
            LogManager.Configuration = config;
        }

        public void StartUp()
        {
            Console.WriteLine("Reading Configuration");
            this.configuration = new ConfigurationReader().ReadConfiguration();
            this.opcServerUrl = this.configuration.opcServerEndpoint;
            this.opcUser = this.configuration.opcUser;
            this.opcPwd = this.configuration.opcPassword;
            this.readExtraLibs = this.configuration.readExtraLibs;
            this.MqttProvider.connectionString = this.configuration.mqttServerEndpopint;
            this.MqttProvider.user = this.configuration.mqttUser;
            this.MqttProvider.pwd = this.configuration.mqttPassword;
            this.MqttProvider.clientId = this.configuration.mqttClientId;
            this.MqttProvider.mqttPrefix = this.configuration.mqttPrefix;
            this.MqttProvider.singleThreadPolling = this.configuration.singleThreadPolling;
            this.MqttProvider.PollTimer = this.configuration.pollTime;
            this.opcUser = this.configuration.opcUser;
            this.opcPwd = this.configuration.opcPassword;
            foreach (PublishedNode publishedNode in configuration.publishedNodes)
            {
                this.MqttProvider.publishedNodes.Add(publishedNode);
                //Publish to machine nodes
                MachineNode machineNode = new MachineNode(publishedNode.nodeId, publishedNode.namespaceUrl);
                machineNode.NodeIdType = publishedNode.type;
                machineNode.BaseType = publishedNode.baseType;
                this.MqttProvider.publishedMachines.Add(machineNode);
            }
            // Read Config for Custom DataTypes from here for now.
            this.MqttProvider.customEncodingManager.ReadConfiguration(this.configuration.configFilePath);
            if (this.configuration.autostart == true)
            {
                Console.WriteLine("Create OPC Connection");
                _ = this.ConnectAsync(this.opcServerUrl).Result;
                Console.WriteLine("Create Mqtt Connection");
                this.MqttProvider.Connect();
            }
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// Gets the client session.
        /// </summary>
        public Session? Session => m_session;
        public TypeDictionaries TypeDictionaries;
        public String opcServerUrl = "";
        public bool readExtraLibs = false;
        /// <summary>
        /// Auto accept untrusted certificates.
        /// </summary>
        public bool AutoAccept { get; set; } = true;
        #endregion

        public String opcUser = "";
        public String opcPwd = "";
        public Tree BrowseTree = new Tree();
        public Configuration configuration = new Configuration();
        public Configuration loadedConfiguration = new Configuration();
        public Subscription? subscription = null;
        public List<OpcUaEventListener> opcUaEventListeners = new List<OpcUaEventListener>();

        public MqttProvider MqttProvider;

        public string getOpcConnectionUrl()
        {
            return this.opcServerUrl;
        }
        public void ConnectMqtt()
        {
            this.MqttProvider.Connect();
        }
        public void setMqttPrefix(string MqttPrefix)
        {
            this.MqttProvider.mqttPrefix = MqttPrefix;
        }
        public string getMqttPrefix()
        {
            return this.MqttProvider.mqttPrefix;
        }
        public void setMqttConnectionType(string MqttConnectionType)
        {
            this.MqttProvider.connectionType = MqttConnectionType;
        }
        public string getMqttConnectionType()
        {
            return this.MqttProvider.connectionType;
        }
        public string getMqttConnectionUrl()
        {
            return this.MqttProvider.connectionString;
        }
        public void setMqttConnectionUrl(string MqttConectionUrl)
        {
            this.MqttProvider.connectionString = MqttConectionUrl;
        }
        public string getMqttConnectionPort()
        {
            return this.MqttProvider.connectionPort;
        }
        public void setMqttConnectionPort(string port)
        {
            this.MqttProvider.connectionPort = port;
        }
        public string getMqttUser()
        {
            return this.MqttProvider.user;
        }
        public void setMqttUser(string MqttUser)
        {
            this.MqttProvider.user = MqttUser;
        }
        public string getMqttPassword()
        {
            return this.MqttProvider.pwd;
        }
        public void setMqttPassword(string pwd)
        {
            this.MqttProvider.pwd = pwd;
        }
        public string getMqttClientId()
        {
            return this.MqttProvider.clientId;
        }
        public void setMqttClientId(string MqttClientId)
        {
            this.MqttProvider.clientId = MqttClientId;
        }
        public List<PublishedNode> getPublishedNodes()
        {
            return this.MqttProvider.publishedNodes;
        }
        public void publishNode(NodeId nodeId)
        {
            if (nodeId != null)
            {
                object? identifier = nodeId.Identifier;
                string? stringId = nodeId.Identifier.ToString();
                if (stringId != null)
                {
                    //Publish to normal nodes
                    PublishedNode publishedNode = new PublishedNode();
                    publishedNode.type = nodeId.IdType.ToString();
                    publishedNode.nodeId = stringId;
                    publishedNode.namespaceUrl = this.GetNamespaceTable().GetString(nodeId.NamespaceIndex);
                    this.MqttProvider.publishedNodes.Add(publishedNode);
                    //Publish to machine nodes
                    MachineNode machineNode = new MachineNode(stringId, this.GetNamespaceTable().GetString(nodeId.NamespaceIndex));
                    machineNode.NodeIdType = nodeId.IdType.ToString();
                    this.MqttProvider.publishedMachines.Add(machineNode);
                }
            }
        }



        public void DisconnectMqtt()
        {
            this.MqttProvider.Disconnect();
        }

        #region Public Methods
        /// <summary>
        /// Creates a session with the UA server
        /// </summary>
        public async Task<bool> ConnectAsync(string serverUrl)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            if (!this.blockingTransition.isBlocking)
            {
                this.blockingTransition = new BlockingTransition("Connecting OPC", $"Connecting to {serverUrl}", "", true);
                this.blockingTransitionChange(this.blockingTransition);
                try
                {
                    m_output.WriteLine("Connecting to... {0}", serverUrl);

                    // Get the endpoint by connecting to server's discovery endpoint.
                    // Try to find the first endopint with security.
                    EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(m_configuration, serverUrl, false);
                    EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(m_configuration);
                    ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
                    UserIdentity userIdentity = new UserIdentity();
                    if (!String.IsNullOrWhiteSpace(this.opcUser))
                    {
                        userIdentity = new UserIdentity(this.opcUser, this.opcPwd);
                    }

                    // Create the session
                    Session session = await Session.Create(
                        m_configuration,
                        endpoint,
                        false,
                        false,
                        m_configuration.ApplicationName,
                        30 * 60 * 1000,
                        userIdentity,
                        //null,
                        null
                    );

                    // Assign the created session
                    if (session != null && session.Connected)
                    {

                        m_session = session;


                        // Session created successfully.
                        m_output.WriteLine("New Session Created with SessionName = {0}", m_session.SessionName);

                        this.TypeDictionaries = new TypeDictionaries(this);
                        this.TypeDictionaries.ReadExtraLibs = this.readExtraLibs;
                        this.blockingTransition.Message = "Read Type Dictionaries";
                        this.blockingTransition.Detail = "Read Binaries";
                        this.blockingTransitionChange(this.blockingTransition);
                        //this.TypeDictionaries.ReadTypeDictionary(false);
                        Console.WriteLine("Read Binaries");
                        this.TypeDictionaries.ReadOpcBinary();
                        this.blockingTransition.Detail = "Read DataTypes";
                        this.blockingTransitionChange(this.blockingTransition);
                        Console.WriteLine("Read DataTypes");
                        this.TypeDictionaries.ReadDataTypes();
                        this.blockingTransition.Detail = "Read EventTypes";
                        this.blockingTransitionChange(this.blockingTransition);
                        Console.WriteLine("Read EventTypes");
                        this.TypeDictionaries.ReadEventTypes();
                        this.blockingTransition.Detail = "Read InterfaceTypes";
                        this.blockingTransitionChange(this.blockingTransition);
                        Console.WriteLine("Read InterfaceTypes");
                        this.TypeDictionaries.ReadInterfaceTypes();
                        this.blockingTransition.Detail = "Read ObjectTypes";
                        this.blockingTransitionChange(this.blockingTransition);
                        Console.WriteLine("Read ObjectTypes");
                        this.TypeDictionaries.ReadObjectTypes();
                        this.blockingTransition.Detail = "Read ReferenceTypes";
                        this.blockingTransitionChange(this.blockingTransition);
                        Console.WriteLine("Read ReferenceTypes");
                        this.TypeDictionaries.ReadReferenceTypes();
                        this.blockingTransition.Detail = "Read VariableTypes";
                        this.blockingTransitionChange(this.blockingTransition);
                        Console.WriteLine("Read VariableTypes");
                        this.TypeDictionaries.ReadVariableTypes();
                        return true;
                    }
                    else
                    {
                        Logger.Info("Unable to Create OPC Session.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    // Log Error
                    m_output.WriteLine("Create Session Error : {0}", ex.Message);
                    return false;
                }
                finally
                {
                    this.blockingTransition = new BlockingTransition();
                }
            }
            else
            {
                Console.WriteLine("Allready trying to Connect to OPC Server");
                return false;
            }
        }
        private void CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
        {
            bool certificateAccepted = false;

            // ****
            // Implement a custom logic to decide if the certificate should be
            // accepted or not and set certificateAccepted flag accordingly.
            // The certificate can be retrieved from the e.Certificate field
            // ***

            ServiceResult error = e.Error;
            m_output.WriteLine(error);
            if (error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted && AutoAccept)
            {
                certificateAccepted = true;
            }

            if (certificateAccepted)
            {
                m_output.WriteLine("Untrusted Certificate accepted. Subject = {0}", e.Certificate.Subject);
                e.Accept = true;
            }
            else
            {
                m_output.WriteLine("Untrusted Certificate rejected. Subject = {0}", e.Certificate.Subject);
            }
        }

        /// <summary>
        /// Disconnects the session.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                //NodeId nodeId = new NodeId((uint)5, (ushort)8);
                //this.decodeComplexType(nodeId);
                if (m_session != null)
                {
                    m_output.WriteLine("Disconnecting...");

                    m_session.Close();
                    m_session.Dispose();
                    m_session = null;

                    // Log Session Disconnected event
                    m_output.WriteLine("Session Disconnected.");
                }
                else
                {
                    m_output.WriteLine("Session not created!");
                }
            }
            catch (Exception ex)
            {
                // Log Error
                m_output.WriteLine($"Disconnect Error : {ex.Message}");
            }
        }

        public Node? ReadNode(NodeId nodeId)
        {
            Node? node = null;
            if (m_session != null && m_session.Connected)
            {
                try
                {
                    node = m_session.ReadNode(nodeId);
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine(e.Message + " NodeId:" + nodeId);
                }
            }
            return node;
        }
        public void ConnectEvents(OpcUaEventListener opcUaEventListener)
        {
            if (this.Session != null)
            {
                this.opcUaEventListeners.Add(opcUaEventListener);
                Subscription subscription = new Subscription(this.Session.DefaultSubscription);
                subscription.DisplayName = "ModelChangeEventSubscription";
                subscription.PublishingInterval = 1000;
                MonitoredItem eventMonitoredItem = new MonitoredItem(subscription.DefaultItem);
                eventMonitoredItem.StartNodeId = ObjectIds.Server;
                eventMonitoredItem.AttributeId = Attributes.EventNotifier;
                eventMonitoredItem.MonitoringMode = MonitoringMode.Reporting;

                EventFilter filter = new EventFilter();
                filter.AddSelectClause(ObjectTypeIds.BaseEventType, BrowseNames.Changes);
                filter.AddSelectClause(ObjectTypeIds.BaseEventType, BrowseNames.EventType);
                filter.AddSelectClause(ObjectTypeIds.BaseEventType, BrowseNames.SourceNode);

                eventMonitoredItem.Filter = filter;

                eventMonitoredItem.Notification += HandleEventNotification;
                subscription.AddItem(eventMonitoredItem);
                this.Session.AddSubscription(subscription);
                subscription.Create();
            }

        }
        public void HandleEventNotification(MonitoredItem item, MonitoredItemNotificationEventArgs notification)
        {
            try
            {
                if (notification.NotificationValue is EventFieldList)
                {
                    EventFieldList efl = (EventFieldList)notification.NotificationValue;
                    VariantCollection vc = efl.EventFields;
                    foreach (Variant var in vc)
                    {
                        if (var.TypeInfo.ToString() == "ExtensionObject[]")
                        {
                            ExtensionObject[] etos = (ExtensionObject[])var.Value;
                            foreach (ExtensionObject eto in etos)
                            {
                                Object body = eto.Body;
                                if (body is ModelChangeStructureDataType)
                                {
                                    ModelChangeStructureDataType mcs = (ModelChangeStructureDataType)body;
                                    if (mcs.Affected != null)
                                    {
                                        Console.WriteLine($"Affected: {mcs.Affected}");
                                        Console.WriteLine($"AffectedType: {mcs.AffectedType}");
                                        Console.WriteLine($"Verb: {mcs.Verb}");
                                        foreach (OpcUaEventListener listener in this.opcUaEventListeners)
                                        {
                                            listener.ModelChangeEvent(mcs.Affected);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnMonitoredItemNotification error: {ex.Message}");
            }
        }
        public List<NodeId> BrowseLocalNodeIds(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes)
        {
            List<NodeId> nodeList = new List<NodeId>();
            BrowseResultCollection browseResults = BrowseNode(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            foreach (BrowseResult browseResult in browseResults)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    nodeList.Add(new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex));
                }
            }
            return nodeList;
        }
        public NodeId? BrowseLocalNodeIdWithBrowseName(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, QualifiedName browseName)
        {
            BrowseResultCollection browseResults = BrowseNode(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            foreach (BrowseResult browseResult in browseResults)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {

                    NodeId innerNodeId = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                    Node? node = this.ReadNode(innerNodeId);
                    if (node != null)
                    {
                        if (node.BrowseName == browseName)
                        {
                            return innerNodeId;
                        }
                    }
                }
            }
            return null;
        }

        public NamespaceTable GetNamespaceTable()
        {
            if (m_session != null)
            {
                DataValue dv = m_session.ReadValue(VariableIds.Server_NamespaceArray);
                String[] namespaces = (String[])dv.Value;
                return new NamespaceTable(namespaces);
            }
            else
            {
                Logger.Error("Unable to Get NameSpaceTable! Sessions is not Connected");
            }
            return new NamespaceTable();
        }
        public List<NodeId> BrowseLocalNodeIdsWithTypeDefinition(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId expectedTypeDefinition)
        {
            List<NodeId> filteredNodeIds = new List<NodeId>();
            List<NodeId> nodeIds = BrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            foreach (NodeId nodeId in nodeIds)
            {
                NodeId? typeDefinition = BrowseTypeDefinition(nodeId);
                if (typeDefinition != null)
                {
                    if (typeDefinition == expectedTypeDefinition)
                    {
                        filteredNodeIds.Add(nodeId);
                    }
                }
            }
            return filteredNodeIds;
        }
        public List<NodeId> BrowseLocalNodeIds(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId expectedTypeDefinition)
        {
            List<NodeId> filteredNodeIds = new List<NodeId>();
            List<NodeId> nodeIds = BrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            return nodeIds;
        }
        public NodeId? BrowseTypeDefinition(NodeId nodeId)
        {
            NodeId? typeDefinition = null;
            BrowseResultCollection browseResults = BrowseNode(nodeId, BrowseDirection.Forward, (uint)NodeClass.ObjectType | (uint)NodeClass.VariableType, ReferenceTypes.HasTypeDefinition, false);
            foreach (BrowseResult browseResult in browseResults)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    typeDefinition = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                    break;
                }
            }
            return typeDefinition;
        }
        public List<NodeId> BrowseSubTypes(NodeId nodeId)
        {
            List<NodeId> subTypes = new List<NodeId>();
            BrowseResultCollection browseResults = BrowseNode(nodeId, BrowseDirection.Forward, (uint)NodeClass.ObjectType | (uint)NodeClass.VariableType, ReferenceTypes.HasSubtype, false);
            foreach (BrowseResult browseResult in browseResults)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    subTypes.Add(new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex));
                }
            }
            return subTypes;
        }
        public List<NodeId> BrowseAllHierarchicalSubType(NodeId nodeId, List<NodeId> subTypeList)
        {
            BrowseResultCollection browseResults = BrowseNode(nodeId, BrowseDirection.Forward, (uint)NodeClass.ObjectType | (uint)NodeClass.VariableType, ReferenceTypes.HasSubtype, false);
            foreach (BrowseResult browseResult in browseResults)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    NodeId subTypeId = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                    this.BrowseAllHierarchicalSubType(subTypeId, subTypeList);
                    subTypeList.Add(subTypeId);
                }
            }
            return subTypeList;
        }
        public List<NodeId> GetOptionalAndMandatoryPlaceholders(NodeId typeDefinition)
        {
            List<NodeId> nodeIds = new List<NodeId>();
            //Look for the Childs
            BrowseResultCollection browseResultCollection = BrowseNode(typeDefinition, BrowseDirection.Forward, 0, ReferenceTypeIds.HasComponent, true);
            foreach (BrowseResult browseResult in browseResultCollection)
            {
                BrowseDescriptionCollection browseDescriptions = new BrowseDescriptionCollection();
                foreach (ReferenceDescription reference in browseResult.References)
                {
                    NodeId nodeId = (NodeId)reference.NodeId;
                    browseDescriptions.Add(BrowseUtils.ModellingRuleBrowseDescription(nodeId));
                }
                if (browseDescriptions.Count > 0)
                {
                    BrowseResultCollection modelRuleCollection = Browse(browseDescriptions);
                    for (int i = 0; i < modelRuleCollection.Count; i++)
                    {
                        BrowseResult modelRuleBrowseResult = modelRuleCollection[i];
                        foreach (ReferenceDescription modelRuleReference in modelRuleBrowseResult.References)
                        {
                            NodeId nodeId = (NodeId)modelRuleReference.NodeId;
                            if (nodeId == ObjectIds.ModellingRule_MandatoryPlaceholder || nodeId == ObjectIds.ModellingRule_OptionalPlaceholder)
                            {
                                nodeIds.Add(browseDescriptions[i].NodeId);
                            }
                        }
                    }
                }
            }
            return nodeIds;
        }
        public NodeId? BrowseLocalNodeId(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes)
        {

            List<NodeId> nodeIds = BrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
            foreach (NodeId nodeId in nodeIds)
            {
                return nodeId;
            }
            return null;
        }

        public BrowseResultCollection Browse(BrowseDescription browseDescription)
        {
            if (m_session != null)
            {
                BrowseDescriptionCollection browseDescriptionCollection = new BrowseDescriptionCollection(new BrowseDescription[] { browseDescription });
                m_session.Browse(null, null, 10000, browseDescriptionCollection, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                return results;
            }
            else
            {
                throw new SystemException("Session was not Connected!");
            }
        }
        public BrowseResultCollection Browse(BrowseDescriptionCollection browseDescriptionCollection)
        {
            if (m_session != null)
            {
                m_session.Browse(null, null, 10000, browseDescriptionCollection, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                return results;
            }
            else
            {
                throw new SystemException("Session was not Connected");
            }
        }
        public List<ExpandedNodeId> BrowseNodeId(BrowseDescription browseDescription, int? filter = null)
        {
            List<ExpandedNodeId> nodes = new List<ExpandedNodeId>();
            BrowseResultCollection browseResultCollection = this.Browse(browseDescription);
            foreach (BrowseResult browseResult in browseResultCollection)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    nodes.Add(reference.NodeId);
                }
            }
            return nodes;
        }

        public BrowseResultCollection BrowseNode(NodeId nodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, Boolean includeSubTypes)
        {
            if (m_session != null)
            {
                BrowseDescription nodeToBrowse = new BrowseDescription();
                nodeToBrowse.NodeId = nodeId;
                nodeToBrowse.BrowseDirection = browseDirection;
                nodeToBrowse.NodeClassMask = nodeClassMask;
                nodeToBrowse.ReferenceTypeId = referenceTypeIds;
                nodeToBrowse.IncludeSubtypes = includeSubTypes;

                BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                nodesToBrowse.Add(nodeToBrowse);
                m_session.Browse(null, null, 10000, nodesToBrowse, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                return results;
            }
            else
            {
                throw new SystemException("Session not Connected");
            }
        }

        public void BrowseRootNode()
        {
            if (!this.BrowseTree.Initialized)
            {
                Node? node = this.ReadNode(ObjectIds.RootFolder);
                if (node != null)
                {
                    NodeData nodeData = new NodeData(node);
                    TreeNode treeNode = new TreeNode(nodeData);
                    this.BrowseTree.children.AddLast(treeNode);
                    this.BrowseTree.uids.Add(treeNode.uid, treeNode);
                    this.BrowseTree.Initialized = true;
                    if (node.NodeClass == NodeClass.Variable)
                    {
                        nodeData.DataValue = this.decodeComplexType(node.NodeId);
                    }
                }
            }

        }
        private JObject decodeComplexType(NodeId nodeId)
        {
            JObject jObject = new JObject();

            Node? node = this.ReadNode(nodeId);
            DataValue? dv = this.ReadValue(nodeId);
            if (dv != null)
            {
                if (dv.Value is ExtensionObject eto)
                {

                }
            }
            return jObject;
        }
        public ExpandedNodeId getIndexedNodeId(ExpandedNodeId expandedNodeId)
        {
            if (expandedNodeId.IsAbsolute)
            {
                return new ExpandedNodeId(expandedNodeId.Identifier, (ushort)(this.GetNamespaceTable().GetIndex(expandedNodeId.NamespaceUri)), expandedNodeId.NamespaceUri, expandedNodeId.ServerIndex);
            }
            else
            {
                return expandedNodeId;
            }
        }
        public void BrowseSelectedTreeNode(TreeNode TreeNode)
        {
            if (m_session != null)
            {
                BrowseDescription nodeToBrowse = new BrowseDescription();
                nodeToBrowse.NodeId = TreeNode.NodeData.node.NodeId;
                nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
                nodeToBrowse.NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable | (int)NodeClass.Method | (int)NodeClass.ObjectType | (int)NodeClass.VariableType | (int)NodeClass.DataType | (int)NodeClass.ReferenceType; ;
                nodeToBrowse.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences;
                nodeToBrowse.IncludeSubtypes = true;
                nodeToBrowse.ResultMask = (int)BrowseResultMask.All;

                BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                nodesToBrowse.Add(nodeToBrowse);
                m_session.Browse(null, null, 100, nodesToBrowse, out BrowseResultCollection browseResults, out DiagnosticInfoCollection diagnosticInfos);
                foreach (BrowseResult browseResult in browseResults)
                {
                    ReferenceDescriptionCollection references = browseResult.References;
                    foreach (ReferenceDescription reference in references)
                    {
                        NodeId nodeId = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                        Node? node = this.ReadNode(nodeId);
                        if (node != null)
                        {
                            NodeData nodeData = new NodeData(node);
                            TreeNode treeNode = new TreeNode(nodeData);
                            TreeNode.children.AddLast(treeNode);
                            this.BrowseTree.uids.Add(treeNode.uid, treeNode);
                            if (node.NodeClass == NodeClass.Variable)
                            {
                                nodeData.DataValue = this.decodeComplexType(node.NodeId);
                            }
                        }
                    }
                }
            }
            else
            {
                throw new SystemException("Session not Connected");
            }
        }
        public DataValue? ReadValue(NodeId nodeId)
        {
            if (m_session != null)
            {
                return m_session.ReadValue(nodeId);
            }
            else
            {
                throw new SystemException("Session Not Connected");
            }

        }

        /// <summary>
        /// Create Subscription and MonitoredItems for DataChanges
        /// </summary>
        public uint SubscribeToDataChanges(NodeId nodeId, MonitoredItemNotificationEventHandler eventHandler)
        {
            uint subscriptionId = 0;
            if (m_session == null || m_session.Connected == false)
            {
                m_output.WriteLine("Session not connected!");
                return 0;
            }

            try
            {
                // Create a subscription for receiving data change notifications

                // Define Subscription parameters
                if (subscription == null)
                {
                    subscription = new Subscription(m_session.DefaultSubscription);
                    subscription.DisplayName = "Subscription for NodeIds";
                    subscription.PublishingEnabled = true;
                    subscription.PublishingInterval = 1000;
                    m_session.AddSubscription(subscription);
                    // Create the subscription on Server side
                    subscription.Create();
                    subscriptionId = subscription.Id;
                }

                m_output.WriteLine("New Subscription created with SubscriptionId = {0}.", subscription.Id);

                // Create MonitoredItems for data changes (Reference Server)

                MonitoredItem intMonitoredItem = new MonitoredItem(subscription.DefaultItem);
                // Int32 Node - Objects\CTT\Scalar\Simulation\Int32
                intMonitoredItem.StartNodeId = nodeId;
                intMonitoredItem.AttributeId = Attributes.Value;
                intMonitoredItem.DisplayName = "Subscription";
                intMonitoredItem.SamplingInterval = 1000;
                intMonitoredItem.Notification += eventHandler;

                subscription.AddItem(intMonitoredItem);

                // Create the monitored items on Server side
                subscription.ApplyChanges();
                m_output.WriteLine("MonitoredItems created for SubscriptionId = {0} with NodeId {1}.", subscription.Id, nodeId);
            }
            catch (Exception ex)
            {
                m_output.WriteLine("Subscribe error: {0}", ex.Message);
            }
            return subscriptionId;
        }
        #endregion

        #region Private Methods

        private bool checkSession()
        {
            if (m_session == null || m_session.Connected == false)
            {
                m_output.WriteLine("Session not connected!");
                return false;
            }
            else
            {
                return true;
            }
        }
        #endregion

        #region Private Fields
        private ApplicationConfiguration m_configuration;
        private Session? m_session;
        private readonly TextWriter m_output;
        private readonly Action<IList, IList> m_validateResponse;
        #endregion
        private void blockingTransitionChange(BlockingTransition blockingTransition)
        {
            foreach (UmatiGatewayAppListener UmatiGatewayAppListener in this.UmatiGatewayAppListeners)
            {
                try
                {
                    UmatiGatewayAppListener.blockingTransitionChanged(blockingTransition);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to notify Listener: {ex.StackTrace}");
                }
            }
        }
    }
}