using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using System.Collections.Concurrent;
using System.Reflection;
using System.Resources;
using UmatiGateway.OPC;

namespace UmatiGateway.Pages
{
    public class OPCConnectionModel : PageModel, UmatiGatewayAppListener
    {
        public string LabelConnectionUrl { get; } = "Connection URL:";
        public string LabelSessionId { get; } = "SessionId:";
        public string LabelOPCSessionName { get; } = "OPCSessionName:";
        public string LabelOPCSessionId { get; } = "OPCSessionId:";
        public string LabelConnectionStatus { get; } = "ConnectionStatus:";
        public string ConnectionUrl { get; private set; } = "";
        public ClientFactory ClientFactory;
        public string SessionId { get; private set; } = "";
        public string OPCSessionName { get; private set; } = "";
        public string OPCSessionId { get; private set; } = "";
        public string ConnectionStatus { get; private set; } = "Not Connected";

        private static readonly BlockingCollection<string> UpdateQueue = new BlockingCollection<string>();
        public OPCConnectionModel(ClientFactory ClientFactory)
        {
            this.ClientFactory = ClientFactory;
            ResourceManager rm = new ResourceManager("UmatiGateway.Pages.TestPage", Assembly.GetExecutingAssembly());
            string? Label_ConnectionUrl_Translated = rm.GetString("TestPage_Label_ConnectionUrl");
            if (Label_ConnectionUrl_Translated != null) { this.LabelConnectionUrl = Label_ConnectionUrl_Translated; }
        }

        public IActionResult OnPostConnect(String ConnectionUrl)
        {
            this.ConnectionUrl = ConnectionUrl;
            UmatiGatewayApp client = this.getClient();
            if (ConnectionUrl != null)
            {
                _ = client.ConnectAsync(this.ConnectionUrl).Result;
            }
            this.UpdateClientData();
            return new PageResult();
        }
        public IActionResult OnPostDisconnect()
        {
            UmatiGatewayApp client = this.getClient();
            client.Disconnect();
            this.UpdateClientData();
            return new PageResult();
        }
        public void OnGet()
        {
            this.UpdateClientData();
        }
        private UmatiGatewayApp getClient()
        {
            string? mySessionId = HttpContext.Session.GetString("SessionId");
            if (mySessionId == null)
            {
                this.SessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("SessionId", this.SessionId);
            }
            else
            {
                this.SessionId = mySessionId;
            }
            UmatiGatewayApp client = ClientFactory.getClient(this.SessionId);
            return client;
        }
        private void UpdateClientData()
        {
            string? mySessionId = HttpContext.Session.GetString("SessionId");
            if (mySessionId == null)
            {
                this.SessionId = string.Empty;
                this.OPCSessionId = string.Empty;
                this.ConnectionStatus = string.Empty;
                this.OPCSessionName = string.Empty;
            }
            else
            {
                UmatiGatewayApp client = ClientFactory.getClient(mySessionId);
                client.AddUmatiGatewayAppListener(this);
                this.SessionId = mySessionId;
                if (client.Session != null)
                {
                    this.OPCSessionId = client.Session.SessionId.ToString();
                    this.ConnectionStatus = client.Session.Connected.ToString();
                    this.OPCSessionName = client.Session.SessionName;

                }
                this.ConnectionUrl = client.getOpcConnectionUrl();
            }
        }
        public IActionResult OnGetStreamUpdates()
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            try
            {
                // Loop indefinitely, checking for updates
                foreach (var update in UpdateQueue.GetConsumingEnumerable())
                {
                    if (Response.HttpContext.RequestAborted.IsCancellationRequested)
                        break; // Exit if the client disconnects

                    var message = $"data: {update}\n\n";
                    Response.WriteAsync(message);
                    Response.Body.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SSE connection: {ex.Message}");
            }

            return new EmptyResult();
        }

        public void blockingTransitionChanged(BlockingTransition blockingTransition)
        {
            var bt = new
            {
                transition = blockingTransition.Transition,
                message = blockingTransition.Message,
                detail = blockingTransition.Detail,
                isBlocking = blockingTransition.isBlocking
            };
            string jsonData = System.Text.Json.JsonSerializer.Serialize(bt);
            UpdateQueue.Add(jsonData); // Add update to the queue
        }
    }
}
