// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Components.Forms.Mapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using UmatiGateway.OPC;

namespace UmatiGateway.Pages
{
    public class UmatiMqttModel : PageModel
    {
        public ClientFactory ClientFactory;
        public string ConnectionType { get; private set; }
        public string ConnectionUrl { get; private set; }
        public string Port { get; private set; }
        public string MqttUser { get; private set; }
        public string Password { get; private set; }
        public string ClientId { get; private set; }
        public string Prefix { get; private set; }
        public string[] ConnectionTypes { get; private set; }
        public List<PublishedNode> publishedNodes { get; private set; }
        public String SessionId { get; private set; }
        public UmatiMqttModel(ClientFactory ClientFactory)
        {
            this.ClientFactory = ClientFactory;
            ConnectionType = "tcp";
            MqttUser = "fva/matthias2";
            Password = "";
            SessionId = "";
            ConnectionUrl = "localhost";
            ClientId = "fva/matthias2";
            Prefix = "Umati/v2";
            Port = "1883";
            ConnectionTypes = new string[] { "Websocket", "Tcp" };
            publishedNodes = new List<PublishedNode>();
        }

        public IActionResult OnPostConnect(string ConnectionUrl, string Port, string MqttUser, string MqttPassword, string MqttClientId, string MqttPrefix)
        {
            UmatiGatewayApp client = this.getClient();
            client.setMqttConnectionType("tcp");
            client.setMqttConnectionUrl(ConnectionUrl);
            client.setMqttConnectionPort(Port);
            client.setMqttUser(MqttUser);
            client.setMqttPassword(MqttPassword);
            client.setMqttClientId(MqttClientId);
            client.setMqttPrefix(MqttPrefix);
            client.ConnectMqtt();
            this.UpdateFromMqttClient();
            return RedirectToPage();
        }
        public IActionResult OnPostSave(string ConnectionUrl, string MqttPrefix)
        {
            UmatiGatewayApp client = this.getClient();
            client.setMqttPrefix(Prefix);
            this.UpdateFromMqttClient();
            return RedirectToPage();
        }
        public IActionResult OnPostDisconnect()
        {
            UmatiGatewayApp client = this.getClient();
            client.DisconnectMqtt();
            return RedirectToPage();
        }

        public void OnGet()
        {
            this.UpdateFromMqttClient();
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
        private void UpdateFromMqttClient()
        {
            UmatiGatewayApp client = this.getClient();
            ConnectionType = client.getMqttConnectionType();
            ConnectionUrl = client.getMqttConnectionUrl();
            Port = client.getMqttConnectionPort();
            MqttUser = client.getMqttUser();
            Password = client.getMqttPassword();
            ClientId = client.getMqttClientId();
            Prefix = client.getMqttPrefix();
            publishedNodes = client.getPublishedNodes();
        }
    }
}
