using Opc.Ua;

namespace umatiGateway.Core.OPC
{
    public class TypeClassNode
    {
        public RelativePathElementCollection RelativePathElements { get; set; }
        public NodeId StartNodeId { get; set; }
        public TypeClassNode(NodeId StartNodeId, RelativePathElementCollection RelativePathElements)
        {
            this.StartNodeId = StartNodeId;
            this.RelativePathElements = RelativePathElements;
        }
    }
}
