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
        
        public IActionResult OnPostDownload(string OpcConnectionUrl, string OpcUser, string OpcPassword,
            string MqttConnectionUrl, string MqttUser, string MqttPassword, string MqttClientId, string MqttPrefix,
            bool? AutoStart, bool? readExtraLibs, bool? includeStructuredComponents, string LogLevel, string configfilePath, List<CustomEncoding> CustomEncodings, List<PublishedNode> PublishedNodes)
        {
            Configuration.OPCConnection.ServerEndpoint = OpcConnectionUrl ?? "";
            Configuration.OPCConnection.UserName = OpcUser ?? "";
            Configuration.OPCConnection.Password = OpcPassword ?? "";
            Configuration.OPCConnection.ServerEndpoint = MqttConnectionUrl ?? "";
            Configuration.MqttProviderConfig.UserName = MqttUser ?? "";
            Configuration.MqttProviderConfig.Password = MqttPassword ?? "";
            Configuration.MqttProviderConfig.ClientId = MqttClientId ?? "";
            Configuration.MqttProviderConfig.Prefix = MqttPrefix ?? "";
            Configuration.OPCConnection.ReadExtraLibs = readExtraLibs ?? false;
            Configuration.MqttProviderConfig.IncludeStructuredComponents = includeStructuredComponents ?? false;
            Configuration.LogLevel = LogLevel;
            Configuration.MqttProviderConfig.CustomEncodings = CustomEncodings ?? new List<CustomEncoding>();
            Configuration.MqttProviderConfig.PublishedNodes = PublishedNodes ?? new List<PublishedNode>();
            UmatiConfigurationManager configManager = new UmatiConfigurationManager();
            var byteArray = Encoding.UTF8.GetBytes(configManager.GetConfigurationAsString(Configuration));
            return File(byteArray, "application/xml", "umatiGatewayConfig.xml");
        }
    }
}
