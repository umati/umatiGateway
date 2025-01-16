using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using System.Reflection;
using System.Resources;
using UmatiGateway.OPC;

namespace UmatiGateway.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private ClientFactory clientFactory;

        public IndexModel(ILogger<IndexModel> logger, ClientFactory ClientFactory)
        {
            _logger = logger;
            this.clientFactory = ClientFactory;
        }

        public void OnGet()
        {
            UmatiGatewayApp client = this.getClient(clientFactory);
            //client.StartUp();
        }
        private UmatiGatewayApp getClient(ClientFactory clientFactory)
        {
            string SessionId = "";
            string? mySessionId = HttpContext.Session.GetString("SessionId");
            if (mySessionId == null)
            {
                SessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("SessionId", SessionId);
            }
            else
            {
                SessionId = mySessionId;
            }
            UmatiGatewayApp client = clientFactory.getClient(SessionId);
            return client;
        }
    }
}
