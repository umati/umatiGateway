using Newtonsoft.Json.Linq;
using Opc.Ua;
using Opc.Ua.Client;

namespace UmatiGateway.OPC
{
    public class NodeData
    {
        public Node node { get; set; }
        public bool isexpanded { get; set; }

        public JObject DataValue { get; set; }
        public NodeData(Node node)
        {
            isexpanded = false;
            this.node = node;
            DataValue = new JObject();
        }
        public NodeData()
        {
            isexpanded = false;
            this.node = new Node();
            DataValue = new JObject();
        }
    }
}
