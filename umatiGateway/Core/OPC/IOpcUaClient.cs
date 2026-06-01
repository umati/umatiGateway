using Opc.Ua;
using Opc.Ua.Client;

namespace umatiGateway.Core.OPC
{
    public interface IOpcUaClient
    {
        public TypeDictionaries GetTypeDictionaries();
        public List<OpcUaClientState> GetClientStateHistory();
        public OpcUaClientState GetClientState();
        /// <summary>
        /// Indicates if the client tries to maintain an active connection or not.
        /// </summary>
        /// <returns>True if the client tries to maintain or establish the connection. False if the client disconnects or is disconnected.</returns>
        public bool IsClientActive();
        /// <summary>
        /// Connects the OpcUaClient to the OPC Server.
        /// </summary>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public void Connect();
        /// <summary>
        /// Disconnects the OPC Ua Server from the OPC Server.
        /// </summary>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public void Disconnect();
        /// <summary>
        /// Reads the Typedefinition for a given NodeId.
        /// </summary>
        /// <param name="nodeId"> The NodeId for a that the Typedefinition is red.</param>
        /// <param name="typeDefinition">The TypeDefinition for the given NodeId or null if there is no TypeDefinition for the given node.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryBrowseTypeDefinition(NodeId nodeId, out NodeId? typeDefinition);
        /// <summary>
        /// Returns a List of nodeIds matching the description.
        /// </summary>
        /// <param name="nodeId">Starting nodeId.</param>
        /// <param name="browseDirection">The browsedirection in which is searched.</param>
        /// <param name="nodeClass">The nodeclass for which is searched.</param>
        /// <param name="ReferenceTypeId">The node Id of the Reference for that is searched.</param>
        /// <param name="includeSubTypes"> A boolean that indicates if Subtypes of the ReferenceType should be included.</param>
        /// <param name="localNodeIds"> The browsed local NodeIds.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryBrowseLocalNodeIds(NodeId nodeId, BrowseDirection browseDirection, uint nodeClass, NodeId ReferenceTypeId, bool includeSubTypes, out List<NodeId> localNodeIds);
        /// <summary>
        /// Returns a List of NodeIds matching the parameters.
        /// </summary>
        /// <param name="nodeId">Starting nodeId.</param>
        /// <param name="browseDirection">The browsedirection in which is searched.</param>
        /// <param name="nodeClass">The node classes for which is searched.</param>
        /// <param name="referenceTypeId">The nodeId of the References for that is searched.</param>
        /// <param name="includeSubTypes">A boolean that indicates if Subtypes of the ReferenceType should be searched as well.</param>
        /// <param name="typeDefinition">The typedfinition of the NodeIds that should be returned.</param>
        /// <param name="localNodeIds"> The browsed local NodeIds.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryBrowseLocalNodeIdsWithTypeDefinition(NodeId nodeId, BrowseDirection browseDirection, uint nodeClass, NodeId referenceTypeId, bool includeSubTypes, NodeId typeDefinition, out List<NodeId> localNodeIds);
        /// <summary>
        /// Returns a List of NodeIds matching the parameters.
        /// </summary>
        /// <param name="rootNodeId"> Starting nodeId.</param>
        /// <param name="browseDirection">The browsedirection in which is searched.</param>
        /// <param name="nodeClassMask">The node classes for which is searched.</param>
        /// <param name="referenceTypeIds">The nodeId of the References for that is searched.</param>
        /// <param name="includeSubTypes">A boolean that indicates if Subtypes of the ReferenceType should be searched as well.</param>
        /// <param name="excludedReferenceTypeId">The nodeId of the Reference that should be excluded.</param>
        /// <param name="localNodeIds"> The browsed local NodeIds.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryBrowseLocalNodeIdsExcludeReference(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId excludedReferenceTypeId, out List<NodeId> localNodeIds);
        /// <summary>
        /// Browses the first local NodeId that matches the given criteria.
        /// </summary>
        /// <param name="rootNodeId">Starting nodeId.</param>
        /// <param name="browseDirection">The browsedirection in which is searched.</param>
        /// <param name="nodeClassMask">The node classes for which is searched.</param>
        /// <param name="referenceTypeIds">The nodeId of the References for that is searched.</param>
        /// <param name="includeSubTypes">A boolean that indicates if Subtypes of the ReferenceType should be searched as well.</param>
        /// <param name="localNodeId"> The browsed local NodeId.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryBrowseLocalNodeId(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, out NodeId? localNodeId);
        /// <summary>
        /// Browses the first local NodeId that matches the given criteria.
        /// </summary>
        /// <param name="rootNodeId"Starting nodeId.></param>
        /// <param name="browseDirection">The browsedirection in which is searched.</param>
        /// <param name="nodeClassMask">The node classes for which is searched.</param>
        /// <param name="referenceTypeIds">The nodeId of the References for that is searched.</param>
        /// <param name="includeSubTypes">A boolean that indicates if Subtypes of the ReferenceType should be searched as well.</param>
        /// <param name="browseName">The browsename that is searched for.</param>
        /// <param name="localNodeId"> The browsed local NodeId.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryBrowseLocalNodeIdWithBrowseName(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, QualifiedName browseName, out NodeId? localNodeId);
        /// <summary>
        /// Gets the Optional And Mandatory Placeholders For TypeClass nodes.
        /// </summary>
        /// <param name="typeClassNodes">A List of TypeclassNodes for which the optional and Mandatory placeholders are retrieved.</param>
        /// <param name="optionalMandatoryPlaceholders">A List of Optional and Mandatory Placeholders for TypeClass nodes.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryGetOptionalAndMandatoryPlaceholdersForTypeClassNodes(List<TypeClassNode> typeClassNodes, out List<NodeId> optionalMandatoryPlaceholders);
        /// <summary>
        /// Gets a List of the Optional and Mandatory Placeholders for a typedefinition.
        /// </summary>
        /// <param name="typeDefinition">The nodeId of the Typedefinition.</param>
        /// <param name="optionalMandatoryPlaceholders">A List of Optional and Mandatory Placeholders for TypeClass node.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryGetOptionalAndMandatoryPlaceholders(NodeId typeDefinition, out List<NodeId> optionalMandatoryPlaceholders);
        /// <summary>
        /// Returns the TypeclassNodes for a given nodeId
        /// </summary>
        /// <param name="nodeId">The nodeId for that TypeClassnodes are generated.</param>
        /// <param name="typeClassNodes">A List of Type Class nodes.</param>
        /// <param name="pathToChild">The path to the child.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryGetTypeClassNodesForNodeId(NodeId nodeId, out List<TypeClassNode> typeClassNodes, RelativePathElementCollection? pathToChild = null);
        /// <summary>
        /// Reads a node with the given nodeId from the OpcUaServer.
        /// </summary>
        /// <param name="nodeId">NodeId of the node.</param>
        /// <returns>The red node or null if the node was not found</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public Node? ReadNode(NodeId nodeId);

        /// <summary>
        /// Reads the Datavalue of the node with the given nodeId.
        /// </summary>
        /// <param name="nodeId">The node Id from which the datavalue should be read. </param>
        /// <returns>The Datavalue for the nodeId or null if there is no DataValue.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public DataValue? ReadValue(NodeId nodeId);
        /// <summary>
        /// Connects an Opc Ua Eventlistener to the current OpcUaServer that is called when an
        /// event occurs.
        /// </summary>
        /// <param name="opcUaEventListener">The OpcUaEventlistemer to add.</param>
        /// <returns>True if the connection was successful, false otherwise.</returns>
        /// 
        public bool TryConnectEvents(OpcUaEventListener opcUaEventListener);
        /// <summary>
        /// Browses a nodeId on the server and returns the BrowseResults as an BrowseResultCollection.
        /// </summary>
        /// <param name="nodeId">The NodeId from that the browsing starts.</param>
        /// <param name="browseDirection">Defines the browsedirection.</param>
        /// <param name="nodeClassMask">Defines the nodeclasses that are returned.</param>
        /// <param name="referenceTypeIds">Defines the reference to follow.</param>
        /// <param name="includeSubTypes">Boolean that indicates if subtypes of the Relation should also be followed.</param>
        /// <returns>True if the browsing was successfull, False otherwise.</returns>
        public bool TryBrowseNode(NodeId nodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, out BrowseResultCollection browseResultColelction);
        /// <summary>
        /// Returns the Session Id of the session as a string. If the session was not initialized an empty string is returned.
        /// </summary>
        /// <returns>Returns the Session Id of the Opc Session or an empty string if the session was not initialized</returns>
        public string GetSessionId();
        /// <summary>
        /// Returns the Session Name of the Opc Ua session as a string. If the session was not initialized an empty string is returned.
        /// </summary>
        /// <returns>Returns the Session Name of the Opc Session or an empty string if the session was not initialized</returns>
        public string GetSessionName();
        /// <summary>
        /// Returns the Opc Ua Session.
        /// </summary>
        /// <returns>The currently active Opc Ua Session.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public Opc.Ua.Client.Session? GetSession();
        /// <summary>
        /// Returns the NamespaceTable from the Server;
        /// </summary>
        /// <returns>True if the namespacetable could be retrieved from the server false if not.</returns>
        public bool TryGetNamespaceTable(out NamespaceTable namespaceTable);
        /// <summary>
        /// Subscribes a List of NodeIds with a Subscription to the Opc Ua Server;
        /// </summary>
        /// <param name="nodeIds">The NodeIds that should be subscribed.</param>
        /// <param name="eventHandler">The Eventhandler that is called when the subscription triggers.</param>
        /// <param name="subscriptionId"> The subscriptionId.</param>
        /// <returns>True if the subscription was successful. False otherwise.</returns>
        public bool TrySubscribeToDataChanges(List<NodeId> nodeIds, MonitoredItemNotificationEventHandler eventHandler, out uint subscriptionId);
        /// <summary>
        /// Clears the subscriptions of the client.
        /// </summary>
        public void ClearSubscriptions();
        /// <summary>
        /// Browses all hierarchical subtypes of a node.
        /// </summary>
        /// <param name="nodeId">The typeNodeid for that the subtypes should be retrieved.</param>
        /// <param name="subTypeList">Possible sub types including the node type itself.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryBrowseAllHierarchicalSubType(NodeId nodeId, List<NodeId> subTypeList);
        /// <summary>
        /// Checks if the Session is still connected and returns it.
        /// </summary>
        /// <param name="session">The current Session.</param>
        /// <returns>True if the Session is still connected. False otherwise.</returns>
        public bool TryCheckSession(out Session? session);
        /// <summary>
        /// Internal Connection Task.
        /// </summary>
        /// <returns>True for success, false otherwise.</returns>
        public Task<bool> ConnectAsync();
        /// <summary>
        /// Browses NodeIds matching the included BrowseDescriptionCollection, excluding nodes matching the excluded BrowseDescriptionCollection.
        /// </summary>
        /// <param name="included">BrowseDescriptions that should be browsed for include.</param>
        /// <param name="browsedNodeIds">The browsed NodeIds.</param>
        /// <param name="excluded">BrowseDescriptions that should be browsed for exclude.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryBrowseNodeIds(BrowseDescriptionCollection included, out List<NodeId> browsedNodeIds, BrowseDescriptionCollection? excluded = null);
        /// <summary>
        /// Browses the First NodeId matching the included BrowseDescriptionCollection, excluding nodes matching the excluded BrowseDescriptionCollection.
        /// </summary>
        /// <param name="included">BrowseDescriptions that should be browsed for include.</param>
        /// <param name="browsedNodeId">The first browsed NodeId.</param>
        /// <param name="excluded">BrowseDescriptions that should be browsed for exclude.</param>
        /// <returns>True if the Browse was successful or false if it was not successful.</returns>
        public bool TryBrowseFirstNodeId(BrowseDescriptionCollection included, out NodeId? browsedNodeId, BrowseDescriptionCollection? excluded = null);
        /// <summary>
        /// Adds a Listener to the OpcClient.
        /// </summary>
        /// <param name="opcClientListener">The listener that is to be added.</param>
        public void AddOpcClientListener(IOpcClientListener opcClientListener);
    }
}
