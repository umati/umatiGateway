using Opc.Ua;
using Opc.Ua.Client;

namespace umatiGateway.Core.OPC
{
    public class BrowseTreeController
    {
        public Tree BrowseTree = new Tree();
        private IOpcUaClient client;
        public BrowseTreeController(IOpcUaClient client)
        {
            this.client = client;
        }
        public void BrowseRootNode()
        {
            if (!BrowseTree.Initialized)
            {
                Node? node = this.client.ReadNode(ObjectIds.RootFolder);
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

        public void BrowseSelectedTreeNode(TreeNode TreeNode)
        {
            Session session = this.client.CheckSession();
            BrowseDescription nodeToBrowse = new BrowseDescription();
            nodeToBrowse.NodeId = TreeNode.NodeData.node.NodeId;
            nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
            nodeToBrowse.NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable | (int)NodeClass.Method | (int)NodeClass.ObjectType | (int)NodeClass.VariableType | (int)NodeClass.DataType | (int)NodeClass.ReferenceType; ;
            nodeToBrowse.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences;
            nodeToBrowse.IncludeSubtypes = true;
            nodeToBrowse.ResultMask = (int)BrowseResultMask.All;
            BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
            nodesToBrowse.Add(nodeToBrowse);
            session.Browse(null, null, 100, nodesToBrowse, out BrowseResultCollection browseResults, out DiagnosticInfoCollection diagnosticInfos);
            foreach (BrowseResult browseResult in browseResults)
            {
                ReferenceDescriptionCollection references = browseResult.References;
                foreach (ReferenceDescription reference in references)
                {
                    NodeId nodeId = new NodeId(reference.NodeId.Identifier, reference.NodeId.NamespaceIndex);
                    Node? node = this.client.ReadNode(nodeId);
                    if (node != null)
                    {
                        NodeData nodeData = new NodeData(node);
                        TreeNode treeNode = new TreeNode(nodeData);
                        TreeNode.children.AddLast(treeNode);
                        BrowseTree.uids.Add(treeNode.uid, treeNode);
                    }
                }
            }
        }
    }
}
