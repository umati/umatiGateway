namespace UmatiGateway.OPC
{
    public class BlockingTransition
    {
        public string Transition = "";
        public string Message = "";
        public string Detail = "";
        public bool isBlocking = false;
        public BlockingTransition(string Transition, string Message, string Detail, bool isBlocking)
        {
            this.Transition = Transition;
            this.Message = Message;
            this.Detail = Detail;
            this.isBlocking = isBlocking;
        }
        public BlockingTransition()
        {

        }
    }
}
