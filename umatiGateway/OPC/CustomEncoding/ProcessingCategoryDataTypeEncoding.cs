using Newtonsoft.Json.Linq;
using Opc.Ua;
using UmatiGateway.OPC.CustomEncoding;

namespace umatiGateway.OPC.CustomEncoding
{
    public class ProcessingCategoryDataTypeEncoding : ICustomEncoding
    {
        private ExpandedNodeId ProcessingCategoryEncodingId = new ExpandedNodeId(new NodeId(3014), "http://opcfoundation.org/UA/Glass/Flat/v2/");
        public string FullFieldName { get => "ProcessingCategoryDataTypeEncoding"; }
        public ExpandedNodeId NodeId { get => ProcessingCategoryEncodingId; }
        public ProcessingCategoryDataTypeEncoding()
        {

        }
        public JObject? decode(ExtensionObject eto)
        {
            ExtensionObjectEncoding encoding = eto.Encoding;
            switch (encoding)
            {
                case ExtensionObjectEncoding.Binary: return this.decodeBinary(eto);
                case ExtensionObjectEncoding.Xml: throw new CustomEncodingException($"Unable to decode ProcessingCategory. Xml decoding not implemented.");
                case ExtensionObjectEncoding.Json: throw new CustomEncodingException($"Unable to decode ProcessingCategory. Json decoding not implemented.");
                case ExtensionObjectEncoding.None: throw new CustomEncodingException($"Unable to decode ProcessingCategory. Encoding set to NONE.");
                case ExtensionObjectEncoding.EncodeableObject: throw new CustomEncodingException($"Unable to decode ProcessingCategory. EncodeableObject not implemented.");
                default: throw new CustomEncodingException($"Unable to decode ProcessingCategory. No Encoding given.");
            }
        }
        public ExtensionObject? encode(JObject jObject, ExtensionObjectEncoding encoding)
        {
            switch (encoding)
            {
                case ExtensionObjectEncoding.Binary: throw new CustomEncodingException($"Unable to encode ProcessingCategory. Binary encoding not implemented.");
                case ExtensionObjectEncoding.Xml: throw new CustomEncodingException($"Unable to encode ProcessingCategory. Xml encoding not implemented.");
                case ExtensionObjectEncoding.Json: throw new CustomEncodingException($"Unable to encode ProcessingCategory. Json encoding not implemented.");
                case ExtensionObjectEncoding.None: throw new CustomEncodingException($"Unable to encode ProcessingCategory. Encoding set to None.");
                case ExtensionObjectEncoding.EncodeableObject: throw new CustomEncodingException($"Unable to encode ProcessingCategory. EncodeableObjecy encoding not implemented.");
                default: throw new CustomEncodingException($"Unable to encode ProcessingCategory. Mo encoding given.");
            }
        }
        private JObject? decodeBinary(ExtensionObject eto)
        {
            JObject jObject = new JObject();
            BinaryDecoder binaryDecoder = new BinaryDecoder((byte[])eto.Body, ServiceMessageContext.GlobalContext);
            jObject.Add("ID", binaryDecoder.ReadString("ID"));
            jObject.Add("Description", binaryDecoder.ReadString("Description"));
            int length = binaryDecoder.ReadInt32("length");
            JArray array = new JArray();
            for (int i = 0; i < length; i++)
            {
                JObject supPar = new JObject();
                supPar.Add("Name", binaryDecoder.ReadString("Name"));
                supPar.Add("Description", binaryDecoder.ReadString("Description"));
                //ValueType
                JObject valueType = new JObject();
                valueType.Add("Name", binaryDecoder.ReadString("Name"));
                valueType.Add("Description", binaryDecoder.ReadString("Description"));
                valueType.Add("BaseUnit", binaryDecoder.ReadString("BaseUnit"));
                valueType.Add("PossibleValue", binaryDecoder.ReadString("PossibleValue"));
                supPar.Add("ValueType", valueType);
                supPar.Add("TypicalValue", binaryDecoder.ReadString("TypicalValue"));
                supPar.Add("Mandatory", binaryDecoder.ReadBoolean("Mandatory"));
                //Eclass
                JObject eclass = new JObject();
                eclass.Add("ID", binaryDecoder.ReadString("ID"));
                eclass.Add("Description", binaryDecoder.ReadString("Description"));
                eclass.Add("EClass", binaryDecoder.ReadString("Eclass"));
                supPar.Add("EClass", eclass);
                array.Add(supPar);
            }
            jObject.Add("SupportedParameter", array);
            length = binaryDecoder.ReadInt32("length");
            array = new JArray();
            for (int i = 0; i < length; i++)
            {
                array.Add(binaryDecoder.ReadString("SupportedAssignment"));
            }
            jObject.Add("SupportedAssignment", array);
            length = binaryDecoder.ReadInt32("length");
            array = new JArray();
            for (int i = 0; i < length; i++)
            {
                JObject supVar = new JObject();
                supVar.Add("Name", binaryDecoder.ReadString("Name"));
                supVar.Add("Description", binaryDecoder.ReadString("Description"));
                //ValueType
                JObject valueType = new JObject();
                valueType.Add("Name", binaryDecoder.ReadString("Name"));
                valueType.Add("Description", binaryDecoder.ReadString("Description"));
                valueType.Add("BaseUnit", binaryDecoder.ReadString("BaseUnit"));
                valueType.Add("PossibleValue", binaryDecoder.ReadString("PossibleValue"));
                supVar.Add("ValueType", valueType);
                supVar.Add("TypicalValue", binaryDecoder.ReadString("TypicalValue"));
                supVar.Add("Mandatory", binaryDecoder.ReadBoolean("Mandatory"));
                //Eclass
                JObject eclass = new JObject();
                eclass.Add("ID", binaryDecoder.ReadString("ID"));
                eclass.Add("Description", binaryDecoder.ReadString("Description"));
                eclass.Add("EClass", binaryDecoder.ReadString("Eclass"));
                supVar.Add("EClass", eclass);
                array.Add(supVar);
            }
            jObject.Add("SupportedVariable", array);
            jObject.Add("SupportsTransformation", binaryDecoder.ReadInt32("SupportsTransformation"));
            jObject.Add("SupportsSubProcessing", binaryDecoder.ReadInt32("SupportsSubProcessing"));
            return jObject;
        }
    }
}
