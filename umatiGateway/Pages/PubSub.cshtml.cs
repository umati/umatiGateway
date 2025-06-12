// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Components.Forms.Mapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.OPC;

namespace UmatiGateway.Pages
{
    public class PubSubModel : PageModel
    {
        public UmatiGatewayApp UmatiGatewayApp;
        public PubSubModel(ClientFactory ClientFactory)
        {
            this.UmatiGatewayApp = ClientFactory.getClient();
        }

        public IActionResult OnPostConnect(string ConnectionUrl, string Port, string MqttUser, string MqttPassword, string MqttClientId, string MqttPrefix)
        {
            this.UmatiGatewayApp.PubSubProvider.Connect();
            return RedirectToPage();
        }
        public IActionResult OnPostSave(string ConnectionUrl, string MqttPrefix)
        {
            return RedirectToPage();
        }
        public IActionResult OnPostDisconnect()
        {
            this.UmatiGatewayApp.PubSubProvider.Connect();
            return RedirectToPage();
        }
        public IActionResult OnPostRemovePubsSubNode(int index)
        {
            PublishedNode publishedNode = this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.PublishedNodes[index];
            this.UmatiGatewayApp.RemoveNodePubSubConfig(publishedNode);
            return RedirectToPage();
        }
    }
}
