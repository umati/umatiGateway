// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using System.Collections;
using System.Reflection;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Extensions.Logging;
using Opc.Ua;
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
        public UmatiConfiguration ActiveConfiguration { get; set; } = new UmatiConfiguration();
        public UmatiGatewayApp(ApplicationConfiguration configuration, TextWriter writer, Action<IList, IList> validateResponse)
        {
            ConfigureLogging();
            ActiveConfiguration = new UmatiConfigurationManager().ReadConfiguration();
            Logger.Info("Reconfiger Logger");
            Logger.Info("Reading Configuration");
            ConfigureLogging();
            OpcUaClient = new OpcUaClient(this, configuration);
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
            string logfilePath = Path.Combine(AppContext.BaseDirectory, "logs/umatiGateway.log");
            if (File.Exists(logfilePath))
            {
                File.Delete(logfilePath);
            }
            FileTarget logfile = new FileTarget("logfile")
            {
                FileName = logfilePath,
                Layout = "${uppercase:${level}} ${message}",
                ArchiveAboveSize = 10L * 1024 * 1024, // 10 MB
                MaxArchiveFiles = 5, // max. 5 alte Files
                ArchiveFileName = "${basedir}/logs/umatiGateway.{#}.log",
                ArchiveSuffixFormat = "{#}",
                CreateDirs = true
            };

            string logLevelstring = ActiveConfiguration.LogLevel ?? "";
            NLog.LogLevel logLevel = NLog.LogLevel.Info;
            Microsoft.Extensions.Logging.LogLevel opcUaLogLevel = Microsoft.Extensions.Logging.LogLevel.Information;
            switch (logLevelstring)
            {
                case "Trace":
                    logLevel = NLog.LogLevel.Trace;
                    opcUaLogLevel = Microsoft.Extensions.Logging.LogLevel.Trace;
                    break;
                case "Debug":
                    logLevel = NLog.LogLevel.Debug;
                    opcUaLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug;
                    break;
                case "Info":
                    logLevel = NLog.LogLevel.Info;
                    opcUaLogLevel = Microsoft.Extensions.Logging.LogLevel.Information;
                    break;
                case "Warn":
                    logLevel = NLog.LogLevel.Warn;
                    opcUaLogLevel = Microsoft.Extensions.Logging.LogLevel.Warning;
                    break;
                case "Error":
                    logLevel = NLog.LogLevel.Error;
                    opcUaLogLevel = Microsoft.Extensions.Logging.LogLevel.Error;
                    break;
                default: logLevel = NLog.LogLevel.Info; break;
            }
            config.AddRule(logLevel, NLog.LogLevel.Fatal, logconsole);
            config.AddRule(logLevel, NLog.LogLevel.Fatal, logfile);
            LogManager.Configuration = config;
            // Set logger for OPC UA stack
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .ClearProviders()
                    .AddNLog() // Connect NLog to ILoggerFactory
                    .SetMinimumLevel(opcUaLogLevel);
            });
            Utils.SetLogger(loggerFactory.CreateLogger("OpcUa"));
            Utils.SetLogLevel(opcUaLogLevel);
        }

        public void StartUp()
        {
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Logger.Info("umatiGateway Version: {Version}", version);
            StartConfiguration startConfiguration = ActiveConfiguration.StartConfiguration;
            if (startConfiguration.StartOPCConnection)
            {
                Logger.Info("Create OPC Connection");
                this.OpcUaClient.Connect();
            }
            if (startConfiguration.StartMQTTProvider)
            {
                Logger.Info("Create MQTT Connection");
                MqttProvider.Connect();
            }
            if (startConfiguration.StartPubSubProvider)
            {
                Logger.Info("Create PubSub Connection");
                PubSubProvider.Connect();
            }
        }
    }
}