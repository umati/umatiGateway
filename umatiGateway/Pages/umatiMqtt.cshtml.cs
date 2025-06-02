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
    public class UmatiMqttModel : PageModel
    {
        public UmatiGatewayApp UmatiGatewayApp { get; set; }
        public UmatiMqttModel(ClientFactory ClientFactory)
        {
            this.UmatiGatewayApp = ClientFactory.getClient();
        }

        public IActionResult OnPostConnect(string ConnectionUrl, string Port, string MqttUser, string MqttPassword, string MqttClientId, string MqttPrefix)
        {
            this.UmatiGatewayApp.MqttProvider.Connect();
            return RedirectToPage();
        }
        public IActionResult OnPostDisconnect()
        {
            this.UmatiGatewayApp.MqttProvider.Disconnect();
            return RedirectToPage();
        }
    }
}
