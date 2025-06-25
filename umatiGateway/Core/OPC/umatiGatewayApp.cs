// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using System.Collections;
using System.Reflection;
using NLog;
using NLog.Config;
using NLog.Targets;
using Opc.Ua;
using Opc.Ua.Client;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.Mqtt;
using umatiGateway.Core.PubSub;

namespace umatiGateway.Core.OPC
{
    public class UmatiGatewayApp
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public IOpcUaClient OpcUaClient { get; set; }
        public PubSubProvider PubSubProvider { get; set; }
        public MqttProvider MqttProvider { get; set; }
        public BrowseTreeController BrowseTreeController { get; set; }
        public TypeDictionaries TypeDictionaries { get; set; }
        public bool AutoAccept { get; set; } = true;
        public UmatiConfiguration ActiveConfiguration { get; set; } = new UmatiConfiguration();
        public UmatiGatewayApp(ApplicationConfiguration configuration, TextWriter writer, Action<IList, IList> validateResponse)
        {
            ConfigureLogging();
            ActiveConfiguration = new UmatiConfigurationManager().ReadConfiguration();
            Logger.Info("Reconfiger Logger");
            Logger.Info("Reading Configuration");
            ConfigureLogging();
            configuration.CertificateValidator.CertificateValidation += CertificateValidation;
            OpcUaClient = new OpcUaClient(this, configuration);
            TypeDictionaries = new TypeDictionaries(this);
            BrowseTreeController = new BrowseTreeController(this.OpcUaClient);
            MqttProvider = new MqttProvider(this);
            PubSubProvider = new PubSubProvider(this);
        }
        private void ConfigureLogging()
        {
            LoggingConfiguration config = new LoggingConfiguration();
            ConsoleTarget logconsole = new ConsoleTarget("logconsole")
            {
                Layout = "${uppercase:${level}} ${message}"
            };
            string logfilePath = Path.Combine(AppContext.BaseDirectory, "umatiGateway.log");
            if (File.Exists(logfilePath))
            {
                File.Delete(logfilePath);
            }
            FileTarget logfile = new FileTarget("logfile")
            {
                FileName = logfilePath,
                Layout = "${uppercase:${level}} ${message}",
            };

            string logLevelstring = ActiveConfiguration.LogLevel ?? "";
            NLog.LogLevel logLevel = NLog.LogLevel.Info;
            switch (logLevelstring)
            {
                case "Trace": logLevel = NLog.LogLevel.Trace; break;
                case "Debug": logLevel = NLog.LogLevel.Debug; break;
                case "Info": logLevel = NLog.LogLevel.Info; break;
                case "Warn": logLevel = NLog.LogLevel.Warn; break;
                case "Error": logLevel = NLog.LogLevel.Error; break;
                default: logLevel = NLog.LogLevel.Info; break;
            }
            config.AddRule(logLevel, NLog.LogLevel.Fatal, logconsole);
            config.AddRule(logLevel, NLog.LogLevel.Fatal, logfile);
            LogManager.Configuration = config;
        }

        public void StartUp()
        {
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Logger.Info("umatiGateway Version: {Version}", version);
            StartConfiguration startConfiguration = ActiveConfiguration.StartConfiguration;
            if(startConfiguration.StartOPCConnection)
            {
                Logger.Info("Create OPC Connection");
                this.OpcUaClient.Connect();
            }
            if(startConfiguration.StartMQTTProvider)
            {
                Logger.Info("Create Mqtt Connection");
                MqttProvider.Connect();
            }
            if (startConfiguration.StartPubSubProvider)
            {
                Logger.Info("Create PubSub Connection");
                PubSubProvider.Connect();
            }
        }
        private void CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
        {
            bool certificateAccepted = false;

            // ****
            // Implement a custom logic to decide if the certificate should be
            // accepted or not and set certificateAccepted flag accordingly.
            // The certificate can be retrieved from the e.Certificate field
            // ***

            ServiceResult error = e.Error;
            Logger.Error($"Error on Certificate Validation: {e.Error}");
            if (error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted && AutoAccept)
            {
                certificateAccepted = true;
            }

            if (certificateAccepted)
            {
                Logger.Info($"Untrusted Certificate accepted. Subject = {e.Certificate.Subject}");
                e.Accept = true;
            }
            else
            {
                Logger.Error($"Untrusted Certificate rejected. Subject = {e.Certificate.Subject}");

            }
        }
        public void AddNodeUmatiMqttConfig(NodeId nodeId)
        {
            string? namespaceUrl = this.OpcUaClient.GetNamespaceTable().GetString(nodeId.NamespaceIndex);
            string? identifier = nodeId.Identifier.ToString();
            if (namespaceUrl != null && identifier != null)
            {
                PublishedNode publishedNode = new PublishedNode();
                publishedNode.NamespaceUrl = namespaceUrl;
                publishedNode.Type = nodeId.IdType.ToString();
                publishedNode.NodeId = identifier;
                publishedNode.BaseType = "";
                this.ActiveConfiguration.MqttProviderConfig.PublishedNodes.Add(publishedNode);
            }
            else
            {
                Logger.Error($"Unable to create PublishedNode from NodeId: {nodeId}");
            }
        }
        public void RemoveNodeMqttConfig(PublishedNode publishedNode)
        {
            this.ActiveConfiguration.MqttProviderConfig.PublishedNodes.Remove(publishedNode);
        }
        public void AddNodeOpcPubSubConfig(NodeId nodeId)
        {
            string? namespaceUrl = this.OpcUaClient.GetNamespaceTable().GetString(nodeId.NamespaceIndex);
            string? identifier = nodeId.Identifier.ToString();
            if (namespaceUrl != null && identifier != null)
            {
                PublishedNode publishedNode = new PublishedNode();
                publishedNode.NamespaceUrl = namespaceUrl;
                publishedNode.Type = nodeId.IdType.ToString();
                publishedNode.NodeId = identifier;
                publishedNode.BaseType = "";
                this.ActiveConfiguration.PubSubProviderConfig.PublishedNodes.Add(publishedNode);
            }
            else
            {
                Logger.Error($"Unable to create PublishedNode from NodeId: {nodeId}");
            }
        }
        public void RemoveNodePubSubConfig(PublishedNode publishedNode)
        {
            this.ActiveConfiguration.PubSubProviderConfig.PublishedNodes.Remove(publishedNode);
        }
    }
}