using Newtonsoft.Json.Linq;
using Opc.Ua;

namespace UmatiGateway.OPC.CustomEncoding
{
    /// <summary>
    /// This interface is used to describe custom encodings for Datatypes.
    /// </summary>
    public interface ICustomEncoding
    {
        public string FullFieldName { get; }
        public ExpandedNodeId NodeId { get; }
        /// <summary>
        /// Decodes an ExtensionObject to a corresponding JObject.
        /// </summary>
        /// <param name="eto">The ExtensionObject that is to be decoded.</param>
        /// <returns>The decoded ExtensionObject as JObject.</returns>
        /// <exception cref="CustomEncodingException"> Is thrown if there is an error on decoding.<exception>
        public JObject? decode(ExtensionObject eto);
        /// <summary>
        /// Encodes a JObject to a corresponding ExtensionObject.
        /// </summary>
        /// <param name="jToken">The JObject that is to be encoded.</param>
        /// <param name="encoding">The encoding of the ExtensionObject.</param>
        /// <returns>The encoded Extension object.</returns>
        /// <exception cref="CustomEncodingException"> Is thrown if there is an error on encoding.<exception>
        public ExtensionObject? encode(JObject jObject, ExtensionObjectEncoding encoding);
    }
}
