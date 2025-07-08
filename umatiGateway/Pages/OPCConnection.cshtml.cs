// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using NLog;
using System.Collections.Concurrent;
using System.Reflection;
using System.Resources;
using umatiGateway.Core.OPC;

namespace UmatiGateway.Pages
{
    public class OPCConnectionModel : PageModel, UmatiGatewayAppListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public string LabelConnectionUrl { get; } = "Connection URL:";
        public string LabelSessionId { get; } = "SessionId:";
        public string LabelOPCSessionName { get; } = "OPCSessionName:";
        public string LabelOPCSessionId { get; } = "OPCSessionId:";
        public string LabelConnectionStatus { get; } = "ConnectionStatus:";
        public string OpcUser { get; private set; } = "";
        public string OpcPassword { get; private set; } = "";
        public string SessionId { get; private set; } = "";
        public string OPCSessionName { get; private set; } = "";
        public string OPCSessionId { get; private set; } = "";
        public string ConnectionStatus { get; private set; } = "Not Connected";
        public UmatiGatewayApp app { get; set; }

        private static readonly BlockingCollection<string> UpdateQueue = new BlockingCollection<string>();
        public OPCConnectionModel(ClientFactory ClientFactory)
        {
            this.app = ClientFactory.getClient();
            ResourceManager rm = new ResourceManager("UmatiGateway.Pages.Ressource", Assembly.GetExecutingAssembly());
            string? Label_ConnectionUrl_Translated = rm.GetString("TestPage_Label_ConnectionUrl");
            if (Label_ConnectionUrl_Translated != null) { this.LabelConnectionUrl = Label_ConnectionUrl_Translated; }
        }

        public IActionResult OnPostConnect(String ConnectionUrl, String OpcUser, String OpcPassword)
        {
            app.ActiveConfiguration.OPCConnection.ServerEndpoint = ConnectionUrl;
            app.ActiveConfiguration.OPCConnection.UserName = OpcUser ?? "";
            app.ActiveConfiguration.OPCConnection.Password = OpcPassword ?? "";
            IOpcUaClient client = app.OpcUaClient;
            if (ConnectionUrl != null)
            {
                client.Connect();
            }
            return new PageResult();
        }
        public IActionResult OnPostDisconnect()
        {
            IOpcUaClient client = app.OpcUaClient;
            client.Disconnect();
            return new PageResult();
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
                Logger.Info($"Error in SSE connection: {ex.Message}");
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
