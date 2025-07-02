// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.Extensions.Configuration;
using NLog;
using Opc.Ua;
using System.Text;
using System.Xml;
using System.Xml.Linq;

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
        public const string PUBLISHED_CHILD_NODES = "PublishedChildNodes";
        public const string TYPE = "type";
        public const string NAMESPACE_URL = "namespaceurl";
        public const string NODE_ID = "nodeId";
        public const string BASE_TYPE = "baseType";
        public const string PUB_SUB_PROVIDER = "PubSubProvider";
        public const string CUSTOM_ENCODINGS = "CustomEncodings";
        public const string CUSTOM_ENCODING = "CustomEncoding";
        public const string NAME = "name";
        public const string ACTIVE = "active";
        public const string FILTER = "Filter";
        public const string CONDITIONS = "Conditions";
        public const string TYPE_ID_CONDITION = "TypeIdCondition";
        public const string RELATION_CONDITION = "RelationCondition";
        public const string INCLUDE_SUB_TYPES = "includeSubTypes";
        public const string IGNORED_PLACEHOLDER_TAGS = "IgnoredPlaceholderTags";
        public const string IGNORED_PLACEHOLDER_TAG = "IgnoredPlaceholderTag";

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
                        }
                        else
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
                        }
                        else
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
                            XmlNode? publishedNodesNode = ReadNode(mqttProviderNode, PUBLISHED_NODES);
                            if (publishedNodesNode != null)
                            {
                                configuration.MqttProviderConfig.PublishedNodes = this.ReadPublishedNodes(publishedNodesNode);
                            }
                            else
                            {
                                Logger.Warn($"No {PUBLISHED_NODES} node defined in {MQTT_PROVIDER} node.");
                            }
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

                            XmlNode? IgnoredPlaceholderTagsNode = ReadNode(mqttProviderNode, IGNORED_PLACEHOLDER_TAGS);
                            if (IgnoredPlaceholderTagsNode != null)
                            {
                                XmlNodeList? IgnoredPlaceholderTagNodes = IgnoredPlaceholderTagsNode.SelectNodes(IGNORED_PLACEHOLDER_TAG);
                                if (IgnoredPlaceholderTagNodes != null)
                                {
                                    foreach (XmlNode IgnoredPlaceholderTagNode in IgnoredPlaceholderTagNodes)
                                    {
                                        IgnoredPlaceholderTag ignoredPlaceholderTag = new IgnoredPlaceholderTag();
                                        ignoredPlaceholderTag.Name = ReadAttribute(IgnoredPlaceholderTagNode, NAME);
                                        configuration.MqttProviderConfig.IgnoredPlaceholderTags.Add(ignoredPlaceholderTag);
                                    }
                                }
                                else
                                {
                                    Logger.Warn($"No {IGNORED_PLACEHOLDER_TAG} nodes defined in {IGNORED_PLACEHOLDER_TAGS} node.");
                                }
                            }
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
                            XmlNode? publishedNodesNode = ReadNode(pubSubProviderNode, PUBLISHED_NODES);
                            if (publishedNodesNode != null)
                            {
                                configuration.PubSubProviderConfig.PublishedNodes = this.ReadPublishedNodes(publishedNodesNode);
                            }
                            else
                            {
                                Logger.Warn($"No {PUBLISHED_NODES} node defined in {PUB_SUB_PROVIDER} node.");
                            }
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
            XmlElement startConfigurationNode = xmlDocument.CreateElement(STARTCONFIGURATION);
            XmlAttribute startWebUi = xmlDocument.CreateAttribute(START_WEB_UI);
            startWebUi.Value = configuration.StartConfiguration.StartWebUI.ToString();
            XmlAttribute startOpcConnection = xmlDocument.CreateAttribute(START_OPC_CONNECTION);
            startOpcConnection.Value = configuration.StartConfiguration.StartOPCConnection.ToString();
            XmlAttribute startMqttProvider = xmlDocument.CreateAttribute(START_MQTT_PROVIDER);
            startMqttProvider.Value = configuration.StartConfiguration.StartMQTTProvider.ToString();
            XmlAttribute startPubSubProvider = xmlDocument.CreateAttribute(START_PUBSUB_PROVIDER);
            startPubSubProvider.Value = configuration.StartConfiguration.StartPubSubProvider.ToString();
            startConfigurationNode.Attributes.Append(startWebUi);
            startConfigurationNode.Attributes.Append(startOpcConnection);
            startConfigurationNode.Attributes.Append(startMqttProvider);
            startConfigurationNode.Attributes.Append(startPubSubProvider);
            XmlElement webUiNode = xmlDocument.CreateElement(WEB_UI);
            XmlAttribute url = xmlDocument.CreateAttribute(URL);
            url.Value = configuration.WebUI.URL;
            webUiNode.Attributes.Append(url);
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
            XmlElement IgnoredPlaceholderTagsNode = xmlDocument.CreateElement(IGNORED_PLACEHOLDER_TAGS);
            foreach (IgnoredPlaceholderTag ignoredPlaceholderTag in configuration.MqttProviderConfig.IgnoredPlaceholderTags)
            {
                XmlElement IgnoredPlaceholderTagNode = xmlDocument.CreateElement(IGNORED_PLACEHOLDER_TAG);
                XmlAttribute IgnoredPlaceholderTagName = xmlDocument.CreateAttribute(NAME);
                IgnoredPlaceholderTagName.Value = ignoredPlaceholderTag.Name;
                IgnoredPlaceholderTagNode.Attributes.Append(IgnoredPlaceholderTagName);
                IgnoredPlaceholderTagsNode.AppendChild(IgnoredPlaceholderTagNode);
            }
            mqttConnectionNode.Attributes.Append(mqttServerEndpoint);
            mqttConnectionNode.Attributes.Append(mqttUser);
            mqttConnectionNode.Attributes.Append(mqttPassword);
            mqttConnectionNode.Attributes.Append(mqttClientId);
            mqttConnectionNode.Attributes.Append(mqttPrefix);
            mqttConnectionNode.Attributes.Append(includeStructuredComponents);
            mqttConnectionNode.Attributes.Append(publishInterval);
            mqttConnectionNode.AppendChild(CreatePublishedNodesNode(xmlDocument, configuration.MqttProviderConfig.PublishedNodes));
            mqttConnectionNode.AppendChild(customEncodingsNode);
            mqttConnectionNode.AppendChild(IgnoredPlaceholderTagsNode);
            XmlElement pubSubNode = xmlDocument.CreateElement(PUB_SUB_PROVIDER);
            XmlAttribute pubSubServerEndpoint = xmlDocument.CreateAttribute(SERVERENDPOINT);
            pubSubServerEndpoint.Value = configuration.PubSubProviderConfig.ServerEndpoint;
            XmlAttribute pubSubUser = xmlDocument.CreateAttribute(USER);
            pubSubUser.Value = configuration.PubSubProviderConfig.UserName;
            XmlAttribute pubSubPassword = xmlDocument.CreateAttribute(PASSWORD);
            pubSubPassword.Value = configuration.PubSubProviderConfig.Password;
            XmlAttribute pubSubClientId = xmlDocument.CreateAttribute(CLIENT_ID);
            pubSubClientId.Value = configuration.PubSubProviderConfig.ClientId;
            XmlAttribute pubSubPrefix = xmlDocument.CreateAttribute(PREFIX);
            pubSubPrefix.Value = configuration.PubSubProviderConfig.Prefix;
            pubSubNode.Attributes.Append(mqttServerEndpoint);
            pubSubNode.Attributes.Append(mqttUser);
            pubSubNode.Attributes.Append(mqttPassword);
            pubSubNode.Attributes.Append(mqttClientId);
            pubSubNode.Attributes.Append(mqttPrefix);
            pubSubNode.Attributes.Append(includeStructuredComponents);
            pubSubNode.Attributes.Append(publishInterval);
            pubSubNode.AppendChild(CreatePublishedNodesNode(xmlDocument, configuration.PubSubProviderConfig.PublishedNodes));
            configurationNode.AppendChild(startConfigurationNode);
            configurationNode.AppendChild(webUiNode);
            configurationNode.AppendChild(opcConnectionNode);
            configurationNode.AppendChild(mqttConnectionNode);
            configurationNode.AppendChild(pubSubNode);
            xmlDocument.AppendChild(configurationNode);
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineOnAttributes = false, Encoding = Encoding.UTF8 };
            StringBuilder sb = new StringBuilder();
            XmlWriter writer = XmlWriter.Create(sb, settings);
            xmlDocument.Save(writer);
            return sb.ToString();
        }

        private List<PublishedNode> ReadPublishedNodes(XmlNode publishedNodesNode)
        {
            List<PublishedNode> publishedNodes = new List<PublishedNode>();
            XmlNodeList? publishedNodeList = publishedNodesNode.SelectNodes(PUBLISHED_NODE);
            if (publishedNodeList != null)
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
            XmlNodeList? publishedChildNodeList = publishedNodesNode.SelectNodes(PUBLISHED_CHILD_NODES);
            if (publishedChildNodeList != null)
            {
                foreach (XmlNode publishedChildNodesNode in publishedChildNodeList)
                {
                    PublishedChildNodes publishedChildNodes = new PublishedChildNodes();
                    Console.WriteLine(publishedChildNodes.GetType().Name);
                    publishedChildNodes.Type = ReadAttribute(publishedChildNodesNode, TYPE);
                    publishedChildNodes.NamespaceUrl = ReadAttribute(publishedChildNodesNode, NAMESPACE_URL);
                    publishedChildNodes.NodeId = ReadAttribute(publishedChildNodesNode, NODE_ID);
                    publishedChildNodes.BaseType = ReadAttribute(publishedChildNodesNode, BASE_TYPE);
                    XmlNode? filterNode = publishedChildNodesNode.SelectSingleNode(FILTER);
                    if (filterNode != null)
                    {
                        Filter filter = new Filter();
                        string? filterString = ReadAttribute(filterNode, TYPE);
                        if (filterString != null)
                        {
                            if (Enum.TryParse<FilterType>(filterString, out FilterType result))
                            {
                                filter.FilterType = result;
                            }
                            else
                            {
                                Logger.Error($"Unknown FilterType in node {filterNode} in attribute {TYPE}: {filterString}");
                            }
                        }
                        XmlNodeList? conditionsNodeList = filterNode.SelectNodes(CONDITIONS);
                        if (conditionsNodeList != null)
                        {
                            List<Conditions> conditionsList = new List<Conditions>();
                            foreach (XmlNode conditionsNode in conditionsNodeList)
                            {
                                Conditions conditions = new Conditions();
                                string? conditionsTypeString = ReadAttribute(conditionsNode, TYPE);
                                if (conditionsTypeString != null)
                                {
                                    if (Enum.TryParse<ConditionType>(conditionsTypeString, out ConditionType result))
                                    {
                                        conditions.ConditionType = result;
                                    }
                                    else
                                    {
                                        Logger.Error($"Unknown ConditionType in node {conditionsNode} in attribute {TYPE}: {conditionsTypeString}");
                                    }
                                }
                                List<Condition> conditionList = new List<Condition>();
                                XmlNodeList? typeIDConditionNodeList = conditionsNode.SelectNodes(TYPE_ID_CONDITION);
                                if (typeIDConditionNodeList != null)
                                {
                                    foreach (XmlNode typeIdConditionNode in typeIDConditionNodeList)
                                    {
                                        TypeIdCondition typeIdCondition = new TypeIdCondition();
                                        typeIdCondition.Type = ReadAttribute(typeIdConditionNode, TYPE);
                                        typeIdCondition.NamespaceUrl = ReadAttribute(typeIdConditionNode, NAMESPACE_URL);
                                        typeIdCondition.NodeId = ReadAttribute(typeIdConditionNode, NODE_ID);
                                        conditionList.Add(typeIdCondition);
                                    }
                                }
                                XmlNodeList? relationConditionNodeList = conditionsNode.SelectNodes(RELATION_CONDITION);
                                if (relationConditionNodeList != null)
                                {
                                    foreach (XmlNode relationConditionNode in relationConditionNodeList)
                                    {
                                        RelationCondition relationCondition = new RelationCondition();
                                        relationCondition.Type = ReadAttribute(relationConditionNode, TYPE);
                                        relationCondition.NamespaceUrl = ReadAttribute(relationConditionNode, NAMESPACE_URL);
                                        relationCondition.NodeId = ReadAttribute(relationConditionNode, NODE_ID);
                                        string includeSubTypes = ReadAttribute(relationConditionNode, INCLUDE_SUB_TYPES);
                                        relationCondition.IncludeSubTypes = string.Equals(includeSubTypes, "true", StringComparison.OrdinalIgnoreCase) ? true : false;
                                        conditionList.Add(relationCondition);
                                    }
                                }
                                conditions.ConditionList = conditionList;
                                conditionsList.Add(conditions);
                            }
                            filter.ConditionsList = conditionsList;
                        }
                        publishedChildNodes.Filter.Add(filter);
                    }
                    publishedNodes.Add(publishedChildNodes);
                }
            }
            return publishedNodes;
        }
        private XmlNode CreatePublishedNodesNode(XmlDocument xmlDocument, List<PublishedNode> publishedNodes)
        {
            XmlElement publishedNodesNode = xmlDocument.CreateElement(PUBLISHED_NODES);
            foreach (PublishedNode publishedNodeObj in publishedNodes)
            {
                Console.WriteLine(publishedNodeObj.GetType().Name);
                switch (publishedNodeObj)
                {
                    case PublishedChildNodes publishedChildNodes:
                        XmlElement publishedChildNodesNode = xmlDocument.CreateElement(PUBLISHED_CHILD_NODES);
                        XmlAttribute typeNode = xmlDocument.CreateAttribute(TYPE);
                        typeNode.Value = publishedChildNodes.Type;
                        XmlAttribute namespaceUrlNode = xmlDocument.CreateAttribute(NAMESPACE_URL);
                        namespaceUrlNode.Value = publishedChildNodes.NamespaceUrl;
                        XmlAttribute nodeIdNode = xmlDocument.CreateAttribute(NODE_ID);
                        nodeIdNode.Value = publishedChildNodes.NodeId;
                        XmlAttribute basetypeNode = xmlDocument.CreateAttribute(BASE_TYPE);
                        basetypeNode.Value = publishedChildNodes.BaseType;
                        publishedChildNodesNode.Attributes.Append(typeNode);
                        publishedChildNodesNode.Attributes.Append(namespaceUrlNode);
                        publishedChildNodesNode.Attributes.Append(nodeIdNode);
                        publishedChildNodesNode.Attributes.Append(basetypeNode);
                        foreach (Filter filter in publishedChildNodes.Filter)
                        {
                            XmlElement filterNode = xmlDocument.CreateElement(FILTER);
                            XmlAttribute filterType = xmlDocument.CreateAttribute(TYPE);
                            filterType.Value = filter.FilterType.ToString();
                            filterNode.Attributes.Append(filterType);
                            foreach (Conditions conditions in filter.ConditionsList)
                            {
                                XmlElement conditionsNode = xmlDocument.CreateElement(CONDITIONS);
                                XmlAttribute conditionsType = xmlDocument.CreateAttribute(TYPE);
                                conditionsType.Value = conditions.ConditionType.ToString();
                                conditionsNode.Attributes.Append(conditionsType);
                                foreach (Condition condition in conditions.ConditionList)
                                {
                                    switch (condition)
                                    {
                                        case TypeIdCondition typeIdCondition:
                                            XmlElement typeIdConditionNode = xmlDocument.CreateElement(TYPE_ID_CONDITION);
                                            XmlAttribute typeIdConditionType = xmlDocument.CreateAttribute(TYPE);
                                            typeIdConditionType.Value = typeIdCondition.Type.ToString();
                                            XmlAttribute typeIdConditionTypeNamespaceUrl = xmlDocument.CreateAttribute(NAMESPACE_URL);
                                            typeIdConditionTypeNamespaceUrl.Value = typeIdCondition.NamespaceUrl;
                                            XmlAttribute typeIdConditionTypeNodeId = xmlDocument.CreateAttribute(NODE_ID);
                                            typeIdConditionTypeNodeId.Value = typeIdCondition.NodeId.ToString();
                                            typeIdConditionNode.Attributes.Append(typeIdConditionType);
                                            typeIdConditionNode.Attributes.Append(typeIdConditionTypeNamespaceUrl);
                                            typeIdConditionNode.Attributes.Append(typeIdConditionTypeNodeId);
                                            conditionsNode.AppendChild(typeIdConditionNode);
                                            break;
                                        case RelationCondition relationCondition:
                                            XmlElement relationConditionNode = xmlDocument.CreateElement(RELATION_CONDITION);
                                            XmlAttribute relationConditionType = xmlDocument.CreateAttribute(TYPE);
                                            relationConditionType.Value = relationCondition.Type.ToString();
                                            XmlAttribute relationConditionTypeNamespaceUrl = xmlDocument.CreateAttribute(NAMESPACE_URL);
                                            relationConditionTypeNamespaceUrl.Value = relationCondition.NamespaceUrl;
                                            XmlAttribute relationConditionTypeNodeId = xmlDocument.CreateAttribute(NODE_ID);
                                            relationConditionTypeNodeId.Value = relationCondition.NodeId.ToString();
                                            XmlAttribute relationConditionTypeIncludeSubTypes = xmlDocument.CreateAttribute(INCLUDE_SUB_TYPES);
                                            relationConditionTypeIncludeSubTypes.Value = relationCondition.IncludeSubTypes.ToString();
                                            relationConditionNode.Attributes.Append(relationConditionType);
                                            relationConditionNode.Attributes.Append(relationConditionTypeNamespaceUrl);
                                            relationConditionNode.Attributes.Append(relationConditionTypeNodeId);
                                            relationConditionNode.Attributes.Append(relationConditionTypeIncludeSubTypes);
                                            conditionsNode.AppendChild(relationConditionNode);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                filterNode.AppendChild(conditionsNode);
                            }
                            publishedChildNodesNode.AppendChild(filterNode);
                        }
                        publishedNodesNode.AppendChild(publishedChildNodesNode);
                        break;
                    case PublishedNode publishedNode:
                        XmlElement publishedNodeNode = xmlDocument.CreateElement(PUBLISHED_NODE);
                        XmlAttribute typeNode1 = xmlDocument.CreateAttribute(TYPE);
                        typeNode1.Value = publishedNode.Type;
                        XmlAttribute namespaceUrlNode1 = xmlDocument.CreateAttribute(NAMESPACE_URL);
                        namespaceUrlNode1.Value = publishedNode.NamespaceUrl;
                        XmlAttribute nodeIdNode1 = xmlDocument.CreateAttribute(NODE_ID);
                        nodeIdNode1.Value = publishedNode.NodeId;
                        XmlAttribute basetypeNode1 = xmlDocument.CreateAttribute(BASE_TYPE);
                        basetypeNode1.Value = publishedNode.BaseType;
                        publishedNodeNode.Attributes.Append(typeNode1);
                        publishedNodeNode.Attributes.Append(namespaceUrlNode1);
                        publishedNodeNode.Attributes.Append(nodeIdNode1);
                        publishedNodeNode.Attributes.Append(basetypeNode1);
                        publishedNodesNode.AppendChild(publishedNodeNode);
                        break;
                    default:
                        Logger.Error($"Unknown SubType of Published Node: {publishedNodeObj.GetType()}");
                        break;
                }
            }
            return publishedNodesNode;
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
