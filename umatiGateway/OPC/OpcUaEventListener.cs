using Opc.Ua;

namespace UmatiGateway.OPC
{
    public interface OpcUaEventListener
    {
        public void ModelChangeEvent(NodeId affectedNode);
    }
}
