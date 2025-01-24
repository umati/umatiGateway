namespace UmatiGateway.OPC.CustomEncoding
{
    /// <summary>
    /// This Exception is used if any error happened during Custom Decoding of an DataType.
    /// </summary>
    public class CustomEncodingException : Exception
    {
        public CustomEncodingException()
        {
        }

        public CustomEncodingException(string message)
            : base(message)
        {
        }

        public CustomEncodingException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}