using Opc.Ua;
using Opc.Ua.Client;

namespace umatiGateway.Core.OPC
{
    public class BrowseUtil
    {
        private IOpcUaClient client;
        public BrowseUtil(IOpcUaClient client)
        {
            this.client = client;
        }
        public List<NodeId> BrowseNodeIds(BrowseDescriptionCollection included, BrowseDescriptionCollection? excluded = null)
        {
            List<NodeId> resultNodeIds = new List<NodeId>();
            int includedCount = included.Count;
            int excludedCount = 0;
            Session session = this.client.CheckSession();
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
    }
}
