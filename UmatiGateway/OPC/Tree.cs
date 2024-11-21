namespace UmatiGateway.OPC
{
    public class Tree
    {
        public Dictionary<string, TreeNode> uids { get; set; }
        public String? SelectedTreeNode { get; set; }
        public LinkedList<TreeNode> children { get; set; }
        public bool Initialized { get; set; } = false;
        public Tree()
        {
            this.SelectedTreeNode = null;
            this.uids = new Dictionary<string, TreeNode>();
            this.children = new LinkedList<TreeNode>();
        }
    }
}
