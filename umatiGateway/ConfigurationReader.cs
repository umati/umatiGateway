// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using NLog;
using Opc.Ua;
using System.Xml;

namespace UmatiGateway
{
    public class ConfigurationReader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private bool isAutostart = false;
        public const string VERSION_1_0 = "1.0";
        public const string CONFIG_VERSION_1_0 = "1.0";
        public ConfigurationReader() { }

        public Configuration ReadConfiguration()
        {
            Configuration configuration = new Configuration();
            XmlDocument xmlDoc = new();
            try
            {
                xmlDoc.Load("./Configuration/umatiGatewayConfig.xml");
                XmlNode? node = xmlDoc.SelectSingleNode("/umatiGatewayConfig");
                if (node != null)
                {

                    configuration.gatewayConfigVersion = this.ReadAttribute(node, "version");
                    if (configuration.gatewayConfigVersion == VERSION_1_0)
                    {
                        string autostart = this.ReadAttribute(node, "autostart");
                        configuration.autostart = string.Equals(autostart, "true", StringComparison.OrdinalIgnoreCase);
                        this.isAutostart = configuration.autostart;

                        string logLevel = this.ReadAttribute(node, "logLevel");
                        logLevel = logLevel.ToLower();
                        switch (logLevel)
                        {
                            case "trace": configuration.loglevel = "Trace"; break;
                            case "debug": configuration.loglevel = "Debug"; break;
                            case "info": configuration.loglevel = "Info"; break;
                            case "warn": configuration.loglevel = "Warn"; break;
                            case "error": configuration.loglevel = "Error"; break;
                            default: 
                                Logger.Warn($"Configuration: Wrong logLevel \"{logLevel}\". Set to default \"INFO\".");
                                configuration.loglevel = "INFO";
                            break;
                        }
                        string readExtraLibs = this.ReadAttribute(node, "ReadExtraLibs");
                        if (string.Equals(readExtraLibs, "true", StringComparison.OrdinalIgnoreCase))
                        {
                            configuration.readExtraLibs = true;
                        }
                        else
                        {
                            configuration.readExtraLibs = false;
                        }
                        string includeStructuredComponents = this.ReadAttribute(node, "includeStructuredComponents");
                        if (string.Equals(includeStructuredComponents, "true", StringComparison.OrdinalIgnoreCase))
                        {
                            configuration.includeStructuredComponents = true;
                        }
                        else
                        {
                            configuration.includeStructuredComponents = false;
                        }
                        string SingleThreadPolling = this.ReadAttribute(node, "singleThreadPolling");
                        if (string.Equals(SingleThreadPolling, "true", StringComparison.OrdinalIgnoreCase))
                        {
                            configuration.singleThreadPolling = true;
                        }
                        else
                        {
                            configuration.singleThreadPolling = false;
                        }
                        string PollTime = this.ReadAttribute(node, "pollTime");
                        if (string.IsNullOrWhiteSpace(PollTime))
                        {
                            configuration.pollTime = 2000;
                        }
                        else
                        {
                            try
                            {
                                configuration.pollTime = int.Parse(PollTime);
                            }
                            catch (Exception e)
                            {
                                Logger.Info(e.Message);
                            }
                        }
                        configuration.configFilePath = this.ReadAttribute(node, "file");
                        if (string.IsNullOrWhiteSpace(configuration.configFilePath))
                        {
                            Logger.Error("Configuration: No config file path found!");
                           throw new Exception("No config file path found!");
                        }
                        else
                        {
                            this.ReadConfigFile(configuration);
                        }
                    }
                }
                else
                {
                    Logger.Error("Root Node not found!");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error on Loading ConfigurationFile." + ex.ToString());
            }
            return configuration;
        }

        public string ReadAttribute(XmlNode node, string attributeName)
        {
            string? value = "";
            if (node.Attributes != null)
            {
                value = node.Attributes[attributeName]?.Value;
                if (value == null)
                {
                    Logger.Warn($"Reading Configuration: Attribute \"{attributeName}\" of node \"{node.Name}\" is missing.");
                    value = "";
                }
            }
            else
            {
                Logger.Warn($"Error on Reading Configuration: Attribute \"{attributeName}\" of node \"{node.Name}\" is missing. The node \"{node.Name}\" does not contain attributes.");
            }
            //Log config values except password because of security reasons (log will be shared)
            if (attributeName == "password")
            {
                Logger.Info($"Configuration:  \"{attributeName}\" = \"{value.Length}\"");
            } else
            {
                Logger.Info($"Configuration:  \"{attributeName}\" = \"{value}\"");
            }
            return value;
        }
        public Configuration ReadConfiguration(string fileContent)
        {
            Configuration configuration = new Configuration();
            XmlDocument xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.LoadXml(fileContent);
                XmlNode? node = xmlDoc.SelectSingleNode("/Configuration");
                if (node != null)
                {

                    configuration.configVersion = this.ReadAttribute(node, "version");
                    if (configuration.configVersion == CONFIG_VERSION_1_0)
                    {
                        XmlNode? opcNode = xmlDoc.SelectSingleNode("/Configuration/OPCConnection");
                        if (opcNode != null)
                        {
                            configuration.opcServerEndpoint = this.ReadAttribute(opcNode, "serverendpoint");
                            configuration.opcAuthentication = this.ReadAttribute(opcNode, "authentication");
                            configuration.opcUser = this.ReadAttribute(opcNode, "user");
                            configuration.opcPassword = this.ReadAttribute(opcNode, "password");

                        }
                        else
                        {
                            Logger.Info("OPCNode not found!");
                        }

                        XmlNode? mqttNode = xmlDoc.SelectSingleNode("/Configuration/MqttConnection");
                        if (mqttNode != null)
                        {
                            configuration.mqttServerEndpopint = this.ReadAttribute(mqttNode, "serverendpoint");
                            configuration.mqttUser = this.ReadAttribute(mqttNode, "user");
                            configuration.mqttPassword = this.ReadAttribute(mqttNode, "password");
                            configuration.mqttClientId = this.ReadAttribute(mqttNode, "clientId");
                            configuration.mqttPrefix = this.ReadAttribute(mqttNode, "prefix");
                        }
                        else
                        {
                            Logger.Info("MqttNode not found!");
                        }
                        XmlNodeList? publishedNodes = xmlDoc.SelectNodes("/Configuration/PublishedNodes/PublishedNode");
                        if (publishedNodes != null)
                        {
                            foreach (XmlNode publishedNode in publishedNodes)
                            {
                                PublishedNode published = new PublishedNode();
                                published.Type = this.ReadAttribute(publishedNode, "type");
                                published.NamespaceUrl = this.ReadAttribute(publishedNode, "namespaceurl");
                                published.NodeId = this.ReadAttribute(publishedNode, "nodeId");
                                published.BaseType = this.ReadAttribute(publishedNode, "BaseType");
                                configuration.publishedNodes.Add(published);
                            }
                        }
                        else
                        {
                            Logger.Info("No nodes to pubish found");
                        }
                        XmlNodeList? customEncodings = xmlDoc.SelectNodes("/Configuration/CustomEncodings/CustomEncoding");
                        if (customEncodings != null)
                        {
                            foreach (XmlNode customEncodingNode in customEncodings)
                            {
                                CustomEncoding customEncoding = new CustomEncoding();
                                customEncoding.Name = this.ReadAttribute(customEncodingNode, "name");

                                string active = this.ReadAttribute(customEncodingNode, "active");
                                if (string.Equals(active, "true", StringComparison.OrdinalIgnoreCase))
                                {
                                    customEncoding.Active = true;
                                }
                                else
                                {
                                    customEncoding.Active = false;
                                }
                                configuration.customEncodings.Add(customEncoding);
                            }
                        }
                        else
                        {
                            Logger.Info("No CustomEncodings found");
                        }
                    }
                    else
                    {
                        Logger.Info($"Wrong Version \"{configuration.gatewayConfigVersion}\".");
                    }
                }
                else
                {
                    Logger.Info("Root Node not found!");
                }

            }
            catch (Exception ex)
            {
                Logger.Info($"Error on Loading ConfigurationFile \"{configuration.configFilePath}\"." + ex.ToString());
            }
            return configuration;
        }

        public void ReadConfigFile(Configuration configuration)
        {
            XmlDocument xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.Load(configuration.configFilePath);
                XmlNode? node = xmlDoc.SelectSingleNode("/Configuration");
                if (node != null)
                {

                    configuration.configVersion = this.ReadAttribute(node, "version");
                    if (configuration.configVersion == CONFIG_VERSION_1_0)
                    {
                        XmlNode? opcNode = xmlDoc.SelectSingleNode("/Configuration/OPCConnection");
                        if (opcNode != null)
                        {
                            Logger.Info($"Configuration:** OPCNode ***");
                            configuration.opcServerEndpoint = this.ReadAttribute(opcNode, "serverendpoint");
                            configuration.opcAuthentication = this.ReadAttribute(opcNode, "authentication");
                            configuration.opcUser = this.ReadAttribute(opcNode, "user");
                            configuration.opcPassword = this.ReadAttribute(opcNode, "password");

                        }
                        else
                        {
        
                            LogConditionalOnAutostart("OPCNode not found!");
                        }

                        XmlNode? mqttNode = xmlDoc.SelectSingleNode("/Configuration/MqttConnection");
                        if (mqttNode != null)
                        {
                            Logger.Info($"Configuration:*** MqttNode ***");
                            configuration.mqttServerEndpopint = this.ReadAttribute(mqttNode, "serverendpoint");
                            configuration.mqttUser = this.ReadAttribute(mqttNode, "user");
                            configuration.mqttPassword = this.ReadAttribute(mqttNode, "password");
                            configuration.mqttClientId = this.ReadAttribute(mqttNode, "clientId");
                            configuration.mqttPrefix = this.ReadAttribute(mqttNode, "prefix");
                        }
                        else
                        {
                            LogConditionalOnAutostart("MqttNode not found!");
                        }
                        XmlNodeList? publishedNodes = xmlDoc.SelectNodes("/Configuration/PublishedNodes/PublishedNode");
                        if (publishedNodes != null)
                        {
                            Logger.Info($"Configuration:*** Published Nodes ***");
                            foreach (XmlNode publishedNode in publishedNodes)
                            {
                                PublishedNode published = new PublishedNode();
                                published.Type = this.ReadAttribute(publishedNode, "type");
                                published.NamespaceUrl = this.ReadAttribute(publishedNode, "namespaceurl");
                                published.NodeId = this.ReadAttribute(publishedNode, "nodeId");
                                published.BaseType = this.ReadAttribute(publishedNode, "BaseType");
                                configuration.publishedNodes.Add(published);
                            }
                        }
                        else
                        {
                            LogConditionalOnAutostart("No nodes to pubish found");
                        }
                        XmlNodeList? customEncodings = xmlDoc.SelectNodes("/Configuration/CustomEncodings/CustomEncoding");
                        if (customEncodings != null)
                        {
                            foreach (XmlNode customEncodingNode in customEncodings)
                            {
                                CustomEncoding customEncoding = new CustomEncoding();
                                customEncoding.Name = this.ReadAttribute(customEncodingNode, "name");

                                string active = this.ReadAttribute(customEncodingNode, "active");
                                if (string.Equals(active, "true", StringComparison.OrdinalIgnoreCase))
                                {
                                    customEncoding.Active = true;
                                }
                                else
                                {
                                    customEncoding.Active = false;
                                }
                                configuration.customEncodings.Add(customEncoding);
                            }
                        }
                        else
                        {
                            Logger.Info("No CustomEncodings found");
                        }

                    }
                    else
                    {
                        Logger.Info($"Wrong Version \"{configuration.gatewayConfigVersion}\".");
                    }
                }
                else
                {
                    Logger.Info("Root Node not found!");
                }

            }
            catch (Exception ex)
            {
                Logger.Info($"Error on Loading ConfigurationFile \"{configuration.configFilePath}\"." + ex.ToString());
            }

        }

        private void LogConditionalOnAutostart(string message)
        {
            if (this.isAutostart)
            {
                Logger.Error(message);
            }
            else
            {
                Logger.Info(message);
            }
        }
    }
}
