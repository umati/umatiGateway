// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Components.Forms.Mapping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Opc.Ua;
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

        public IActionResult OnPostConnect(string ConnectionUrl, string MqttUser, string MqttPassword, string MqttClientId, string MqttPrefix,
            bool? PubSubAllowUntrustedCertificates, double PublishInterval, double MetaDataUpdateTime, string JsonEncodingType)
        {
            this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.ServerEndpoint = ConnectionUrl;
            this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.UserName = MqttUser;
            this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.Password = MqttPassword;
            this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.ClientId = MqttClientId;
            this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.Prefix = MqttPrefix;
            this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.AllowUntrustedCertificates = PubSubAllowUntrustedCertificates == true;
            this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.PublishInterval = PublishInterval;
            this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.MetaDataUpdateTime = MetaDataUpdateTime;
            if (Enum.TryParse<JsonEncoding>(JsonEncodingType, out var encoding))
            {
                this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.JsonEncoding = encoding;
            }
            else
            {
                this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.JsonEncoding = JsonEncoding.LEGACY;
            }
            this.UmatiGatewayApp.PubSubProvider.Connect();
            return RedirectToPage();
        }
        public IActionResult OnPostSave(string ConnectionUrl, string MqttPrefix)
        {
            return RedirectToPage();
        }
        public IActionResult OnPostDisconnect()
        {
            this.UmatiGatewayApp.PubSubProvider.Disconnect();
            return RedirectToPage();
        }
        public IActionResult OnPostRemovePubSubNode(int index)
        {
            PublishedNode publishedNode = this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.PublishedNodes[index];
            this.UmatiGatewayApp.ActiveConfiguration.PubSubProviderConfig.PublishedNodes.Remove(publishedNode);
            return RedirectToPage();
        }
    }
}
