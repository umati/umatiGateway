using Opc.Ua;
using Opc.Ua.Client;

namespace umatiGateway.Core.OPC
{
    public interface IOpcUaClient
    {
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
        /// Returns the TypeDefinition for a given NodeId.
        /// </summary>
        /// <param name="nodeId"> The nodeId for which the typeDefinition is browsed.</param>
        /// <returns>The typeDefinition node Id for the nodeId or null if the node has no typedefinition.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public NodeId? BrowseTypeDefinition(NodeId nodeId);
        /// <summary>
        /// Returns a List of nodeIds matching the description.
        /// </summary>
        /// <param name="nodeId">Starting nodeId.</param>
        /// <param name="browseDirection">The browsedirection in which is searched.</param>
        /// <param name="nodeClass">The nodeclass for which is searched.</param>
        /// <param name="ReferenceTypeId">The node Id of the Reference for that is searched.</param>
        /// <param name="includeSubTypes"> A boolean that indicates if Subtypes of the ReferenceType should be included.</param>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public List<NodeId> BrowseLocalNodeIds(NodeId nodeId, BrowseDirection browseDirection, uint nodeClass, NodeId ReferenceTypeId, bool includeSubTypes);
        /// <summary>
        /// Returns a List of NodeIds matching the parameters.
        /// </summary>
        /// <param name="nodeId">Starting nodeId.</param>
        /// <param name="browseDirection">The browsedirection in which is searched.</param>
        /// <param name="nodeClass">The node classes for which is searched.</param>
        /// <param name="referenceTypeId">The nodeId of the References for that is searched.</param>
        /// <param name="includeSubTypes">A boolean that indicates if Subtypes of the ReferenceType should be searched as well.</param>
        /// <param name="typeDefinition">The typedfinition of the NodeIds that should be returned.</param>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public List<NodeId> BrowseLocalNodeIdsWithTypeDefinition(NodeId nodeId, BrowseDirection browseDirection, uint nodeClass, NodeId referenceTypeId, bool includeSubTypes, NodeId typeDefinition);
        /// <summary>
        /// Returns a List of NodeIds matching the parameters.
        /// </summary>
        /// <param name="rootNodeId"> Starting nodeId.</param>
        /// <param name="browseDirection">The browsedirection in which is searched.</param>
        /// <param name="nodeClassMask">The node classes for which is searched.</param>
        /// <param name="referenceTypeIds">The nodeId of the References for that is searched.</param>
        /// <param name="includeSubTypes">A boolean that indicates if Subtypes of the ReferenceType should be searched as well.</param>
        /// <param name="excludedReferenceTypeId">The nodeId of the Reference that should be excluded.</param>
        /// <returns>Returns a List of NodeIds matching the parameters.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public List<NodeId> BrowseLocalNodeIdsExcludeReference(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, NodeId excludedReferenceTypeId);
        /// <summary>
        /// Browses the first local NodeId that matches the given criteria.
        /// </summary>
        /// <param name="rootNodeId">Starting nodeId.</param>
        /// <param name="browseDirection">The browsedirection in which is searched.</param>
        /// <param name="nodeClassMask">The node classes for which is searched.</param>
        /// <param name="referenceTypeIds">The nodeId of the References for that is searched.</param>
        /// <param name="includeSubTypes">A boolean that indicates if Subtypes of the ReferenceType should be searched as well.</param>
        /// <returns>The first NodeId that matches the criteria or null if there is no match.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public NodeId? BrowseLocalNodeId(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes);
        /// <summary>
        /// Browses the first local NodeId that matches the given criteria.
        /// </summary>
        /// <param name="rootNodeId"Starting nodeId.></param>
        /// <param name="browseDirection">The browsedirection in which is searched.</param>
        /// <param name="nodeClassMask">The node classes for which is searched.</param>
        /// <param name="referenceTypeIds">The nodeId of the References for that is searched.</param>
        /// <param name="includeSubTypes">A boolean that indicates if Subtypes of the ReferenceType should be searched as well.</param>
        /// <param name="browseName">The browsename that is searched for.</param>
        /// <returns>The first NodeId that matches the criteria or null if there is no match.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public NodeId? BrowseLocalNodeIdWithBrowseName(NodeId rootNodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes, QualifiedName browseName);
        /// <summary>
        /// Gets the Optional And Mandatory Placeholders For TypeClass nodes.
        /// </summary>
        /// <param name="typeClassNodes">A List of TypeclassNodes for which the optional and Mandatory placeholders are retrieved.</param>
        /// <returns>A List of Optional and Mandatory Placeholders for TypeClass nodes.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public List<NodeId> GetOptionalAndMandatoryPlaceholdersForTypeClassNodes(List<TypeClassNode> typeClassNodes);
        /// <summary>
        /// Gets a List of the Optional and Mandatory Placeholders for a typedefinition.
        /// </summary>
        /// <param name="typeDefinition">The nodeId of the Typedefinition.</param>
        /// <returns>A List containing the Optional and MandatoryPlaceholders for a typedefinition.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public List<NodeId> GetOptionalAndMandatoryPlaceholders(NodeId typeDefinition);
        /// <summary>
        /// Returns the TypeclassNodes for a given nodeId
        /// </summary>
        /// <param name="nodeId">The nodeId for that TypeClassnodes are generated.</param>
        /// <param name="pathToChild">The path to the child.</param>
        /// <returns>A List of Type Class nodes.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public List<TypeClassNode> GetTypeClassNodesForNodeId(NodeId nodeId, RelativePathElementCollection? pathToChild = null);
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
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        /// 
        public void ConnectEvents(OpcUaEventListener opcUaEventListener);
        /// <summary>
        /// Browses a nodeId on the server and returns the BrowseResults as an BrowseResultCollection.
        /// </summary>
        /// <param name="nodeId">The NodeId from that the browsing starts.</param>
        /// <param name="browseDirection">Defines the browsedirection.</param>
        /// <param name="nodeClassMask">Defines the nodeclasses that are returned.</param>
        /// <param name="referenceTypeIds">Defines the reference to follow.</param>
        /// <param name="includeSubTypes">Boolean that indicates if subtypes of the Relation should also be followed.</param>
        /// <returns>The BrowseResultCollection as the result of the Browsing.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public BrowseResultCollection BrowseNode(NodeId nodeId, BrowseDirection browseDirection, uint nodeClassMask, NodeId referenceTypeIds, bool includeSubTypes);
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
        /// <returns>The Namespacetable of the OPC Ua Server.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public NamespaceTable GetNamespaceTable();
        /// <summary>
        /// Subscribes a List of NodeIds with a Subscription to the Opc Ua Server;
        /// </summary>
        /// <param name="nodeIds">The NodeIds that should be subscribed.</param>
        /// <param name="eventHandler">The Eventhandler that is called when the subscription triggers.</param>
        /// <returns>The subscriptionId.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public uint SubscribeToDataChanges(List<NodeId> nodeIds, MonitoredItemNotificationEventHandler eventHandler);
        /// <summary>
        /// Clears the subscriptions of the client.
        /// </summary>
        public void ClearSubscriptions();
        /// <summary>
        /// Browses all hierarchical subtypes of a node.
        /// </summary>
        /// <param name="nodeId">The typeNodeid for that the subtypes should be retrieved.</param>
        /// <param name="subTypeList">Possible sub types including the node type itself.</param>
        /// <returns>A List of the Type with its subtypes.</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public List<NodeId> BrowseAllHierarchicalSubType(NodeId nodeId, List<NodeId> subTypeList);
        public Session CheckSession();
        public void AddOpcClientListener(IOpcClientListener opcClientListener);
        public Task<bool> ConnectAsync();
        public List<NodeId> BrowseNodeIds(BrowseDescriptionCollection included, BrowseDescriptionCollection? excluded = null);
        public NodeId? BrowseFirstNodeId(BrowseDescriptionCollection included, BrowseDescriptionCollection? excluded = null);
    }
}
