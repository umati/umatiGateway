using Microsoft.Extensions.Configuration;
using NLog;
using Opc.Ua;
using Opc.Ua.Client;

namespace umatiGateway.Core.OPC
{
    public class OpcUaClient : IOpcUaClient
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private Session? session;
        private UmatiGatewayApp app;
        private BlockingTransition blockingTransition;
        private ApplicationConfiguration applicationConfiguration;
        private bool AutoAccept = true;
        public List<OpcUaEventListener> opcUaEventListeners = new List<OpcUaEventListener>();
        public List<UmatiGatewayAppListener> UmatiGatewayAppListeners = new List<UmatiGatewayAppListener>();
        public TypeDictionaries TypeDictionaries { get; set; }

        public OpcUaClient(UmatiGatewayApp app, ApplicationConfiguration applicationConfiguration)
        {
            this.app = app;
            this.blockingTransition = new BlockingTransition();
            this.TypeDictionaries = new TypeDictionaries(app);
            this.applicationConfiguration = applicationConfiguration;
            this.applicationConfiguration.CertificateValidator.CertificateValidation += CertificateValidation;
        }
        public void Connect()
        {
            try
            {
                _= this.ConnectAsync().Result;
            } catch (Exception ex)
            {
                throw new OpcUaException("Unable to Connect to OPC Ua Server.", ex);
            }
        }

        public void Disconnect()
        {
            try
            {
                Logger.Info($"Disconnection Opc Ua Session: {this.session}");
                Session session = this.CheckSession();
                session.Close();
                session.Dispose();
                this.session = null;
            }
            catch (Exception ex)
            {
                throw new OpcUaException($"Failed to disconnect Session.", ex);
            }
        }

        public Node? ReadNode(NodeId nodeId)
        {
            try
            {
                Node? node = null;
                Session session = this.CheckSession();
                return node;
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
            Subscription subscription = new Subscription(session.DefaultSubscription);
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
            session.AddSubscription(subscription);
            subscription.Create();
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
            if (!blockingTransition.isBlocking)
            {
                blockingTransition = new BlockingTransition("Connecting OPC", $"Connecting to {serverUrl}", "", true);
                BlockingTransitionChange(blockingTransition);
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

                        this.session = session;


                        // Session created successfully.
                        Logger.Info($"New Session Created with SessionName = {session.SessionName}");

                        TypeDictionaries = new TypeDictionaries(this.app);
                        TypeDictionaries.ReadExtraLibs = this.app.ActiveConfiguration.OPCConnection.ReadExtraLibs;
                        blockingTransition.Message = "Read Type Dictionaries";
                        blockingTransition.Detail = "Read Binaries";
                        BlockingTransitionChange(blockingTransition);
                        //this.TypeDictionaries.ReadTypeDictionary(false);
                        Logger.Info("Read Binaries");
                        TypeDictionaries.ReadOpcBinary();
                        blockingTransition.Detail = "Read DataTypes";
                        BlockingTransitionChange(blockingTransition);
                        Logger.Info("Read DataTypes");
                        TypeDictionaries.ReadDataTypes();
                        blockingTransition.Detail = "Read EventTypes";
                        BlockingTransitionChange(blockingTransition);
                        Logger.Info("Read EventTypes");
                        TypeDictionaries.ReadEventTypes();
                        blockingTransition.Detail = "Read InterfaceTypes";
                        BlockingTransitionChange(blockingTransition);
                        Logger.Info("Read InterfaceTypes");
                        TypeDictionaries.ReadInterfaceTypes();
                        blockingTransition.Detail = "Read ObjectTypes";
                        BlockingTransitionChange(blockingTransition);
                        Logger.Info("Read ObjectTypes");
                        TypeDictionaries.ReadObjectTypes();
                        blockingTransition.Detail = "Read ReferenceTypes";
                        BlockingTransitionChange(blockingTransition);
                        Logger.Info("Read ReferenceTypes");
                        TypeDictionaries.ReadReferenceTypes();
                        blockingTransition.Detail = "Read VariableTypes";
                        BlockingTransitionChange(blockingTransition);
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
                    this.session = null;
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
        private void BlockingTransitionChange(BlockingTransition blockingTransition)
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
                Logger.Info("Untrusted Certificate accepted. Subject = {0}", e.Certificate.Subject);
                e.Accept = true;
            }
            else
            {
                Logger.Info("Untrusted Certificate rejected. Subject = {0}", e.Certificate.Subject);
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
    }
}
