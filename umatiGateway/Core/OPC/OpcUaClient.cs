// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using NLog;
using Opc.Ua;
using Opc.Ua.Client;

namespace umatiGateway.Core.OPC
{
    public class OpcUaClient : IOpcUaClient
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _clientStateLock = new();
        private volatile Session? session;
        private UmatiGatewayApp app;
        private ApplicationConfiguration applicationConfiguration;
        public bool AutoAccept { get; set; } = true;
        public List<OpcUaEventListener> opcUaEventListeners = new List<OpcUaEventListener>();
        public List<IOpcClientListener> OpcClientListeners = new List<IOpcClientListener>();
        public List<NodeId> subscribedNodeIds = new List<NodeId>();
        public MonitoredItemNotificationEventHandler? publishedDataEventHandler = null;
        public Subscription? publishedDataSubscription = null;
        public Subscription? eventSubscription = null;
        public TypeDictionaries TypeDictionaries { get; set; }
        private bool ClientActive = false;
        private OpcUaClientState ClientState { get; set; } = new OpcUaClientState(OpcUaConnectionState.Idle, "");
        private List<OpcUaClientState> ClientStateHistory = new List<OpcUaClientState>();
        private Thread? reconnectThread;
        private volatile bool keepRunning = true;
        private volatile bool reconnect = false;

        public OpcUaClient(UmatiGatewayApp app, ApplicationConfiguration applicationConfiguration)
        {
            this.app = app;
            this.TypeDictionaries = new TypeDictionaries(app);
            this.applicationConfiguration = applicationConfiguration;
            this.applicationConfiguration.CertificateValidator.CertificateValidation += CertificateValidation;
            this.ClientStateHistory.Add(this.ClientState);
            this.StartReconnectMonitor();
        }
        public void Connect()
        {
            if (this.ClientState.TrySetBlocked())
            {
                this.ClientStateHistory.Clear();
                this.ClientState.setState(OpcUaConnectionState.Connecting);
                this.ClientStateHistory.Add(this.ClientState.Copy());
                this.notifyOpcUaClientListeners();
                this.ClientActive = true;
                try
                {
                    _ = this.ConnectAsync().Result;
                    this.ClientState.setState(OpcUaConnectionState.Connected);
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    this.reconnect = true;
                }
                catch (Exception ex)
                {
                    this.ClientState.setState(OpcUaConnectionState.Error, ex.Message);
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    Logger.Error("Unable to connect to OPC UA server.", ex);
                    throw new OpcUaException("Unable to connect to OPC UA server.", ex);
                }
                finally
                {
                    this.ClientState.ClearBlocked();
                    this.notifyOpcUaClientListeners();
                }
            }
            else
            {
                Logger.Info("OPC UA client is already connecting/disconnecting.");
            }
        }

        public void Disconnect()
        {
            if (this.ClientState.TrySetBlocked())
            {
                this.reconnect = false;
                this.ClientStateHistory.Clear();
                this.ClientActive = false;
                this.ClientState.setState(OpcUaConnectionState.Disconnecting);
                this.ClientStateHistory.Add(this.ClientState.Copy());
                this.notifyOpcUaClientListeners();
                try
                {
                    Logger.Info($"Disconnection OPC UA Session: {this.session}");
                    Session session = this.CheckSession();
                    session.Close();
                    session.Dispose();
                    this.session = null;
                    this.ClientState.setState(OpcUaConnectionState.Idle);
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                }
                catch (Exception ex)
                {
                    this.ClientState.setState(OpcUaConnectionState.Error, ex.Message);
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    Logger.Error($"Failed to disconnect session.", ex);
                    throw new OpcUaException($"Failed to disconnect session.", ex);
                }
                finally
                {
                    this.ClientState.ClearBlocked();
                    this.notifyOpcUaClientListeners();
                }
            }
            else
            {
                Logger.Info("OPC UA client is already connecting/disconnecting.");
            }
        }

        public Node? ReadNode(NodeId nodeId)
        {
            Session session = this.CheckSession();
            try
            {
                return session.ReadNode(nodeId);
            }
            catch (Exception ex)
            {
                throw new OpcUaException($"Failed to read Node with NodeId: {nodeId}", ex);
            }
        }
        public void ConnectEvents(OpcUaEventListener opcUaEventListener)
        {
            Session session = this.CheckSession();
            opcUaEventListeners.Add(opcUaEventListener);
            this.eventSubscription = new Subscription(session.DefaultSubscription);
            eventSubscription.DisplayName = "ModelChangeEventSubscription";
            eventSubscription.PublishingInterval = 1000;
            MonitoredItem eventMonitoredItem = new MonitoredItem(eventSubscription.DefaultItem);
            eventMonitoredItem.StartNodeId = ObjectIds.Server;
            eventMonitoredItem.AttributeId = Attributes.EventNotifier;
            eventMonitoredItem.MonitoringMode = MonitoringMode.Reporting;

            EventFilter filter = new EventFilter();
            filter.AddSelectClause(ObjectTypeIds.BaseEventType, BrowseNames.Changes);
            filter.AddSelectClause(ObjectTypeIds.BaseEventType, BrowseNames.EventType);
            filter.AddSelectClause(ObjectTypeIds.BaseEventType, BrowseNames.SourceNode);

            eventMonitoredItem.Filter = filter;

            eventMonitoredItem.Notification += HandleEventNotification;
            eventSubscription.AddItem(eventMonitoredItem);
            session.AddSubscription(eventSubscription);
            eventSubscription.Create();
        }
        private void HandleEventNotification(MonitoredItem item, MonitoredItemNotificationEventArgs notification)
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
        public BrowseResultCollection BrowseNode(NodeId nodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes)
        {
            Session session = this.CheckSession();
            BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
            BrowseDescription nodeToBrowse = new BrowseDescription();
            nodeToBrowse.NodeId = nodeId;
            nodeToBrowse.BrowseDirection = browseDirection;
            nodeToBrowse.NodeClassMask = nodeClassMask;
            nodeToBrowse.ReferenceTypeId = referenceTypeIds;
            nodeToBrowse.IncludeSubtypes = includeSubTypes;
            nodeToBrowse.ResultMask = (uint)BrowseResultMask.All;
            nodesToBrowse.Add(nodeToBrowse);
            try
            {
                session.Browse(null, null, 10000, nodesToBrowse, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                return results;
            }
            catch (Exception ex)
            {
                throw new OpcUaException("Unable to browse Node:", ex);
            }
        }
        public BrowseResultCollection BrowseNode(NodeId nodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId excludedReferenceTypeId)
        {
            Session session = this.CheckSession();
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
            session.Browse(null, null, 10000, nodesToBrowse, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
            return results;
        }
        public List<NodeId> BrowseNodeIds(BrowseDescriptionCollection included, BrowseDescriptionCollection? excluded = null)
        {
            List<NodeId> resultNodeIds = new List<NodeId>();
            int includedCount = included.Count;
            int excludedCount = 0;
            Session session = this.CheckSession();
            BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
            nodesToBrowse.AddRange(included);
            if (excluded != null)
            {
                excludedCount = excluded.Count;
                nodesToBrowse.AddRange(excluded);
            }
            ResponseHeader responseHeader = session.Browse(null, null, 10000, nodesToBrowse, out BrowseResultCollection browseResults, out DiagnosticInfoCollection diagnosticInfos);
            for (int i = 0; i < browseResults.Count; i++)
            {
                BrowseResult browseResult = browseResults[i];
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    NodeId nodeId = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                    if (i < includedCount)
                    {
                        resultNodeIds.Add(nodeId);
                    }
                    else
                    {
                        if (resultNodeIds.Contains(nodeId))
                        {
                            resultNodeIds.Remove(nodeId);
                        }
                    }
                }
            }
            return resultNodeIds;
        }
        public NodeId? BrowseFirstNodeId(BrowseDescriptionCollection browseDescriptionCollection, BrowseDescriptionCollection? excluded = null)
        {
            List<NodeId> nodeIds = this.BrowseNodeIds(browseDescriptionCollection, excluded);
            return nodeIds.FirstOrDefault();
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

        public async Task<bool> ConnectAsync()
        {
            string serverUrl = this.app.ActiveConfiguration.OPCConnection.ServerEndpoint;
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));

            try
            {
                Logger.Info("Connecting to... {0}", serverUrl);

                // Get the endpoint by connecting to server's discovery endpoint.
                // Try to find the first endopint with security.
                EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(this.applicationConfiguration, serverUrl, false);
                EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(this.applicationConfiguration);
                ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
                UserIdentity userIdentity = new UserIdentity();
                if (!string.IsNullOrWhiteSpace(this.app.ActiveConfiguration.OPCConnection.UserName))
                {
                    userIdentity = new UserIdentity(this.app.ActiveConfiguration.OPCConnection.UserName, this.app.ActiveConfiguration.OPCConnection.Password);
                }
                if (!string.IsNullOrWhiteSpace(this.app.ActiveConfiguration.OPCConnection.CertificatePath))
                {
                    var pfx = new X509Certificate2(
                        this.app.ActiveConfiguration.OPCConnection.CertificatePath,
                        this.app.ActiveConfiguration.OPCConnection.CertificatePassword,
                        X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet
                    );
                    userIdentity = new UserIdentity(pfx);
                }


                // Create the session
                Session session = await Session.Create(
                    this.applicationConfiguration,
                    endpoint,
                false,
                false,
                    this.applicationConfiguration.ApplicationName,
                    30 * 60 * 1000,
                    userIdentity,
                    null
                );

                // Assign the created session
                if (session != null && session.Connected)
                {
                    this.ClientState.setState(OpcUaConnectionState.Connected, "");
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    this.notifyOpcUaClientListeners();
                    this.session = session;


                    // Session created successfully.
                    Logger.Info($"New session created with SessionName = {session.SessionName}");

                    TypeDictionaries = new TypeDictionaries(this.app);
                    TypeDictionaries.ReadExtraLibs = this.app.ActiveConfiguration.OPCConnection.ReadExtraLibs;
                    this.ClientState.setState(OpcUaConnectionState.Connected, "Read Binaries");
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    this.notifyOpcUaClientListeners();
                    Logger.Info("Read Binaries");
                    TypeDictionaries.ReadOpcBinary();
                    this.ClientState.setState(OpcUaConnectionState.Connected, "Read DataTypes");
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    this.notifyOpcUaClientListeners();
                    Logger.Info("Read DataTypes");
                    TypeDictionaries.ReadDataTypes();
                    this.ClientState.setState(OpcUaConnectionState.Connected, "Read EventTypes");
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    this.notifyOpcUaClientListeners();
                    Logger.Info("Read EventTypes");
                    TypeDictionaries.ReadEventTypes();
                    this.ClientState.setState(OpcUaConnectionState.Connected, "Read InterfaceTypes");
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    this.notifyOpcUaClientListeners();
                    Logger.Info("Read InterfaceTypes");
                    TypeDictionaries.ReadInterfaceTypes();
                    this.ClientState.setState(OpcUaConnectionState.Connected, "Read ObjectTypes");
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    this.notifyOpcUaClientListeners();
                    Logger.Info("Read ObjectTypes");
                    TypeDictionaries.ReadObjectTypes();
                    this.ClientState.setState(OpcUaConnectionState.Connected, "Read ReferenceTypes");
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    Logger.Info("Read ReferenceTypes");
                    TypeDictionaries.ReadReferenceTypes();
                    this.ClientState.setState(OpcUaConnectionState.Connected, "Read VariableTypes");
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    this.notifyOpcUaClientListeners();
                    Logger.Info("Read VariableTypes");
                    TypeDictionaries.ReadVariableTypes();
                    this.ClientState.setState(OpcUaConnectionState.Connected, "Read TypeDictionary finished");
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    this.notifyOpcUaClientListeners();
                    return true;
                }
                else
                {
                    Logger.Info("Unable to create OPC Session.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Create Session Error : {ex.Message}");
                this.session = null;
                throw;
            }
        }
        public void TryReconnect()
        {
            if (this.ClientState.TrySetBlocked())
            {
                bool hasException = false;
                this.ClientStateHistory.Clear();
                this.ClientState.setState(OpcUaConnectionState.Reconnecting);
                this.ClientStateHistory.Add(this.ClientState.Copy());
                this.notifyOpcUaClientListeners();
                try
                {
                    Logger.Info("Reconnecting to... {0}", this.app.ActiveConfiguration.OPCConnection.ServerEndpoint);

                    // Get the endpoint by connecting to server's discovery endpoint.
                    // Try to find the first endopint with security.
                    EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(this.applicationConfiguration, this.app.ActiveConfiguration.OPCConnection.ServerEndpoint, false);
                    EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(this.applicationConfiguration);
                    ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
                    UserIdentity userIdentity = new UserIdentity();
                    if (!string.IsNullOrWhiteSpace(this.app.ActiveConfiguration.OPCConnection.UserName))
                    {
                        userIdentity = new UserIdentity(this.app.ActiveConfiguration.OPCConnection.UserName, this.app.ActiveConfiguration.OPCConnection.Password);
                    }

                    // Create the session
                    this.session = Session.Create(
                        this.applicationConfiguration,
                        endpoint,
                    false,
                    false,
                        this.applicationConfiguration.ApplicationName,
                        30 * 60 * 1000,
                        userIdentity,
                        null
                    ).Result;
                    if (this.publishedDataEventHandler != null && this.publishedDataSubscription != null)
                    {
                        this.publishedDataSubscription.Dispose();
                        this.publishedDataSubscription = null;
                        this.SubscribeToDataChanges(this.subscribedNodeIds, this.publishedDataEventHandler);
                        foreach (OpcUaEventListener eventListener in this.opcUaEventListeners)
                        {
                            this.ConnectEvents(eventListener);
                        }
                    }
                }
                catch (Exception ex)
                {
                    hasException = true;
                    this.ClientState.setState(OpcUaConnectionState.Error, ex.Message);
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    Logger.Error(ex, "Exception on Reconnecting");
                }
                finally
                {
                    if (session != null && session.Connected && !hasException)
                    {
                        this.ClientState.setState(OpcUaConnectionState.Connected);
                        this.ClientStateHistory.Add(this.ClientState.Copy());
                    }
                    else
                    {
                        this.ClientState.setState(OpcUaConnectionState.Idle);
                        this.ClientStateHistory.Add(this.ClientState.Copy());
                    }
                    this.ClientState.ClearBlocked();
                    this.notifyOpcUaClientListeners();
                }
            }
            else
            {
                Logger.Info("Allready trying to Connect/Reconnect/Disconnect!");
            }
        }
        public void StartReconnectMonitor()
        {
            reconnectThread = new Thread(() =>
            {
                while (keepRunning)
                {
                    if (reconnect)
                    {
                        try
                        {
                            bool stillConnected = true;
                            try
                            {
                                this.ReadNode(VariableIds.ServerType_ServerStatus_CurrentTime);
                            }
                            catch (Exception ex)
                            {
                                stillConnected = false;
                                Logger.Error(ex, "Error Reading server CurrentTime");
                            }
                            if (session == null || !session.Connected || !stillConnected)
                            {
                                Console.WriteLine("Connection lost - trying reconnect...");
                                TryReconnect();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("❌ Reconnect-Fehler: " + ex.Message);
                        }
                    }
                    Thread.Sleep(10000); // 10 Sekunden warten
                }
            });

            reconnectThread.IsBackground = true;
            reconnectThread.Start();
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
            Logger.Error(error);
            if (error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted && AutoAccept)
            {
                certificateAccepted = true;
            }

            if (certificateAccepted)
            {
                Logger.Info("Untrusted certificate accepted. Subject = {0}", e.Certificate.Subject);
                e.Accept = true;
            }
            else
            {
                Logger.Info("Untrusted certificate rejected. Subject = {0}", e.Certificate.Subject);
            }
        }
        public Session CheckSession()
        {
            try
            {
                if (this.session != null && session.Connected)
                {
                    return this.session;
                }
                else
                {
                    throw new OpcUaException("Opc Ua Session not connected");
                }
            }
            catch (Exception ex)
            {
                throw new OpcUaException("Exception on checking Opc Session state.", ex);
            }
        }

        string IOpcUaClient.GetSessionId()
        {
            if (this.session != null)
            {
                return session.SessionId.ToString();
            }
            else
            {
                return "";
            }
        }
        string IOpcUaClient.GetSessionName()
        {
            if (this.session != null)
            {
                return session.SessionName;
            }
            else
            {
                return "";
            }
        }

        public List<NodeId> BrowseLocalNodeIdsWithTypeDefinition(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeId, bool includeSubTypes, NodeId expectedTypeDefinition)
        {
            Session session = this.CheckSession();
            List<NodeId> filteredNodeIds = new List<NodeId>();
            try
            {
                List<NodeId> nodeIds = BrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeId, includeSubTypes);
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
            catch (Exception exception)
            {
                throw new OpcUaException($"Unable to BrowseLocalNodeIds with Typedefinition for node: {rootNodeId}", exception);
            }
        }

        public NodeId? BrowseTypeDefinition(NodeId nodeId)
        {
            NodeId? typeDefinition = null;
            try
            {
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
            catch (Exception exception)
            {
                throw new OpcUaException($"Unable to browse TypeDefinition for NodeId: {nodeId}", exception);
            }
        }
        public NodeId? BrowseLocalNodeId(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes)
        {
            Session session = this.CheckSession();
            try
            {
                List<NodeId> nodeIds = BrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes);
                foreach (NodeId nodeId in nodeIds)
                {
                    return nodeId;
                }
                return null;
            }
            catch (Exception exception)
            {
                throw new OpcUaException($"Unable to Browse NodeId:{rootNodeId}", exception);
            }
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
                    browseDescriptions.Add(BrowseUtils.GetModellingRule(nodeId));
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
        public BrowseResultCollection Browse(BrowseDescription browseDescription)
        {
            Session session = this.CheckSession();
            try
            {
                BrowseDescriptionCollection browseDescriptionCollection = new BrowseDescriptionCollection(new BrowseDescription[] { browseDescription });
                session.Browse(null, null, 10000, browseDescriptionCollection, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                return results;
            }
            catch (Exception exception)
            {
                throw new OpcUaException($"Unable to browse Browsedescription: {browseDescription}", exception);
            }
        }
        public BrowseResultCollection Browse(BrowseDescriptionCollection browseDescriptionCollection)
        {
            Session session = this.CheckSession();
            try
            {
                session.Browse(null, null, 10000, browseDescriptionCollection, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                return results;
            }
            catch (Exception exception)
            {
                throw new OpcUaException($"Unable to browse browseDescriptions: {browseDescriptionCollection}", exception);
            }
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
            Session session = this.CheckSession();
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
            if (browsePathCollection.Count > 0)
            {
                if (session != null)
                {
                    session.TranslateBrowsePathsToNodeIds(null, browsePathCollection, out BrowsePathResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
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

        public DataValue? ReadValue(NodeId nodeId)
        {
            Session session = this.CheckSession();
            try
            {
                return session.ReadValue(nodeId);
            }
            catch (Exception exception)
            {
                throw new OpcUaException($"Unable to read Datavalue for NodeId: {nodeId}", exception);
            }
        }

        Session? IOpcUaClient.GetSession()
        {
            return this.session;
        }
        public NamespaceTable GetNamespaceTable()
        {
            Session session = this.CheckSession();
            try
            {
                DataValue dv = session.ReadValue(VariableIds.Server_NamespaceArray);
                string[] namespaces = (string[])dv.Value;
                return new NamespaceTable(namespaces);
            }
            catch (Exception exception)
            {
                throw new OpcUaException("Unable to get NamespaceTable from server.", exception);
            }
        }
        public uint SubscribeToDataChanges(List<NodeId> nodeIds, MonitoredItemNotificationEventHandler eventHandler)
        {
            this.subscribedNodeIds = nodeIds;
            this.publishedDataEventHandler = eventHandler;
            Session session = this.CheckSession();
            uint subscriptionId = 0;
            try
            {
                if (this.publishedDataSubscription == null)
                {
                    this.publishedDataSubscription = new Subscription(session.DefaultSubscription);
                    this.publishedDataSubscription.DisplayName = "Subscription for NodeIds";
                    this.publishedDataSubscription.PublishingEnabled = true;
                    this.publishedDataSubscription.PublishingInterval = 1000;
                    session.AddSubscription(publishedDataSubscription);
                    publishedDataSubscription.Create();
                    subscriptionId = publishedDataSubscription.Id;
                }

                foreach (NodeId nodeId in nodeIds)
                {
                    MonitoredItem intMonitoredItem = new MonitoredItem(publishedDataSubscription.DefaultItem);
                    intMonitoredItem.StartNodeId = nodeId;
                    intMonitoredItem.AttributeId = Attributes.Value;
                    intMonitoredItem.DisplayName = "Subscription";
                    intMonitoredItem.SamplingInterval = 1000;
                    intMonitoredItem.Notification += eventHandler;

                    publishedDataSubscription.AddItem(intMonitoredItem);
                }
                publishedDataSubscription.ApplyChanges();
            }
            catch (Exception exception)
            {
                throw new OpcUaException("Unable to Create Subscription.", exception);
            }
            return subscriptionId;
        }

        public void ClearSubscriptions()
        {
            //this.publishedDataSubscription = null;
        }

        bool IOpcUaClient.IsClientActive()
        {
            return this.ClientActive;
        }

        public OpcUaClientState GetClientState()
        {
            return this.ClientState;
        }

        void IOpcUaClient.AddOpcClientListener(IOpcClientListener opcClientListener)
        {
            this.OpcClientListeners.Add(opcClientListener);
        }
        private void notifyOpcUaClientListeners()
        {
            foreach (IOpcClientListener listener in this.OpcClientListeners)
            {
                listener.Change();
            }
        }

        List<OpcUaClientState> IOpcUaClient.GetClientStateHistory()
        {
            return this.ClientStateHistory;
        }

        TypeDictionaries IOpcUaClient.GetTypeDictionaries()
        {
            return this.TypeDictionaries;
        }
    }
}
