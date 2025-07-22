using Microsoft.AspNetCore.SignalR;
using NLog;
using umatiGateway.Core.OPC;

namespace umatiGateway.Hub
{
    public class SignalHubBridge : IOpcClientListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IHubContext<SignalHub> hub;

        public SignalHubBridge(IHubContext<SignalHub> hub, ClientFactory clientFactory)
        {
            this.hub = hub;
            clientFactory.getClient().OpcUaClient.AddOpcClientListener(this);
        }

        public void Change()
        {
            Task.Run(async () =>
            {
                try
                {
                    await this.hub.Clients.All.SendAsync("OpcClientChange");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "SignalR Push failed.");
                }
            });
        }
    }
}
