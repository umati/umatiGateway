// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using NLog;
using Opc.Ua;
using System.Text;
using System.Xml;

namespace umatiGateway.Core.Configuration
{
    public class UmatiConfigurationManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public const string VERSION_2_0 = "2.0";
        public const string VERSION = "version";
        public const string UMATI_GATEWAY_CONFIG_FILE_PATH = "./umatiGatewayConfig.xml";
        public const string UMATI_GATEWAY_CONFIG = "umatiGatewayConfig";
        public const string LOG_LEVEL = "logLevel";
        public const string READ_EXTRA_LIBS = "ReadExtraLibs";
        public const string INCLUDE_STRUCTURED_COMPONENTS = "includeStructuredComponents";
        public const string PUBLISH_INTERVAL = "publishInterval";
        public const string STARTCONFIGURATION = "StartConfiguration";
        public const string START_WEB_UI = "startWebUI";
        public const string START_OPC_CONNECTION = "startOPCConnection";
        public const string START_MQTT_PROVIDER = "startMqttProvider";
        public const string START_PUBSUB_PROVIDER = "startPubSubProvider";
        public const string OPC_CONNECTION = "OPCConnection";
        public const string SERVERENDPOINT = "serverendpoint";
        public const string AUTHENTICATION = "authentication";
        public const string USER = "user";
        public const string PASSWORD = "password";
        public const string WEB_UI = "WebUI";
        public const string URL = "url";
        public const string MQTT_PROVIDER = "MqttProvider";
        public const string CLIENT_ID = "clientId";
        public const string PREFIX = "prefix";
        public const string PUBLISHED_NODES = "PublishedNodes";
        public const string PUBLISHED_NODE = "PublishedNode";
        public const string TYPE = "type";
        public const string NAMESPACE_URL = "namespaceurl";
        public const string NODE_ID = "nodeId";
        public const string BASE_TYPE = "baseType";
        public const string PUB_SUB_PROVIDER = "PubSubProvider";
        public const string PUB_SUB_NODES = "PubSubNodes";
        public const string PUB_SUB_NODE = "PubSubNode";
        public const string CUSTOM_ENCODINGS = "CustomEncodings";
        public const string CUSTOM_ENCODING = "CustomEncoding";
        public const string NAME = "name";
        public const string ACTIVE = "active";
        
        public UmatiConfigurationManager() { }

        public UmatiConfiguration ReadConfiguration()
        {
            UmatiConfiguration configuration = new UmatiConfiguration();
            XmlDocument xmlDoc = new();
            try
            {
                xmlDoc.Load(UMATI_GATEWAY_CONFIG_FILE_PATH);
                XmlNode? node = xmlDoc.SelectSingleNode($"/{UMATI_GATEWAY_CONFIG}");
                if (node != null)
                {
                    configuration.LogLevel = ReadAttribute(node, LOG_LEVEL);
                    configuration.Version = ReadAttribute(node, VERSION);
                    if (configuration.Version == VERSION_2_0)
                    {
                        XmlNode? startConfigurationNode = ReadNode(node, STARTCONFIGURATION);
                        if (startConfigurationNode != null)
                        {
                            string startWebUI = ReadAttribute(startConfigurationNode, START_WEB_UI);
                            configuration.StartConfiguration.StartWebUI = string.Equals(startWebUI, "true", StringComparison.OrdinalIgnoreCase) ? true : false;
                            string startOPCConnection = ReadAttribute(startConfigurationNode, START_OPC_CONNECTION);
                            configuration.StartConfiguration.StartOPCConnection = string.Equals(startOPCConnection, "true", StringComparison.OrdinalIgnoreCase) ? true : false;
                            string startMqttProvider = ReadAttribute(startConfigurationNode, START_MQTT_PROVIDER);
                            configuration.StartConfiguration.StartMQTTProvider = string.Equals(startMqttProvider, "true", StringComparison.OrdinalIgnoreCase) ? true : false;
                            string startPubSubProvider = ReadAttribute(startConfigurationNode, START_PUBSUB_PROVIDER);
                            configuration.StartConfiguration.StartPubSubProvider = string.Equals(startPubSubProvider, "true", StringComparison.OrdinalIgnoreCase) ? true : false;
                        } else
                        {
                            Logger.Warn($"No {STARTCONFIGURATION} node defined in {UMATI_GATEWAY_CONFIG} node.");
                        }
                        XmlNode? opcConnectionNode = ReadNode(node, OPC_CONNECTION);
                        if (opcConnectionNode != null)
                        {
                            configuration.OPCConnection.ServerEndpoint = ReadAttribute(opcConnectionNode, SERVERENDPOINT);
                            configuration.OPCConnection.Authentication = ReadAttribute(opcConnectionNode, AUTHENTICATION);
                            configuration.OPCConnection.UserName = ReadAttribute(opcConnectionNode, USER);
                            configuration.OPCConnection.Password = ReadAttribute(opcConnectionNode, PASSWORD);
                            string readExtraLibs = ReadAttribute(opcConnectionNode, READ_EXTRA_LIBS);
                            configuration.OPCConnection.ReadExtraLibs = string.Equals(readExtraLibs, "true", StringComparison.OrdinalIgnoreCase) ? true : false;
                        } else
                        {
                            Logger.Warn($"No {OPC_CONNECTION} node defined in {UMATI_GATEWAY_CONFIG} node.");
                        }
                        XmlNode? webUINode = ReadNode(node, WEB_UI);
                        if (webUINode != null)
                        {
                            configuration.WebUI.URL = ReadAttribute(webUINode, URL);
                        }
                        else
                        {
                            Logger.Warn($"No {WEB_UI} node defined in {UMATI_GATEWAY_CONFIG} node.");
                        }
                        XmlNode? mqttProviderNode = ReadNode(node, MQTT_PROVIDER);
                        if (mqttProviderNode != null)
                        {
                            configuration.MqttProviderConfig.UserName = ReadAttribute(mqttProviderNode, USER);
                            configuration.MqttProviderConfig.Password = ReadAttribute(mqttProviderNode, PASSWORD);
                            configuration.MqttProviderConfig.ServerEndpoint = ReadAttribute(mqttProviderNode, SERVERENDPOINT);
                            configuration.MqttProviderConfig.ClientId = ReadAttribute(mqttProviderNode, CLIENT_ID);
                            configuration.MqttProviderConfig.Prefix = ReadAttribute(mqttProviderNode, PREFIX);
                            string includeStructuredComponents = ReadAttribute(mqttProviderNode, INCLUDE_STRUCTURED_COMPONENTS);
                            configuration.MqttProviderConfig.IncludeStructuredComponents = string.Equals(includeStructuredComponents, "true", StringComparison.OrdinalIgnoreCase) ? true : false;
                            string publishInterval = ReadAttribute(mqttProviderNode, PUBLISH_INTERVAL);
                            configuration.MqttProviderConfig.PublishInterval = uint.Parse(publishInterval);
                            List<PublishedNode> publishedNodes = new List<PublishedNode>();
                            XmlNode? publishedNodesNode = ReadNode(mqttProviderNode, PUBLISHED_NODES);
                            if (publishedNodesNode != null)
                            {
                                XmlNodeList? publishedNodeList = publishedNodesNode.SelectNodes(PUBLISHED_NODE);
                                if(publishedNodeList != null)
                                {
                                    foreach (XmlNode publishedNodeNode in publishedNodeList)
                                    {
                                        PublishedNode publishedNode = new PublishedNode();
                                        publishedNode.Type = ReadAttribute(publishedNodeNode, TYPE);
                                        publishedNode.NamespaceUrl = ReadAttribute(publishedNodeNode, NAMESPACE_URL);
                                        publishedNode.NodeId = ReadAttribute(publishedNodeNode, NODE_ID);
                                        publishedNode.BaseType = ReadAttribute(publishedNodeNode, BASE_TYPE);
                                        publishedNodes.Add(publishedNode);
                                    }
                                }
                            }
                            else
                            {
                                Logger.Warn($"No {PUBLISHED_NODES} node defined in {MQTT_PROVIDER} node.");
                            }
                            configuration.MqttProviderConfig.PublishedNodes = publishedNodes;
                            XmlNode? customEncodingsNode = ReadNode(mqttProviderNode, CUSTOM_ENCODINGS);
                            List<CustomEncoding> customEncodings = new List<CustomEncoding>();
                            if (customEncodingsNode != null)
                            {
                                XmlNodeList? customEncodingsList = customEncodingsNode.SelectNodes(CUSTOM_ENCODING);
                                if (customEncodingsList != null)
                                {
                                    foreach (XmlNode customEncodingNode in customEncodingsList)
                                    {
                                        CustomEncoding customEncoding = new CustomEncoding();
                                        customEncoding.Name = ReadAttribute(customEncodingNode, NAME);
                                        string active = ReadAttribute(customEncodingNode, ACTIVE);
                                        customEncoding.Active = string.Equals(active, "true", StringComparison.OrdinalIgnoreCase) ? true : false;
                                        customEncodings.Add(customEncoding);
                                    }
                                }
                            }
                            else
                            {
                                Logger.Warn($"No {CUSTOM_ENCODINGS} node defined in {MQTT_PROVIDER} node.");
                            }
                            configuration.MqttProviderConfig.CustomEncodings = customEncodings;
                        }
                        else
                        {
                            Logger.Warn($"No {MQTT_PROVIDER} node defined in {UMATI_GATEWAY_CONFIG} node.");
                        }
                        XmlNode? pubSubProviderNode = ReadNode(node, PUB_SUB_PROVIDER);
                        if (pubSubProviderNode != null)
                        {
                            configuration.PubSubProviderConfig.UserName = ReadAttribute(pubSubProviderNode, USER);
                            configuration.PubSubProviderConfig.Password = ReadAttribute(pubSubProviderNode, PASSWORD);
                            configuration.PubSubProviderConfig.ServerEndpoint = ReadAttribute(pubSubProviderNode, SERVERENDPOINT);
                            configuration.PubSubProviderConfig.ClientId = ReadAttribute(pubSubProviderNode, CLIENT_ID);
                            configuration.PubSubProviderConfig.Prefix = ReadAttribute(pubSubProviderNode, PREFIX);
                            List<PubSubNode> pubSubNodes = new List<PubSubNode>();
                            XmlNode? pubSubNodesNode = ReadNode(pubSubProviderNode, PUB_SUB_NODES);
                            if (pubSubNodesNode != null)
                            {
                                XmlNodeList? pubSubNodeList = pubSubNodesNode.SelectNodes(PUB_SUB_NODE);
                                if (pubSubNodeList != null)
                                {
                                    foreach (XmlNode pubSubNodeNode in pubSubNodeList)
                                    {
                                        PubSubNode pubSubNode = new PubSubNode();
                                        pubSubNode.Type = ReadAttribute(pubSubNodeNode, TYPE);
                                        pubSubNode.NamespaceUrl = ReadAttribute(pubSubNodeNode, NAMESPACE_URL);
                                        pubSubNode.NodeId = ReadAttribute(pubSubNodeNode, NODE_ID);
                                        pubSubNodes.Add(pubSubNode);
                                    }
                                }
                            }
                            else
                            {
                                Logger.Warn($"No {PUB_SUB_NODES} node defined in {PUB_SUB_PROVIDER} node.");
                            }
                            configuration.PubSubProviderConfig.PubSubNodes = pubSubNodes;
                        }
                    }
                    else
                    {
                        Logger.Error($"Unable to load configuration file with Version: {configuration.Version}.\n" +
                            $"Minimum configuration version is: {VERSION_2_0}");
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

        public string GetConfigurationAsString(UmatiConfiguration configuration)
        {
            XmlDocument xmlDocument = new XmlDocument();
            XmlDeclaration xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDocument.InsertBefore(xmlDeclaration, xmlDocument.DocumentElement);
            XmlElement configurationNode = xmlDocument.CreateElement(UMATI_GATEWAY_CONFIG);
            XmlAttribute version = xmlDocument.CreateAttribute(VERSION);
            version.Value = VERSION_2_0;
            XmlAttribute logLevel = xmlDocument.CreateAttribute(LOG_LEVEL);
            logLevel.Value = configuration.LogLevel;
            configurationNode.Attributes.Append(version);
            configurationNode.Attributes.Append(logLevel);
            xmlDocument.AppendChild(configurationNode);
            XmlElement opcConnectionNode = xmlDocument.CreateElement(OPC_CONNECTION);
            XmlAttribute opcServerEndpoint = xmlDocument.CreateAttribute(SERVERENDPOINT);
            opcServerEndpoint.Value = configuration.OPCConnection.ServerEndpoint;
            XmlAttribute opcAuthentication = xmlDocument.CreateAttribute(AUTHENTICATION);
            opcAuthentication.Value = configuration.OPCConnection.Authentication;
            XmlAttribute opcUser = xmlDocument.CreateAttribute(USER);
            opcUser.Value = configuration.OPCConnection.UserName;
            XmlAttribute opcPassword = xmlDocument.CreateAttribute(PASSWORD);
            opcPassword.Value = configuration.OPCConnection.Password;
            XmlAttribute readExtraLibs = xmlDocument.CreateAttribute(READ_EXTRA_LIBS);
            readExtraLibs.Value = configuration.OPCConnection.ReadExtraLibs.ToString();
            opcConnectionNode.Attributes.Append(opcServerEndpoint);
            opcConnectionNode.Attributes.Append(opcAuthentication);
            opcConnectionNode.Attributes.Append(opcUser);
            opcConnectionNode.Attributes.Append(opcPassword);
            opcConnectionNode.Attributes.Append(readExtraLibs);
            XmlElement mqttConnectionNode = xmlDocument.CreateElement(MQTT_PROVIDER);
            XmlAttribute mqttServerEndpoint = xmlDocument.CreateAttribute(SERVERENDPOINT);
            mqttServerEndpoint.Value = configuration.MqttProviderConfig.ServerEndpoint;
            XmlAttribute mqttUser = xmlDocument.CreateAttribute(USER);
            mqttUser.Value = configuration.MqttProviderConfig.UserName;
            XmlAttribute mqttPassword = xmlDocument.CreateAttribute(PASSWORD);
            mqttPassword.Value = configuration.MqttProviderConfig.Password;
            XmlAttribute mqttClientId = xmlDocument.CreateAttribute(CLIENT_ID);
            mqttClientId.Value = configuration.MqttProviderConfig.ClientId;
            XmlAttribute mqttPrefix = xmlDocument.CreateAttribute(PREFIX);
            mqttPrefix.Value = configuration.MqttProviderConfig.Prefix;
            XmlAttribute includeStructuredComponents = xmlDocument.CreateAttribute(INCLUDE_STRUCTURED_COMPONENTS);
            includeStructuredComponents.Value = configuration.MqttProviderConfig.IncludeStructuredComponents.ToString();
            XmlAttribute publishInterval = xmlDocument.CreateAttribute(PUBLISH_INTERVAL);
            publishInterval.Value = configuration.MqttProviderConfig.PublishInterval.ToString();
            XmlElement customEncodingsNode = xmlDocument.CreateElement(CUSTOM_ENCODINGS);
            foreach (CustomEncoding customEncoding in configuration.MqttProviderConfig.CustomEncodings)
            {
                XmlElement customEncodingNode = xmlDocument.CreateElement(CUSTOM_ENCODING);
                XmlAttribute nameNode = xmlDocument.CreateAttribute(NAME);
                nameNode.Value = customEncoding.Name;
                XmlAttribute active = xmlDocument.CreateAttribute(ACTIVE);
                active.Value = customEncoding.Active.ToString();
                customEncodingNode.Attributes.Append(nameNode);
                customEncodingNode.Attributes.Append(active);
                customEncodingsNode.AppendChild(customEncodingNode);
            }
            mqttConnectionNode.Attributes.Append(mqttServerEndpoint);
            mqttConnectionNode.Attributes.Append(mqttUser);
            mqttConnectionNode.Attributes.Append(mqttPassword);
            mqttConnectionNode.Attributes.Append(mqttClientId);
            mqttConnectionNode.Attributes.Append(mqttPrefix);
            mqttConnectionNode.Attributes.Append(includeStructuredComponents);
            mqttConnectionNode.Attributes.Append(publishInterval);
            mqttConnectionNode.AppendChild(customEncodingsNode);
            XmlElement publishedNodesNode = xmlDocument.CreateElement(PUBLISHED_NODES);
            foreach (PublishedNode publishedNode in configuration.MqttProviderConfig.PublishedNodes)
            {
                XmlElement publishedNodeNode = xmlDocument.CreateElement(PUBLISHED_NODE);
                XmlAttribute typeNode = xmlDocument.CreateAttribute(TYPE);
                typeNode.Value = publishedNode.Type;
                XmlAttribute namespaceUrlNode = xmlDocument.CreateAttribute(NAMESPACE_URL);
                namespaceUrlNode.Value = publishedNode.NamespaceUrl;
                XmlAttribute nodeIdNode = xmlDocument.CreateAttribute(NODE_ID);
                nodeIdNode.Value = publishedNode.NodeId;
                XmlAttribute basetypeNode = xmlDocument.CreateAttribute(BASE_TYPE);
                basetypeNode.Value = publishedNode.BaseType;
                publishedNodeNode.Attributes.Append(typeNode);
                publishedNodeNode.Attributes.Append(namespaceUrlNode);
                publishedNodeNode.Attributes.Append(nodeIdNode);
                publishedNodeNode.Attributes.Append(basetypeNode);
                publishedNodesNode.AppendChild(publishedNodeNode);
            }
            mqttConnectionNode.AppendChild(publishedNodesNode);
            configurationNode.AppendChild(opcConnectionNode);
            configurationNode.AppendChild(mqttConnectionNode);
            configurationNode.AppendChild(publishedNodesNode);
            xmlDocument.AppendChild(configurationNode);
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineOnAttributes = false, Encoding = Encoding.UTF8 };
            StringBuilder sb = new StringBuilder();
            XmlWriter writer = XmlWriter.Create(sb, settings);
            xmlDocument.Save(writer);
            return sb.ToString();
        }

        public XmlNode? ReadNode(XmlNode node, string childNodeName)
        {
            return node.SelectSingleNode(childNodeName);
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
            }
            else
            {
                Logger.Info($"Configuration:  \"{attributeName}\" = \"{value}\"");
            }
            return value;
        }
    }
}
