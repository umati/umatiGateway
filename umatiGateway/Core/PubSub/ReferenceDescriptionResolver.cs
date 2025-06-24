using Microsoft.AspNetCore.Mvc.Razor.Infrastructure;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using Opc.Ua.Client;
using Org.BouncyCastle.Tls.Crypto;
using umatiGateway.Core.OPC;

namespace umatiGateway.Core.PubSub
{
    public class ReferenceDescriptionResolver
    {
        IOpcUaClient client;
        List<NodeId> referenceTypeIds = new List<NodeId>();
        public ReferenceDescriptionResolver(IOpcUaClient client)
        {
            this.client = client;
        }

        public KeyValuePairCollection ResolveReferences(HierarchicalNode hierarchicalNode)
        {
            KeyValuePairCollection keyValuePairs = new KeyValuePairCollection();
            int counter = 0;
            Dictionary <NodeId, ReferenceDescriptionCollection> forwardReferences = new Dictionary<NodeId, ReferenceDescriptionCollection>();
            Dictionary<NodeId, ReferenceDescriptionCollection> backwardReferences = new Dictionary<NodeId, ReferenceDescriptionCollection>();
            List<NodeId> refTypeIds = GetReferenceTypeIdsToSearch();
            BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
            foreach(NodeId refTypeId in refTypeIds)
            {
                BrowseDescription nodeToBrowse = new BrowseDescription();
                nodeToBrowse.NodeId = hierarchicalNode.NodeId;
                nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
                nodeToBrowse.NodeClassMask = (int)NodeClass.DataType | (int)NodeClass.Object | (int)NodeClass.Variable | (int)NodeClass.VariableType | (int)NodeClass.ObjectType;
                nodeToBrowse.ReferenceTypeId = refTypeId;
                nodeToBrowse.IncludeSubtypes = false;
                nodesToBrowse.Add(nodeToBrowse);
                BrowseDescription nodeToBrowseBackward = new BrowseDescription();
                nodeToBrowseBackward.NodeId = hierarchicalNode.NodeId;
                nodeToBrowseBackward.BrowseDirection = BrowseDirection.Inverse;
                nodeToBrowseBackward.NodeClassMask = (int)NodeClass.DataType | (int)NodeClass.Object | (int)NodeClass.Variable | (int)NodeClass.VariableType | (int)NodeClass.ObjectType;
                nodeToBrowseBackward.ReferenceTypeId = refTypeId;
                nodeToBrowseBackward.IncludeSubtypes = false;
                nodesToBrowse.Add(nodeToBrowseBackward);
            }
            Session? session = client.GetSession();
            if (session != null)
            {
                session.Browse(null, null, 10000, nodesToBrowse, out BrowseResultCollection browseResultCollection, out DiagnosticInfoCollection diagnosticInfos);

                for(int i = 0; i < browseResultCollection.Count; i++)
                {
                    BrowseResult browseResult = browseResultCollection[i];
                    ReferenceDescriptionCollection referenceDescriptionCollection = browseResult.References;
                    foreach (ReferenceDescription referenceDescription in referenceDescriptionCollection)
                    {
                        counter++;
                        Opc.Ua.KeyValuePair keyValuePair = new Opc.Ua.KeyValuePair();
                        keyValuePair.Key = new QualifiedName($"relation_{counter}", 0);
                        referenceDescription.ReferenceTypeId = nodesToBrowse[i].ReferenceTypeId;
                        referenceDescription.IsForward = nodesToBrowse[i].BrowseDirection == BrowseDirection.Forward ? true : false;
                        Node? node = client.ReadNode(ExpandedNodeId.ToNodeId(referenceDescription.NodeId, client.GetNamespaceTable()));
                        if(node != null)
                        {
                            referenceDescription.BrowseName = node.BrowseName;
                            referenceDescription.DisplayName = node.DisplayName;
                            referenceDescription.NodeClass = node.NodeClass;
                            referenceDescription.TypeDefinition = node.TypeDefinitionId;
                        }
                        keyValuePair.Value = new Variant(referenceDescription);
                        keyValuePairs.Add(keyValuePair);
                    }
                }
            }
            return keyValuePairs;
        }
        private List<NodeId> GetReferenceTypeIdsToSearch()
        {
            if(referenceTypeIds.Count == 0)
            {
                GetReferenceSubTypes(ReferenceTypeIds.References, referenceTypeIds);
            }
            return referenceTypeIds;
        }
        private void GetReferenceSubTypes(NodeId parentNodeId, List<NodeId> result)
        {

            List<NodeId> children = client.BrowseLocalNodeIds(parentNodeId, BrowseDirection.Forward, (uint)NodeClass.ReferenceType, ReferenceTypeIds.HasSubtype, false);
            foreach (NodeId child in children)
            {
                if (!result.Contains(child))
                {
                    result.Add(child);
                    GetReferenceSubTypes(child, result);
                }
            }
        }
    }
}
