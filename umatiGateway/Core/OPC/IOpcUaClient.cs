using Opc.Ua;

namespace umatiGateway.Core.OPC
{
    public interface IOpcUaClient
    {
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
        /// Reads a node with the given nodeId from the OpcUaServer.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <returns>The red node or null if the node was not found</returns>
        /// <exception cref="OpcUaException">Exception in Opc Ua Context.</exception>
        public Node? ReadNode(NodeId nodeId);
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
    }
}
