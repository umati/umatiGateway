namespace UmatiGateway.OPC
{
    public interface UmatiGatewayAppListener
    {
        public void blockingTransitionChanged(BlockingTransition blockingTransition);
    }
}
