namespace umatiGateway.Core.OPC
{
    /// <summary>
    /// Exception class used in the OpcUaClient interface.
    /// </summary>
    public class OpcUaException : Exception
    {
        public OpcUaException() { }

        public OpcUaException(string message)
            : base(message) { }

        public OpcUaException(string message, Exception inner)
            : base(message, inner) { }
    }
}
