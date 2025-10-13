using Opc.Ua;

namespace umatiGateway.Core.External.Opc.Ua.PubSub.Encoding
{
    public static class JsonEncodingConfiguration
    {
        public static bool UseCustomizedEncoding { get; set; } = false;
        public static JsonEncodingType jsonEncodingType { get; set; } = JsonEncodingType.Reversible;
    }
}
