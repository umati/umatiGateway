// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using NLog;
using Opc.Ua;
using Opc.Ua.Client;
using System.Reflection;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;

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
        private volatile bool wasinitialConnected = false;

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
                    this.wasinitialConnected = true;
                    
                }
                catch (Exception ex)
                {
                    this.ClientState.setState(OpcUaConnectionState.Error, ex.Message);
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    Logger.Error("Unable to connect to OPC UA server.", ex);
                    //throw new OpcUaException("Unable to connect to OPC UA server.", ex);
                }
                finally
                {
                    this.reconnect = true;
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
                Logger.Info("Disconnection OPC UA Session: {Session}", this.session);
                if(this.TryCheckSession(out Session? checkedSession))
                {
                    if(checkedSession != null)
                    {
                        try
                        {
                            checkedSession.Close();
                            checkedSession.Dispose();
                            this.session = null;
                        } 
                        catch (Exception exception)
                        {
                            this.ClientState.setState(OpcUaConnectionState.Error, exception.Message);
                            this.ClientStateHistory.Add(this.ClientState.Copy());
                            Logger.Error(exception, "Exception on disconnecting Session.");
                        }
                }
                this.ClientState.setState(OpcUaConnectionState.Idle);
                this.ClientStateHistory.Add(this.ClientState.Copy());
                }
                this.ClientState.ClearBlocked();
                this.notifyOpcUaClientListeners();
            }
            else
            {
                Logger.Info("OPC UA client is already connecting/disconnecting.");
            }
        }

        public Node? ReadNode(NodeId nodeId)
        {
            if (this.TryCheckSession(out Session? checkedSession) && checkedSession != null)
            {
                try
                {
                    return checkedSession.ReadNode(nodeId);
                }
                catch (ServiceResultException ex) when (ex.StatusCode == Opc.Ua.StatusCodes.BadNodeIdUnknown)
                {
                    Logger.Warn(ex, "Failed to read Node with NodeId: {nodeId}", nodeId);
                    return null;
                }
                catch (Exception ex)
                {
                    throw new OpcUaException($"Failed to read Node with NodeId: {nodeId}", ex);
                }
            }
            else
            {
                Logger.Error("Invalid Session.");
                return null;
            }
        }
        public bool TryConnectEvents(OpcUaEventListener opcUaEventListener)
        {
            if (this.TryCheckSession(out Session? checkedSession) && checkedSession != null)
            {
                opcUaEventListeners.Add(opcUaEventListener);
                this.eventSubscription = new Subscription(checkedSession.DefaultSubscription);
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
                checkedSession.AddSubscription(eventSubscription);
                eventSubscription.Create();
                return true;
            }
            else
            {
                Logger.Error("InvalidSession");
                return false;
            }
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
                            foreach (ExtensionObject extensionObject in etos)
                            {
                                object body = extensionObject.Body;
                                if (body is ModelChangeStructureDataType)
                                {
                                    ModelChangeStructureDataType mcs = (ModelChangeStructureDataType)body;
                                    if (mcs.Affected != null)
                                    {
                                        Logger.Info("Affected: {Affected}", mcs.Affected);
                                        Logger.Info("AffectedType: {AffectedType}", mcs.AffectedType);
                                        Logger.Info("Verb: {Verb}", mcs.Verb);
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
                Logger.Info("OnMonitoredItemNotification error: {ExceptionMessage}", ex.Message);
            }
        }
        public bool TryBrowseNode(NodeId nodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, out BrowseResultCollection browseResultCollection)
        {
            browseResultCollection = new BrowseResultCollection();
            if (this.TryCheckSession(out Session? checkedSession) && checkedSession != null)
            {
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
                    browseResultCollection = results;
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Unable to browse Node:", nodeId);
                    return false;
                }
            }
            else
            {
                Logger.Error("Invalid Session");
                return false;
            }
        }
        public BrowseResultCollection BrowseNode(NodeId nodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId excludedReferenceTypeId)
        {
            if (this.TryCheckSession(out Session? checkedSession) && checkedSession != null)
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
                session.Browse(null, null, 10000, nodesToBrowse, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                return results;
            } 
            else
            {
                Logger.Error("Invalid Session.");
                //Remove after Refactoring
                return new BrowseResultCollection();
            }
        }
        public bool TryBrowseNodeIds(BrowseDescriptionCollection included, out List<NodeId> browsedNodeIds, BrowseDescriptionCollection? excluded = null)
        {
            browsedNodeIds = new List<NodeId>();
            int includedCount = included.Count;
            int excludedCount = 0;
            if (this.TryCheckSession(out Session? checkedSession) && checkedSession != null)
            {
                BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                nodesToBrowse.AddRange(included);
                if (excluded != null)
                {
                    excludedCount = excluded.Count;
                    nodesToBrowse.AddRange(excluded);
                }
                try
                {
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
                                browsedNodeIds.Add(nodeId);
                            }
                            else
                            {
                                if (browsedNodeIds.Contains(nodeId))
                                {
                                    browsedNodeIds.Remove(nodeId);
                                }
                            }
                        }
                    }
                    return true;
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, "Unable to browse NodeIds.");
                    return false;
                }
            }
            else
            {
                Logger.Error("Invalid Session.");
                return false;
            }
        }
        public bool TryBrowseFirstNodeId(BrowseDescriptionCollection browseDescriptionCollection, out NodeId? browsedNodeId, BrowseDescriptionCollection? excluded = null)
        {
            browsedNodeId = null;
            try
            {
                if (this.TryBrowseNodeIds(browseDescriptionCollection, out List<NodeId> nodeIds, excluded))
                {
                    browsedNodeId = nodeIds.FirstOrDefault();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch(Exception exception)
            {
                Logger.Error(exception, "Unable to browse FirstNodeIds.");
                return false;
            }
        }
        public bool TryBrowseLocalNodeIds(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, out List<NodeId> localNodeIds)
        {
            localNodeIds = new List<NodeId>();
            try
            {
                if (TryBrowseNode(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes, out BrowseResultCollection browseResults))
                {
                    foreach (BrowseResult browseResult in browseResults)
                    {
                        ReferenceDescriptionCollection references = browseResult.References;
                        foreach (ReferenceDescription reference in references)
                        {
                            localNodeIds.Add(new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex));
                        }
                    }
                    return true;
                }
                else
                {
                    Logger.Error("Unable to browse NodeIds for {RootNodeId}", rootNodeId);
                    return false;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Unable to browse NodeIds for {RootNodeId}", rootNodeId);
                return false;
            }
        }
        public bool TryBrowseLocalNodeIdsExcludeReference(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId excludedReferenceTypeId, out List<NodeId> localNodeIds)
        {
            localNodeIds = new List<NodeId>();
            try
            {
                BrowseResultCollection browseResults = BrowseNode(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes, excludedReferenceTypeId);
                if (browseResults.Count == 2)
                {
                    BrowseResult allNodesResult = browseResults[0];
                    ReferenceDescriptionCollection allReferences = allNodesResult.References;
                    foreach (ReferenceDescription reference in allReferences)
                    {
                        localNodeIds.Add(new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex));
                    }
                    BrowseResult excludedNodesResult = browseResults[1];
                    ReferenceDescriptionCollection excludedReferences = excludedNodesResult.References;
                    foreach (ReferenceDescription excludedReference in excludedReferences)
                    {
                        localNodeIds.Remove(new NodeId(excludedReference.NodeId.Identifier, excludedReference.NodeId.NamespaceIndex));
                    }
                }
                else
                {
                    Logger.Error("Number of BrowseResults does not match 2.");
                }
                return true;
            }
            catch(Exception exception)
            {
                Logger.Error(exception, "Unable to browse NodeIds for {RootNodeId}", rootNodeId);
                return false;
            }
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
                    Logger.Info("New session created with SessionName = {SessionName}", session.SessionName);

                    TypeDictionaries = new TypeDictionaries(this.app);
                    TypeDictionaries.ReadExtraLibs = this.app.ActiveConfiguration.OPCConnection.ReadExtraLibs;
                    this.ClientState.setState(OpcUaConnectionState.Connected, "Read Binaries");
                    this.ClientStateHistory.Add(this.ClientState.Copy());
                    this.notifyOpcUaClientListeners();
                    Logger.Info("Read Binaries");
                    TypeDictionaries.ReadOpcBinary();
                    if (this.app.ActiveConfiguration.OPCConnection.ResolveBinariesOnly)
                    {
                        Logger.Info("Skip Reading Objects, ObjectTypes, Variables, VariableTypes, Interfaces and References");
                    }
                    else
                    {
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
                    }
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
                Logger.Error("Create Session Error : {ExceptionMessage}", ex.Message);
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
                        if(!this.TrySubscribeToDataChanges(this.subscribedNodeIds, this.publishedDataEventHandler, out uint subscriptionId))
                        {
                            Logger.Error("Unable to subscribe");
                        }
                        foreach (OpcUaEventListener eventListener in this.opcUaEventListeners)
                        {

                            if(!this.TryConnectEvents(eventListener))
                            {
                                Logger.Error("Unable to Connect Events!");
                            }
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
                Logger.Info("Already trying to Connect/Reconnect/Disconnect!");
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
                                this.DisposeSession();
                                Logger.Error(ex, "Error Reading server CurrentTime");
                            }
                            if (session == null || !session.Connected || !stillConnected)
                            {
                                if (this.wasinitialConnected)
                                {
                                    Logger.Info("Connection lost - trying reconnect...");
                                    TryReconnect();
                                } 
                                else
                                {
                                    Logger.Info("Try to establish connection to OPC Server...");
                                    Connect();
                                }
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
        private void DisposeSession()
        {
            if (session == null) return;

            try
            {
                foreach (var sub in session.Subscriptions.ToList())
                {
                    try { sub.Dispose(); } catch { }
                }
            }
            catch { }

            try { session.Close(); } catch { }
            try { session.Dispose(); } catch { }
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
        public bool TryCheckSession(out Session? session)
        {
            session = null;
            try
            {
                if (this.session != null && this.session.Connected)
                {
                    session = this.session;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Invalid Session!");
                return false;
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

        public bool TryBrowseLocalNodeIdsWithTypeDefinition(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeId, bool includeSubTypes, NodeId expectedTypeDefinition, out List<NodeId> localNodeIds)
        {
            localNodeIds = new List<NodeId>();
            try
            {
                if (this.TryCheckSession(out Session? checkedSession) && checkedSession != null)
                {
                    if (TryBrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeId, includeSubTypes, out List<NodeId> nodeIds))
                    {
                        foreach (NodeId nodeId in nodeIds)
                        {
                            if (TryBrowseTypeDefinition(nodeId, out NodeId? typeDefinition))
                            {
                                if (typeDefinition != null)
                                {
                                    if (typeDefinition == expectedTypeDefinition)
                                    {
                                        localNodeIds.Add(nodeId);
                                    }
                                }
                            }
                            else
                            {
                                Logger.Error("Unable to browse TypeDefinition");
                            }
                        }
                    }
                    return true;
                }
                else
                {
                    Logger.Error("Invalid Session.");
                    return false;
                }
            }
            catch(Exception exception)
            {
                Logger.Error(exception, "Unable to browse NodeIds for {RootNodeId}", rootNodeId);
                return false;
            }
        }

        public bool TryBrowseTypeDefinition(NodeId nodeId, out NodeId? typeDefinitionNodeId)
        {
            typeDefinitionNodeId = null;
            try
            {
                if (TryBrowseNode(nodeId, BrowseDirection.Forward, (uint)NodeClass.ObjectType | (uint)NodeClass.VariableType, ReferenceTypes.HasTypeDefinition, false, out BrowseResultCollection browseResults))
                {
                    foreach (BrowseResult browseResult in browseResults)
                    {
                        ReferenceDescriptionCollection references = browseResult.References;
                        foreach (ReferenceDescription reference in references)
                        {
                            typeDefinitionNodeId = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                            break;
                        }
                    }
                    return true;
                }
                else
                {
                    Logger.Error("Unable to browse TypeDefinition for NodeId: {NodeId}", nodeId);
                    return false;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Unable to browse TypeDefinition for NodeId: {NodeId}", nodeId);
                return false;
            }
        }
        public bool TryBrowseLocalNodeId(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, out NodeId? localNodeId)
        {
            localNodeId = null;
            if (TryBrowseLocalNodeIds(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes, out List<NodeId> nodeIds))
            {
                foreach (NodeId nodeId in nodeIds)
                {
                    localNodeId = nodeId;
                    return true;
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool TryBrowseLocalNodeIdWithBrowseName(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, QualifiedName browseName, out NodeId? localNodeId)
        {
            localNodeId = null;
            try
            {
                if (TryBrowseNode(rootNodeId, browseDirection, nodeClassMask, referenceTypeIds, includeSubTypes, out BrowseResultCollection browseResults))
                {
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
                                    localNodeId = innerNodeId;
                                    return true;
                                }
                            }
                        }
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                return false;
            }
        }
        public bool TryGetOptionalAndMandatoryPlaceholders(NodeId typeDefinition, out List<NodeId> optionalMandatoryPlaceholders)
        {
            optionalMandatoryPlaceholders = new List<NodeId>();
            //Look for the Children
            try
            {
                if (TryBrowseNode(typeDefinition, BrowseDirection.Forward, 0, ReferenceTypeIds.HasComponent, true, out BrowseResultCollection browseResultCollection))
                {
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
                                        optionalMandatoryPlaceholders.Add(browseDescriptions[i].NodeId);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    return false;
                }
            } 
            catch(Exception exception)
            {
                Logger.Error(exception, "Unable to browse OptionalAndMandatoryPlaceholders: {NodeId}", typeDefinition);
                return false;
            }
            return true;
        }
        public BrowseResultCollection Browse(BrowseDescriptionCollection browseDescriptionCollection)
        {
            if (this.TryCheckSession(out Session? checkedSession) && checkedSession != null)
            {
                try
                {
                    checkedSession.Browse(null, null, 10000, browseDescriptionCollection, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                    return results;
                }
                catch (Exception exception)
                {
                    throw new OpcUaException($"Unable to browse browseDescriptions: {browseDescriptionCollection}", exception);
                }
            }
            else
            {
                Logger.Error("Invalid Session");
                return new BrowseResultCollection();
            }
        }
        public bool TryBrowseAllHierarchicalSubType(NodeId nodeId, List<NodeId> subTypeList)
        {
            try
            {
                if (TryBrowseNode(nodeId, BrowseDirection.Forward, (uint)NodeClass.ObjectType | (uint)NodeClass.VariableType, ReferenceTypes.HasSubtype, false, out BrowseResultCollection browseResults))
                {
                    foreach (BrowseResult browseResult in browseResults)
                    {
                        ReferenceDescriptionCollection references = browseResult.References;
                        foreach (ReferenceDescription reference in references)
                        {
                            NodeId subTypeId = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                            if (this.TryBrowseAllHierarchicalSubType(subTypeId, subTypeList))
                            {
                                subTypeList.Add(subTypeId);
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            } 
            catch (Exception exception)
            {
                Logger.Error(exception, "Error on Browsing Hierarchical SubTypes");
                return false;
            }
        }
        public bool TryGetTypeClassNodesForNodeId(NodeId nodeId, out List<TypeClassNode> typeClassNodes, RelativePathElementCollection? pathToChild = null)
        {
            typeClassNodes = new List<TypeClassNode>();
            try
            {
                Node? node = ReadNode(nodeId);
                if (node != null)
                {
                    RelativePath relativePath = new RelativePath(node.BrowseName);
                    if (pathToChild != null)
                    {
                        relativePath.Elements.AddRange(pathToChild);
                    }
                    if (TryBrowseLocalNodeIds(nodeId, BrowseDirection.Inverse, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true, out List<NodeId> parentNodeIds))
                    {
                        foreach (NodeId parentNodeId in parentNodeIds)
                        {
                            if (TryBrowseTypeDefinition(parentNodeId, out NodeId? parentTypeDefinition))
                            {
                                if (parentTypeDefinition != null)
                                {
                                    typeClassNodes.Add(new TypeClassNode(parentTypeDefinition, relativePath.Elements));
                                }
                                if (TryGetTypeClassNodesForNodeId(parentNodeId, out List<TypeClassNode> childDictionary, relativePath.Elements))
                                {
                                    foreach (TypeClassNode typeclassnode in childDictionary)
                                    {
                                        typeClassNodes.Add(typeclassnode);
                                    }
                                } 
                                else
                                {
                                    Logger.Error("Unable to BrowseTypedefinition.");
                                }
                            }
                            else
                            {
                                Logger.Error("Unable to BrowseTypedefinition.");
                            }
                        }
                    }
                }
            }
            catch(Exception exception)
            {
                Logger.Error(exception, "Unable to browse TypeClassNodes for nodeId: {NodeId}", nodeId);
                return false;
            }
            return true;
        }
        public bool TryGetOptionalAndMandatoryPlaceholdersForTypeClassNodes(List<TypeClassNode> typeClassNodes, out List <NodeId> optionalMandatoryPlaceholders)
        {
            optionalMandatoryPlaceholders = new List<NodeId>();
            if (this.TryCheckSession(out Session? checkedSession) && checkedSession != null)
            {
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
                        try
                        {
                            checkedSession.TranslateBrowsePathsToNodeIds(null, browsePathCollection, out BrowsePathResultCollection results, out DiagnosticInfoCollection diagnosticInfos);
                            foreach (BrowsePathResult result in results)
                            {
                                if (StatusCode.IsGood(result.StatusCode))
                                {
                                    if (result.Targets.Count > 0)
                                    {
                                        NodeId nodeId = ExpandedNodeId.ToNodeId(result.Targets[0].TargetId, checkedSession.NamespaceUris);
                                        optionalMandatoryPlaceholders.Add(nodeId);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error("Unable to BrowseTypedefinition.");
                            return false;
                        }
                    }
                }
                return true;
            }
            else
            {
                Logger.Error("Invalid Session");
                return false;
            }
        }

        public DataValue? ReadValue(NodeId nodeId)
        {
            if (this.TryCheckSession(out Session? checkedSession) && checkedSession != null)
            {
                try
                {
                    return session.ReadValue(nodeId);
                }
                catch (Exception exception)
                {
                    throw new OpcUaException($"Unable to read Datavalue for NodeId: {nodeId}", exception);
                }
            }
            else
            {
                Logger.Error("Invalid Session");
                throw new OpcUaException($"Invalid Session");
            }
        }

        Session? IOpcUaClient.GetSession()
        {
            return this.session;
        }
        public bool TryGetNamespaceTable(out NamespaceTable namespaceTable)
        {
            namespaceTable = new NamespaceTable();
            if (this.TryCheckSession(out Session? checkedSession) && session != null)
            {
                try
                {
                    DataValue dv = checkedSession.ReadValue(VariableIds.Server_NamespaceArray);
                    string[] namespaces = (string[])dv.Value;
                    namespaceTable = new NamespaceTable(namespaces);
                    return true;
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, "Unable to get NamespaceTable from server.");
                    return false;
                }
            }
            else
            {
                Logger.Error("Invalid Session");
                return false;
            }
        }
        public bool TrySubscribeToDataChanges(List<NodeId> nodeIds, MonitoredItemNotificationEventHandler eventHandler, out uint subscriptionId)
        {
            subscriptionId = 0;
            this.subscribedNodeIds = nodeIds;
            this.publishedDataEventHandler = eventHandler;
            if (this.TryCheckSession(out Session? checkedSession) && checkedSession != null)
            {
                try
                {
                    if (this.publishedDataSubscription == null)
                    {
                        this.publishedDataSubscription = new Subscription(session.DefaultSubscription);
                        this.publishedDataSubscription.DisplayName = "Subscription for NodeIds";
                        this.publishedDataSubscription.PublishingEnabled = true;
                        this.publishedDataSubscription.PublishingInterval = 1000;
                        checkedSession.AddSubscription(publishedDataSubscription);
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
                    return true;
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, "Unable to Create Subscription.");
                    return false;
                }
            }
            else
            {
                Logger.Error("Invalid Session");
                return false;
            }
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
