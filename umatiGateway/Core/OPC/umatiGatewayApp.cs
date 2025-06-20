// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;
using Opc.Ua;
using Opc.Ua.Client;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.Mqtt;
using umatiGateway.Core.PubSub;

namespace umatiGateway.Core.OPC
{
    public class UmatiGatewayApp
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public PubSubProvider PubSubProvider { get; set; }
        public MqttProvider MqttProvider;
        public Session? Session => m_session;
        public TypeDictionaries TypeDictionaries;
        public string opcServerUrl = "";
        public bool readExtraLibs = false;
        public bool AutoAccept { get; set; } = true;
        public string opcUser = "";
        public string opcPwd = "";
        public Tree BrowseTree = new Tree();
        public UmatiConfiguration ActiveConfiguration { get; set; } = new UmatiConfiguration();
        public UmatiConfiguration loadedConfiguration = new UmatiConfiguration();
        public Subscription? subscription = null;
        public List<OpcUaEventListener> opcUaEventListeners = new List<OpcUaEventListener>();
        
        private ApplicationConfiguration m_configuration;
        private Session? m_session;
        private readonly TextWriter m_output;
        private readonly Action<IList, IList> m_validateResponse;

        public List<UmatiGatewayAppListener> UmatiGatewayAppListeners = new List<UmatiGatewayAppListener>();
        public BlockingTransition blockingTransition = new BlockingTransition("", "", "", false);
        public void AddUmatiGatewayAppListener(UmatiGatewayAppListener UmatiGatewayAppListener)
        {
            UmatiGatewayAppListeners.Add(UmatiGatewayAppListener);
        }
        public UmatiGatewayApp(ApplicationConfiguration configuration, TextWriter writer, Action<IList, IList> validateResponse)
        {
            ConfigureLogging();
            ActiveConfiguration = new UmatiConfigurationManager().ReadConfiguration();
            Logger.Info("Reconfiger Logger");
            Logger.Info("Reading Configuration");
            ConfigureLogging();
            m_validateResponse = validateResponse;
            m_output = writer;
            m_configuration = configuration;
            m_configuration.CertificateValidator.CertificateValidation += CertificateValidation;
            TypeDictionaries = new TypeDictionaries(this);
            MqttProvider = new MqttProvider(this);
            PubSubProvider = new PubSubProvider(this);

        }
        private void ConfigureLogging()
        {
            LoggingConfiguration config = new LoggingConfiguration();
            ConsoleTarget logconsole = new ConsoleTarget("logconsole")
            {
                Layout = "${uppercase:${level}} ${message}"
            };
            string logfilePath = Path.Combine(AppContext.BaseDirectory, "umatiGateway.log");
            if (File.Exists(logfilePath))
            {
                File.Delete(logfilePath);
            }
            FileTarget logfile = new FileTarget("logfile")
            {
                FileName = logfilePath,
                Layout = "${uppercase:${level}} ${message}",
            };

            string logLevelstring = ActiveConfiguration.LogLevel ?? "";
            NLog.LogLevel logLevel = NLog.LogLevel.Info;
            switch (logLevelstring)
            {
                case "Trace": logLevel = NLog.LogLevel.Trace; break;
                case "Debug": logLevel = NLog.LogLevel.Debug; break;
                case "Info": logLevel = NLog.LogLevel.Info; break;
                case "Warn": logLevel = NLog.LogLevel.Warn; break;
                case "Error": logLevel = NLog.LogLevel.Error; break;
                default: logLevel = NLog.LogLevel.Info; break;
            }
            config.AddRule(logLevel, NLog.LogLevel.Fatal, logconsole);
            config.AddRule(logLevel, NLog.LogLevel.Fatal, logfile);
            LogManager.Configuration = config;
        }

        public void StartUp()
        {
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Logger.Info("umatiGateway Version: {Version}", version);
            opcServerUrl = ActiveConfiguration.OPCConnection.ServerEndpoint;
            opcUser = ActiveConfiguration.OPCConnection.UserName;
            opcPwd = ActiveConfiguration.OPCConnection.Password;

            readExtraLibs = ActiveConfiguration.OPCConnection.ReadExtraLibs;

            //this.MqttProvider.customEncodingManager.;
            StartConfiguration startConfiguration = ActiveConfiguration.StartConfiguration;
            if(startConfiguration.StartOPCConnection)
            {
                Logger.Info("Create OPC Connection");
                _ = ConnectAsync(opcServerUrl).Result;
            }
            if(startConfiguration.StartMQTTProvider)
            {
                Logger.Info("Create Mqtt Connection");
                MqttProvider.Connect();
            }
        }

        /// <summary>
        /// Creates a session with the UA server
        /// </summary>
        public async Task<bool> ConnectAsync(string serverUrl)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            if (!blockingTransition.isBlocking)
            {
                blockingTransition = new BlockingTransition("Connecting OPC", $"Connecting to {serverUrl}", "", true);
                blockingTransitionChange(blockingTransition);
                try
                {
                    Logger.Info("Connecting to... {0}", serverUrl);

                    // Get the endpoint by connecting to server's discovery endpoint.
                    // Try to find the first endopint with security.
                    EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(m_configuration, serverUrl, false);
                    EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(m_configuration);
                    ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
                    UserIdentity userIdentity = new UserIdentity();
                    if (!string.IsNullOrWhiteSpace(ActiveConfiguration.OPCConnection.UserName))
                    {
                        userIdentity = new UserIdentity(ActiveConfiguration.OPCConnection.UserName, ActiveConfiguration.OPCConnection.Password);
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
                        null
                    );

                    // Assign the created session
                    if (session != null && session.Connected)
                    {

                        m_session = session;


                        // Session created successfully.
                        Logger.Info("New Session Created with SessionName = {0}", m_session.SessionName);

                        TypeDictionaries = new TypeDictionaries(this);
                        TypeDictionaries.ReadExtraLibs = readExtraLibs;
                        blockingTransition.Message = "Read Type Dictionaries";
                        blockingTransition.Detail = "Read Binaries";
                        blockingTransitionChange(blockingTransition);
                        //this.TypeDictionaries.ReadTypeDictionary(false);
                        Logger.Info("Read Binaries");
                        TypeDictionaries.ReadOpcBinary();
                        blockingTransition.Detail = "Read DataTypes";
                        blockingTransitionChange(blockingTransition);
                        Logger.Info("Read DataTypes");
                        TypeDictionaries.ReadDataTypes();
                        blockingTransition.Detail = "Read EventTypes";
                        blockingTransitionChange(blockingTransition);
                        Logger.Info("Read EventTypes");
                        TypeDictionaries.ReadEventTypes();
                        blockingTransition.Detail = "Read InterfaceTypes";
                        blockingTransitionChange(blockingTransition);
                        Logger.Info("Read InterfaceTypes");
                        TypeDictionaries.ReadInterfaceTypes();
                        blockingTransition.Detail = "Read ObjectTypes";
                        blockingTransitionChange(blockingTransition);
                        Logger.Info("Read ObjectTypes");
                        TypeDictionaries.ReadObjectTypes();
                        blockingTransition.Detail = "Read ReferenceTypes";
                        blockingTransitionChange(blockingTransition);
                        Logger.Info("Read ReferenceTypes");
                        TypeDictionaries.ReadReferenceTypes();
                        blockingTransition.Detail = "Read VariableTypes";
                        blockingTransitionChange(blockingTransition);
                        Logger.Info("Read VariableTypes");
                        TypeDictionaries.ReadVariableTypes();
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
                    Logger.Error($"Create Session Error : {ex.Message}");
                    m_session = null;
                    return false;
                }
                finally
                {
                    blockingTransition = new BlockingTransition();
                }
            }
            else
            {
                Logger.Info("Allready trying to Connect to OPC Server");
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

        public void Disconnect()
        {
            try
            {
                if (m_session != null)
                {
                    m_output.WriteLine("Disconnecting...");
                    m_session.Close();
                    m_session.Dispose();
                    m_session = null;
                    m_output.WriteLine("Session Disconnected.");
                }
                else
                {
                    m_output.WriteLine("Session not created!");
                }
            }
            catch (Exception ex)
            {
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
            if (Session != null)
            {
                opcUaEventListeners.Add(opcUaEventListener);
                Subscription subscription = new Subscription(Session.DefaultSubscription);
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
                Session.AddSubscription(subscription);
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
                        if (var.Value is NodeId)
                        {
                            NodeId nodeId = (NodeId)var.Value;
                            if (nodeId.IdType == IdType.Numeric && (uint)nodeId.Identifier == 1002)
                            {
                                Logger.Info("ResultReady Event received");
                                foreach (OpcUaEventListener listener in opcUaEventListeners)
                                {
                                    listener.ResultReadyEvent();
                                }
                                return;
                            }
                        }
                        if (var.TypeInfo.ToString() == "ExtensionObject[]")
                        {
                            ExtensionObject[] etos = (ExtensionObject[])var.Value;
                            foreach (ExtensionObject eto in etos)
                            {
                                object body = eto.Body;
                                if (body is ModelChangeStructureDataType)
                                {
                                    ModelChangeStructureDataType mcs = (ModelChangeStructureDataType)body;
                                    if (mcs.Affected != null)
                                    {
                                        Logger.Info($"Affected: {mcs.Affected}");
                                        Logger.Info($"AffectedType: {mcs.AffectedType}");
                                        Logger.Info($"Verb: {mcs.Verb}");
                                        foreach (OpcUaEventListener listener in opcUaEventListeners)
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
                Logger.Info($"OnMonitoredItemNotification error: {ex.Message}");
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
        public List<NodeId> BrowseLocalNodeIdsExcludeReference(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId excludedReferenceTypeId)
        {
            List<NodeId> nodeList = new List<NodeId>();
            BrowseResultCollection browseResults = BrowseNode(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes, excludedReferenceTypeId);
            if (browseResults.Count == 2)
            {
                BrowseResult allNodesResult = browseResults[0];
                ReferenceDescriptionCollection allReferences = allNodesResult.References;
                foreach (ReferenceDescription reference in allReferences)
                {
                    nodeList.Add(new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex));
                }
                BrowseResult excludedNodesResult = browseResults[1];
                ReferenceDescriptionCollection excludedReferences = excludedNodesResult.References;
                foreach (ReferenceDescription excludedReference in excludedReferences)
                {
                    nodeList.Remove(new NodeId(excludedReference.NodeId.Identifier, excludedReference.NodeId.NamespaceIndex));
                }
            }
            else
            {
                Logger.Error("Number of BrowseResults does not match 2.");
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
                    Node? node = ReadNode(innerNodeId);
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
                string[] namespaces = (string[])dv.Value;
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
                    BrowseAllHierarchicalSubType(subTypeId, subTypeList);
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
        public List<TypeClassNode> GetTypeClassNodesForNodeId(NodeId nodeId, RelativePathElementCollection? pathToChild = null)
        {
            List<TypeClassNode> typeClassNodes = new List<TypeClassNode>();
            Node? node = ReadNode(nodeId);
            if (node != null)
            {
                RelativePath relativePath = new RelativePath(node.BrowseName);
                if (pathToChild != null)
                {
                    relativePath.Elements.AddRange(pathToChild);
                }
                List<NodeId> parentNodeIds = BrowseLocalNodeIds(nodeId, BrowseDirection.Inverse, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true);
                foreach (NodeId parentNodeId in parentNodeIds)
                {
                    NodeId? parentTypeDefinition = BrowseTypeDefinition(parentNodeId);
                    if (parentTypeDefinition != null)
                    {
                        typeClassNodes.Add(new TypeClassNode(parentTypeDefinition, relativePath.Elements));
                    }
                    List<TypeClassNode> childDictionary = GetTypeClassNodesForNodeId(parentNodeId, relativePath.Elements);
                    foreach (TypeClassNode typeclassnode in childDictionary)
                    {
                        typeClassNodes.Add(typeclassnode);
                    }
                }
            }
            return typeClassNodes;
        }
        public List<NodeId> GetOptionalAndMandatoryPlaceholdersForTypeClassNodes(List<TypeClassNode> typeClassNodes)
        {
            List<NodeId> typeClassesFromParent = new List<NodeId>();
            BrowsePathCollection browsePathCollection = new BrowsePathCollection();
            foreach (TypeClassNode typeClassNode in typeClassNodes)
            {
                BrowsePath browsePath = new BrowsePath();
                browsePath.StartingNode = typeClassNode.StartNodeId;
                RelativePath relativePath = new RelativePath();
                relativePath.Elements = typeClassNode.RelativePathElements;
                browsePath.RelativePath = relativePath;
                browsePathCollection.Add(browsePath);
            }
            if (browsePathCollection.Count > 0 && m_session != null)
            {
                if (m_session != null)
                {
                    m_session.TranslateBrowsePathsToNodeIds(null, browsePathCollection, out BrowsePathResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                    foreach (BrowsePathResult result in results)
                    {
                        if (StatusCode.IsGood(result.StatusCode))
                        {
                            if (result.Targets.Count > 0)
                            {
                                NodeId nodeId = ExpandedNodeId.ToNodeId(result.Targets[0].TargetId, GetNamespaceTable());
                                typeClassesFromParent.Add(nodeId);
                            }
                        }
                    }
                }
            }
            return typeClassesFromParent;
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
            BrowseResultCollection browseResultCollection = Browse(browseDescription);
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

        public BrowseResultCollection BrowseNode(NodeId nodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes)
        {
            if (m_session != null)
            {
                BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                BrowseDescription nodeToBrowse = new BrowseDescription();
                nodeToBrowse.NodeId = nodeId;
                nodeToBrowse.BrowseDirection = browseDirection;
                nodeToBrowse.NodeClassMask = nodeClassMask;
                nodeToBrowse.ReferenceTypeId = referenceTypeIds;
                nodeToBrowse.IncludeSubtypes = includeSubTypes;
                nodesToBrowse.Add(nodeToBrowse);
                m_session.Browse(null, null, 10000, nodesToBrowse, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                return results;
            }
            else
            {
                throw new SystemException("Session not Connected");
            }
        }
        public BrowseResultCollection BrowseNode(NodeId nodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId excludedReferenceTypeId)
        {
            if (m_session != null)
            {
                BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                BrowseDescription nodeToBrowse = new BrowseDescription();
                nodeToBrowse.NodeId = nodeId;
                nodeToBrowse.BrowseDirection = browseDirection;
                nodeToBrowse.NodeClassMask = nodeClassMask;
                nodeToBrowse.ReferenceTypeId = referenceTypeIds;
                nodeToBrowse.IncludeSubtypes = includeSubTypes;
                nodesToBrowse.Add(nodeToBrowse);
                BrowseDescription excludedNodeToBrowse = new BrowseDescription();
                excludedNodeToBrowse.NodeId = nodeId;
                excludedNodeToBrowse.BrowseDirection = browseDirection;
                excludedNodeToBrowse.NodeClassMask = nodeClassMask;
                excludedNodeToBrowse.ReferenceTypeId = excludedReferenceTypeId;
                excludedNodeToBrowse.IncludeSubtypes = true;
                nodesToBrowse.Add(excludedNodeToBrowse);
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
            if (!BrowseTree.Initialized)
            {
                Node? node = ReadNode(ObjectIds.RootFolder);
                if (node != null)
                {
                    NodeData nodeData = new NodeData(node);
                    TreeNode treeNode = new TreeNode(nodeData);
                    BrowseTree.children.AddLast(treeNode);
                    BrowseTree.uids.Add(treeNode.uid, treeNode);
                    BrowseTree.Initialized = true;
                    if (node.NodeClass == NodeClass.Variable)
                    {
                        //nodeData.DataValue = decodeComplexType(node.NodeId);
                    }
                }
            }

        }
        private JObject decodeComplexType(NodeId nodeId)
        {
            JObject jObject = new JObject();

            Node? node = ReadNode(nodeId);
            DataValue? dv = ReadValue(nodeId);
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
                return new ExpandedNodeId(expandedNodeId.Identifier, (ushort)GetNamespaceTable().GetIndex(expandedNodeId.NamespaceUri), expandedNodeId.NamespaceUri, expandedNodeId.ServerIndex);
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
                        Node? node = ReadNode(nodeId);
                        if (node != null)
                        {
                            NodeData nodeData = new NodeData(node);
                            TreeNode treeNode = new TreeNode(nodeData);
                            TreeNode.children.AddLast(treeNode);
                            BrowseTree.uids.Add(treeNode.uid, treeNode);
                            /*if (node.NodeClass == NodeClass.Variable)
                            {
                                nodeData.DataValue = decodeComplexType(node.NodeId);
                            }*/
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
        public uint SubscribeToDataChanges(List<NodeId> nodeIds, MonitoredItemNotificationEventHandler eventHandler)
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

                m_output.WriteLine($"New Subscription created with SubscriptionId = {subscriptionId}.");

                // Create MonitoredItems for data changes (Reference Server)
                foreach (NodeId nodeId in nodeIds)
                {
                    MonitoredItem intMonitoredItem = new MonitoredItem(subscription.DefaultItem);
                    // Int32 Node - Objects\CTT\Scalar\Simulation\Int32
                    intMonitoredItem.StartNodeId = nodeId;
                    intMonitoredItem.AttributeId = Attributes.Value;
                    intMonitoredItem.DisplayName = "Subscription";
                    intMonitoredItem.SamplingInterval = 1000;
                    intMonitoredItem.Notification += eventHandler;

                    subscription.AddItem(intMonitoredItem);
                }
                // Create the monitored items on Server side
                subscription.ApplyChanges();
                m_output.WriteLine("MonitoredItems created for SubscriptionId = {0}");
            }
            catch (Exception ex)
            {
                m_output.WriteLine("Subscribe error: {0}", ex.Message);
            }
            return subscriptionId;
        }

        public void AddNodeUmatiMqttConfig(NodeId nodeId)
        {
            
            string? namespaceUrl = this.GetNamespaceTable().GetString(nodeId.NamespaceIndex);
            string? identifier = nodeId.Identifier.ToString();
            if (namespaceUrl != null && identifier != null)
            {
                PublishedNode publishedNode = new PublishedNode();
                publishedNode.NamespaceUrl = namespaceUrl;
                publishedNode.Type = nodeId.IdType.ToString();
                publishedNode.NodeId = identifier;
                publishedNode.BaseType = "";
                this.ActiveConfiguration.MqttProviderConfig.PublishedNodes.Add(publishedNode);
            }
            else
            {
                Logger.Error($"Unable to create PublishedNode from NodeId: {nodeId}");
            }
        }
        public void RemoveNodeMqttConfig(PublishedNode publishedNode)
        {
            this.ActiveConfiguration.MqttProviderConfig.PublishedNodes.Remove(publishedNode);
        }
        public void AddNodeOpcPubSubConfig(NodeId nodeId)
        {

        }
        public void RemoveNodePubSubConfig(PublishedNode publishedNode)
        {
            this.ActiveConfiguration.PubSubProviderConfig.PublishedNodes.Remove(publishedNode);
        }

        private void blockingTransitionChange(BlockingTransition blockingTransition)
        {
            foreach (UmatiGatewayAppListener UmatiGatewayAppListener in UmatiGatewayAppListeners)
            {
                try
                {
                    UmatiGatewayAppListener.blockingTransitionChanged(blockingTransition);
                }
                catch (Exception ex)
                {
                    Logger.Info($"Unable to notify Listener: {ex.StackTrace}");
                }
            }
        }
    }
    public class TypeClassNode
    {
        public RelativePathElementCollection RelativePathElements { get; set; }
        public NodeId StartNodeId { get; set; }
        public TypeClassNode(NodeId StartNodeId, RelativePathElementCollection RelativePathElements)
        {
            this.StartNodeId = StartNodeId;
            this.RelativePathElements = RelativePathElements;
        }
    }
}