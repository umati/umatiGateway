// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO.Compression;
using System.Text;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.OPC;

namespace UmatiGateway.Pages
{
    public class ConfigurationModel : PageModel
    {
        public UmatiConfiguration Configuration { get; set; }
        public ConfigurationModel(ClientFactory ClientFactory)
        {
            this.Configuration = ClientFactory.getClient().ActiveConfiguration;
        }
        public void OnGet()
        {
        }
        public IActionResult OnPostDownload(string LogLevel, bool? StartWebUI, bool? StartOPCConnection, bool? StartMqttProvider, bool? StartPubSubProvider,
            string WebURL)
        {
            Configuration.LogLevel = LogLevel;
            Configuration.StartConfiguration.StartWebUI = StartWebUI ?? false;
            Configuration.StartConfiguration.StartOPCConnection = StartOPCConnection ?? false;
            Configuration.StartConfiguration.StartMQTTProvider = StartMqttProvider ?? false;
            Configuration.StartConfiguration.StartPubSubProvider = StartPubSubProvider ?? false;
            Configuration.WebUI.URL = WebURL ?? "";
            UmatiConfigurationManager configManager = new UmatiConfigurationManager();
            var byteArray = Encoding.UTF8.GetBytes(configManager.GetConfigurationAsString(this.Configuration));
            return File(byteArray, "application/xml", "umatiGatewayConfig.xml");
        }
    }
}
