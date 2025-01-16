namespace UmatiGateway.OPC
{
    public class TreeNode
    {
        public String uid { get; set; }
        public NodeData NodeData { get; set; }
        public LinkedList<TreeNode> children { get; set; }
        public Boolean IsExpanded { get; set; }

        public TreeNode(NodeData NodeData)
        {
            uid = Guid.NewGuid().ToString();
            this.NodeData = NodeData;
            children = new LinkedList<TreeNode>();
            this.IsExpanded = false;
        }
    }
}
