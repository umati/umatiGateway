// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.OPC;

namespace UmatiGateway.Pages
{
    public class UmatiMqttModel : PageModel
    {
        public UmatiGatewayApp UmatiGatewayApp { get; set; }
        public UmatiMqttModel(ClientFactory ClientFactory)
        {
            this.UmatiGatewayApp = ClientFactory.getClient();
        }

        public IActionResult OnPostConnect(string ConnectionUrl, string MqttUser, string MqttPassword, string MqttClientId, string MqttPrefix,
            string ServerCertificatePath, string CustomCaCertificatePath)
        {
            this.UmatiGatewayApp.ActiveConfiguration.MqttProviderConfig.ServerEndpoint = ConnectionUrl;
            this.UmatiGatewayApp.ActiveConfiguration.MqttProviderConfig.UserName = MqttUser;
            this.UmatiGatewayApp.ActiveConfiguration.MqttProviderConfig.Password = MqttPassword;
            this.UmatiGatewayApp.ActiveConfiguration.MqttProviderConfig.ClientId = MqttClientId;
            this.UmatiGatewayApp.ActiveConfiguration.MqttProviderConfig.Prefix = MqttPrefix;
            this.UmatiGatewayApp.ActiveConfiguration.MqttProviderConfig.ServerCertificatePath = ServerCertificatePath;
            this.UmatiGatewayApp.ActiveConfiguration.MqttProviderConfig.CustomCaCertificatePath = CustomCaCertificatePath;
            this.UmatiGatewayApp.MqttProvider.Connect();
            return RedirectToPage();
        }
        public IActionResult OnPostDisconnect()
        {
            this.UmatiGatewayApp.MqttProvider.Disconnect();
            return RedirectToPage();
        }
        public IActionResult OnPostRemoveNodeMqttConfig(int index)
        {
            PublishedNode publishedNode = this.UmatiGatewayApp.ActiveConfiguration.MqttProviderConfig.PublishedNodes[index];
            this.UmatiGatewayApp.ActiveConfiguration.MqttProviderConfig.PublishedNodes.Remove(publishedNode);
            return RedirectToPage();
        }
    }
}
