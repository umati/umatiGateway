// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json.Linq;
using NLog;
using Opc.Ua;
using System.Collections.Concurrent;
using System.Reflection;
using System.Resources;
using System.Text.Json.Nodes;
using umatiGateway.Core.OPC;
using umatiGateway.Hub;

namespace UmatiGateway.Pages
{
    [IgnoreAntiforgeryToken]
    public class OPCConnectionModel : PageModel
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
        private readonly IHubContext<SignalHub> signalHub;
        public OPCConnectionModel(ClientFactory ClientFactory, IHubContext<SignalHub> signalHub)
        {
            this.app = ClientFactory.getClient();
            this.signalHub = signalHub;
            ResourceManager rm = new ResourceManager("UmatiGateway.Pages.Ressource", Assembly.GetExecutingAssembly());
            string? Label_ConnectionUrl_Translated = rm.GetString("TestPage_Label_ConnectionUrl");
            if (Label_ConnectionUrl_Translated != null) { this.LabelConnectionUrl = Label_ConnectionUrl_Translated; }
        }

        public JsonResult OnPostConnect([FromBody] OpcConnectionParams opcConnectionParams)
        {
            try
            {
                OpcConnectionParams? local = opcConnectionParams;
                if (local != null)
                {
                    app.ActiveConfiguration.OPCConnection.ServerEndpoint = local.ConnectionUrl ?? "";
                    app.ActiveConfiguration.OPCConnection.UserName = local.User ?? "";
                    app.ActiveConfiguration.OPCConnection.Password = local.Password ?? "";
                    app.ActiveConfiguration.OPCConnection.ReadExtraLibs = Convert.ToBoolean(opcConnectionParams.UseInternalLibs);
                    app.ActiveConfiguration.OPCConnection.CertificatePath = local.CertPath ?? "";
                    app.ActiveConfiguration.OPCConnection.CertificatePassword = local.CertPwd ?? "";
                }
                IOpcUaClient client = app.OpcUaClient;
                client.Connect();
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Fehler: {ex.Message}" });
            }
        }
        public IActionResult OnPostDisconnect()
        {
            try
            {
                IOpcUaClient client = app.OpcUaClient;
                client.Disconnect();
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Fehler: {ex.Message}" });
            }
        }

        public JsonResult OnGetOpcClientState()
        {
            JsonObject jObj = new JsonObject();
            List<OpcUaClientState> opcUaClientStateHistory = this.app.OpcUaClient.GetClientStateHistory();
            OpcUaClientState lastClientState = opcUaClientStateHistory.Last();
            JsonArray jConnectionStateHistory = new JsonArray();
            foreach (OpcUaClientState clientState in opcUaClientStateHistory)
            {
                JsonObject jClientState = new JsonObject();
                jClientState.Add("ConnectionState", clientState.ConnectionState.ToString());
                jClientState.Add("Detail", clientState.Detail);
                jConnectionStateHistory.Add(jClientState);
            }
            jObj.Add("OpcServerEndpoint", this.app.ActiveConfiguration.OPCConnection.ServerEndpoint);
            jObj.Add("OpcUser", this.app.ActiveConfiguration.OPCConnection.UserName);
            jObj.Add("OpcPassword", this.app.ActiveConfiguration.OPCConnection.Password);
            jObj.Add("UseInternalLibs", this.app.ActiveConfiguration.OPCConnection.ReadExtraLibs);
            jObj.Add("CertPath", this.app.ActiveConfiguration.OPCConnection.CertificatePath);
            jObj.Add("CertPwd", this.app.ActiveConfiguration.OPCConnection.CertificatePassword);
            jObj.Add("ConnectionState", this.app.OpcUaClient.GetClientState().ConnectionState.ToString());
            jObj.Add("ConnectionDetails", this.app.OpcUaClient.GetClientState().Detail);
            jObj.Add("ConnectionStateHistory", jConnectionStateHistory);
            jObj.Add("OpcSessionName", this.app.OpcUaClient.GetSessionName());
            jObj.Add("OpcSessionId", this.app.OpcUaClient.GetSessionId());
            jObj.Add("Blocked", this.app.OpcUaClient.GetClientState().IsBlocked);
            return new JsonResult(jObj);
        }
    }
    public class OpcConnectionParams
    {
        public string? ConnectionUrl { get; set; }
        public string? User { get; set; }
        public string? Password { get; set; }
        public string? UseInternalLibs { get; set; }
        public string? CertPath { get; set; }
        public string? CertPwd { get; set; }
    }
}
