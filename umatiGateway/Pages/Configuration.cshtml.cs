// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO.Compression;
using System.Text;
using UmatiGateway.OPC;
using UmatiGateway.OPC.CustomEncoding;

namespace UmatiGateway.Pages
{
    public class ConfigurationModel : PageModel
    {
        private String SessionId = "";
        private ClientFactory ClientFactory;
        public Configuration configuration { get; set; } = new Configuration();
        public Configuration loadedConfiguration { get; set; } = new Configuration();
        public CustomEncodingManager CustomEncodingsManager { get; set; } = new CustomEncodingManager();
        public List<CustomEncoding> CustomEncodings { get; set; } = new List<CustomEncoding>();
        public List<PublishedNode> PublishedNodes { get; set; } = new List<PublishedNode>();


        public ConfigurationModel(ClientFactory ClientFactory)
        {
            this.ClientFactory = ClientFactory;
        }
        public void OnGet()
        {
            UmatiGatewayApp client = this.getClient();
            this.configuration = client.configuration;
            this.loadedConfiguration = client.loadedConfiguration;
            this.CustomEncodingsManager = client.MqttProvider.customEncodingManager;
            this.CustomEncodings = client.configuration.customEncodings;
            this.PublishedNodes = client.configuration.publishedNodes;
        }
        public IActionResult OnPostDownload(string OpcConnectionUrl, string OpcUser, string OpcPassword,
            string MqttConnectionUrl, string MqttUser, string MqttPassword, string MqttClientId, string MqttPrefix,
            bool? AutoStart, bool? readExtraLibs,string LogLevel, string configfilePath, List<CustomEncoding> CustomEncodings, List<PublishedNode> PublishedNodes,
            string CertificateFile, string CertificatePassword)

        {
            UmatiGatewayApp client = this.getClient();
            Configuration configuration = new Configuration();
            configuration.configFilePath = configfilePath ?? "./umatiLocalConfig.xml";
            configuration.opcServerEndpoint = OpcConnectionUrl ?? "";
            configuration.opcUser = OpcUser ?? "";
            configuration.opcPassword = OpcPassword ?? "";
            configuration.mqttServerEndpopint = MqttConnectionUrl ?? "";
            configuration.mqttUser = MqttUser ?? "";
            configuration.mqttPassword = MqttPassword ?? "";
            configuration.mqttClientId = MqttClientId ?? "";
            configuration.mqttPrefix = MqttPrefix ?? "";
            configuration.autostart = AutoStart ?? false;
            configuration.readExtraLibs = readExtraLibs ?? false;
            configuration.loglevel = LogLevel;
            configuration.customEncodings = CustomEncodings ?? new List<CustomEncoding>();
            configuration.publishedNodes = PublishedNodes ?? new List<PublishedNode>();
            configuration.mqttCertificateFile = CertificateFile ?? "";
            configuration.mqttCertificatePassword = CertificatePassword ?? "";
            ConfigurationWriter writer = new ConfigurationWriter();
            string appfileContent = writer.WriteApplicationConfigToString(configuration);
            string fileContent = writer.WriteToString(configuration);
            string configurationPath = configuration.configFilePath;
            string filename = Path.GetFileName(configurationPath);

            // Create a zip file
            using MemoryStream memoryStream = new MemoryStream();
            using (ZipArchive zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                ZipArchiveEntry configEntry = zip.CreateEntry("Configuration/umatiGatewayConfig.xml", CompressionLevel.Fastest);
                using (Stream entryStream = configEntry.Open())
                {
                    using (StreamWriter streamWriter = new StreamWriter(entryStream))
                    {
                        streamWriter.Write(appfileContent);
                    }
                }
                ZipArchiveEntry fileEntry = zip.CreateEntry("Configuration/Files/umatiLocalConfig.xml", CompressionLevel.Fastest);
                using (Stream entryStream = fileEntry.Open())
                {
                    using (StreamWriter streamWriter = new StreamWriter(entryStream))
                    {
                        streamWriter.Write(fileContent);
                    }
                }
            }
            // Convert the content to a byte array
            //var byteArray = Encoding.UTF8.GetBytes(fileContent);

            // Return the file with content, MIME type, and suggested file name
            return File(memoryStream.ToArray(), "application/zip", "UmatiGatewayConfig.zip");
        }
        public IActionResult OnPostLoad(string FileContent)
        {
            UmatiGatewayApp client = this.getClient();
            ConfigurationReader configReader = new ConfigurationReader();
            this.loadedConfiguration = configReader.ReadConfiguration(FileContent);
            client.loadedConfiguration = this.loadedConfiguration;
            return RedirectToPage();
        }
        private UmatiGatewayApp getClient()
        {
            UmatiGatewayApp client = ClientFactory.getClient(this.SessionId);
            return client;
        }
    }
}
