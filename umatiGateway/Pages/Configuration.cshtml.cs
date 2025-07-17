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
            string WebURL, string OpcConnectionUrl, string OpcUser, string OpcPassword, bool? readExtraLibs,
            string MqttConnectionUrl, string MqttUser, string MqttPassword, string MqttClientId, string MqttPrefix,
            bool? includeStructuredComponents, bool? upperCaseRange, uint? publishInterval, List<CustomEncoding> CustomEncodings, List<PublishedNode> PublishedNodes,
            string PubSubConnectionUrl, string PubSubUser, string PubSubPassword, string PubSubClientId, string PubSubPrefix, bool? allowUntrustedCertificates,
            List<PublishedNode> PubSubPublishedNodes)
        {
            /*Configuration.LogLevel = LogLevel;
            Configuration.StartConfiguration.StartWebUI = StartWebUI ?? false;
            Configuration.StartConfiguration.StartOPCConnection = StartOPCConnection ?? false;
            Configuration.StartConfiguration.StartMQTTProvider = StartMqttProvider ?? false;
            Configuration.StartConfiguration.StartPubSubProvider = StartPubSubProvider ?? false;
            Configuration.WebUI.URL = WebURL ?? "";
            Configuration.OPCConnection.ServerEndpoint = OpcConnectionUrl ?? "";
            Configuration.OPCConnection.UserName = OpcUser ?? "";
            Configuration.OPCConnection.Password = OpcPassword ?? "";
            Configuration.OPCConnection.ReadExtraLibs = readExtraLibs ?? false;
            Configuration.MqttProviderConfig.ServerEndpoint = MqttConnectionUrl ?? "";
            Configuration.MqttProviderConfig.UserName = MqttUser ?? "";
            Configuration.MqttProviderConfig.Password = MqttPassword ?? "";
            Configuration.MqttProviderConfig.ClientId = MqttClientId ?? "";
            Configuration.MqttProviderConfig.Prefix = MqttPrefix ?? "";
            Configuration.MqttProviderConfig.IncludeStructuredComponents = includeStructuredComponents ?? false;
            Configuration.MqttProviderConfig.UpperCaseRange = upperCaseRange ?? false;
            Configuration.MqttProviderConfig.CustomEncodings = CustomEncodings ?? new List<CustomEncoding>();
            Configuration.MqttProviderConfig.PublishInterval = publishInterval ?? 5000;
            Configuration.MqttProviderConfig.PublishedNodes = PublishedNodes ?? new List<PublishedNode>();
            Configuration.PubSubProviderConfig.ServerEndpoint = PubSubConnectionUrl ?? "";
            Configuration.PubSubProviderConfig.UserName = PubSubUser ?? "";
            Configuration.PubSubProviderConfig.Password = PubSubPassword ?? "";
            Configuration.PubSubProviderConfig.ClientId = PubSubClientId ?? "";
            Configuration.PubSubProviderConfig.Prefix = PubSubPrefix ?? "";
            Configuration.PubSubProviderConfig.AllowUntrustedCertificates = allowUntrustedCertificates ?? false;
            Configuration.PubSubProviderConfig.PublishedNodes = PubSubPublishedNodes ?? new List<PublishedNode>();*/
            UmatiConfigurationManager configManager = new UmatiConfigurationManager();
            var byteArray = Encoding.UTF8.GetBytes(configManager.GetConfigurationAsString(this.Configuration));
            return File(byteArray, "application/xml", "umatiGatewayConfig.xml");
        }
    }
}
