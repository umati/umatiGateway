// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace UmatiGateway
{
    public class ConfigurationWriter
    {
        public ConfigurationWriter() { }
        public void WriteConfiguration(Configuration configuration)
        {
            XmlDocument xmlDocument = new XmlDocument();
            XmlDeclaration xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDocument.InsertBefore(xmlDeclaration, xmlDocument.DocumentElement);
            XmlElement node = xmlDocument.CreateElement("umatiGatewayConfig");
            XmlAttribute version = xmlDocument.CreateAttribute("version");
            version.Value = "1.0";
            node.Attributes.Append(version);
            XmlAttribute autostart = xmlDocument.CreateAttribute("autostart");
            autostart.Value = configuration.autostart.ToString();
            node.Attributes.Append(autostart);
            XmlAttribute file = xmlDocument.CreateAttribute("file");
            file.Value = configuration.configFilePath;
            node.Attributes.Append(file);
            xmlDocument.AppendChild(node);
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineOnAttributes = false, Encoding = Encoding.UTF8 };
            XmlWriter writer = XmlWriter.Create("./Configuration/umatiGatewayConfig.xml", settings);
            xmlDocument.Save(writer);
            writer.Close();
        }
        /// <summary>
        /// Returns the Application Configuration as string.
        /// </summary>
        /// <param name="configuration">The configuration that is to be returned as xml formatted string.</param>
        /// <returns>The xml formatted Application Configuration.</returns>
        public string WriteApplicationConfigToString(Configuration configuration)
        {
            XmlDocument xmlDocument = new XmlDocument();
            XmlDeclaration xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDocument.InsertBefore(xmlDeclaration, xmlDocument.DocumentElement);
            XmlElement umatiGatewayConfigNode = xmlDocument.CreateElement("umatiGatewayConfig");
            XmlAttribute version = xmlDocument.CreateAttribute("version");
            version.Value = "1.0";
            umatiGatewayConfigNode.Attributes.Append(version);
            XmlAttribute autostart = xmlDocument.CreateAttribute("autostart");
            autostart.Value = configuration.autostart.ToString();
            umatiGatewayConfigNode.Attributes.Append(autostart);
            XmlAttribute logLevel = xmlDocument.CreateAttribute("logLevel");
            logLevel.Value = configuration.loglevel;
            umatiGatewayConfigNode.Attributes.Append(logLevel);
            XmlAttribute file = xmlDocument.CreateAttribute("file");
            file.Value = configuration.configFilePath;
            umatiGatewayConfigNode.Attributes.Append(file);
            XmlAttribute readExtraLibs = xmlDocument.CreateAttribute("ReadExtraLibs");
            readExtraLibs.Value = configuration.readExtraLibs.ToString();
            umatiGatewayConfigNode.Attributes.Append(readExtraLibs);
            XmlAttribute includeStructuredComponents = xmlDocument.CreateAttribute("includeStructuredComponents");
            includeStructuredComponents.Value = configuration.includeStructuredComponents.ToString();
            umatiGatewayConfigNode.Attributes.Append(includeStructuredComponents);
            XmlAttribute pollTime = xmlDocument.CreateAttribute("pollTime");
            pollTime.Value = configuration.pollTime.ToString();
            umatiGatewayConfigNode.Attributes.Append(pollTime);
            xmlDocument.AppendChild(umatiGatewayConfigNode);
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineOnAttributes = false, Encoding = Encoding.UTF8 };
            StringBuilder sb = new StringBuilder();
            XmlWriter writer = XmlWriter.Create(sb, settings);
            xmlDocument.Save(writer);
            return sb.ToString();

        }
        /// <summary>
        /// Returns the given Configuration as a xml formatted string.
        /// </summary>
        /// <param name="configuration">The configuration that is to be returned as xml formatted string.</param>
        /// <returns>The xml formatted configuration.</returns>
        public string WriteToString(Configuration configuration)
        {
            XmlDocument xmlDocument = new XmlDocument();
            XmlDeclaration xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDocument.InsertBefore(xmlDeclaration, xmlDocument.DocumentElement);
            XmlElement configurationNode = xmlDocument.CreateElement("Configuration");
            XmlAttribute version = xmlDocument.CreateAttribute("version");
            version.Value = "1.0";
            configurationNode.Attributes.Append(version);
            XmlElement opcConnectionNode = xmlDocument.CreateElement("OPCConnection");
            XmlAttribute opcServerEndpoint = xmlDocument.CreateAttribute("serverendpoint");
            opcServerEndpoint.Value = configuration.opcServerEndpoint;
            XmlAttribute opcAuthentication = xmlDocument.CreateAttribute("authentication");
            opcAuthentication.Value = configuration.opcAuthentication;
            XmlAttribute opcUser = xmlDocument.CreateAttribute("user");
            opcUser.Value = configuration.opcUser;
            XmlAttribute opcPassword = xmlDocument.CreateAttribute("password");
            opcPassword.Value = configuration.opcPassword;
            opcConnectionNode.Attributes.Append(opcServerEndpoint);
            opcConnectionNode.Attributes.Append(opcAuthentication);
            opcConnectionNode.Attributes.Append(opcUser);
            opcConnectionNode.Attributes.Append(opcPassword);
            XmlElement mqttConnectionNode = xmlDocument.CreateElement("MqttConnection");
            XmlAttribute mqttServerEndpoint = xmlDocument.CreateAttribute("serverendpoint");
            mqttServerEndpoint.Value = configuration.mqttServerEndpopint;
            XmlAttribute mqttUser = xmlDocument.CreateAttribute("user");
            mqttUser.Value = configuration.mqttUser;
            XmlAttribute mqttPassword = xmlDocument.CreateAttribute("password");
            mqttPassword.Value = configuration.mqttPassword;
            XmlAttribute mqttClientId = xmlDocument.CreateAttribute("clientId");
            mqttClientId.Value = configuration.mqttClientId;
            XmlAttribute mqttPrefix = xmlDocument.CreateAttribute("prefix");
            mqttPrefix.Value = configuration.mqttPrefix;
            mqttConnectionNode.Attributes.Append(mqttServerEndpoint);
            mqttConnectionNode.Attributes.Append(mqttUser);
            mqttConnectionNode.Attributes.Append(mqttPassword);
            mqttConnectionNode.Attributes.Append(mqttClientId);
            mqttConnectionNode.Attributes.Append(mqttPrefix);
            XmlElement publishedNodesNode = xmlDocument.CreateElement("PublishedNodes");
            foreach (PublishedNode publishedNode in configuration.publishedNodes)
            {
                XmlElement publishedNodeNode = xmlDocument.CreateElement("PublishedNode");
                XmlAttribute typeNode = xmlDocument.CreateAttribute("type");
                typeNode.Value = publishedNode.Type;
                XmlAttribute namespaceUrlNode = xmlDocument.CreateAttribute("namespaceUrl");
                namespaceUrlNode.Value = publishedNode.NamespaceUrl;
                XmlAttribute nodeIdNode = xmlDocument.CreateAttribute("nodeId");
                nodeIdNode.Value = publishedNode.NodeId;
                XmlAttribute basetypeNode = xmlDocument.CreateAttribute("BaseType");
                basetypeNode.Value = publishedNode.BaseType;
                publishedNodeNode.Attributes.Append(typeNode);
                publishedNodeNode.Attributes.Append(namespaceUrlNode);
                publishedNodeNode.Attributes.Append(nodeIdNode);
                publishedNodeNode.Attributes.Append(basetypeNode);
                publishedNodesNode.AppendChild(publishedNodeNode);
            }
            XmlElement customEncodingsNode = xmlDocument.CreateElement("CustomEncodings");
            foreach (CustomEncoding customEncoding in configuration.customEncodings)
            {
                XmlElement customEncodingNode = xmlDocument.CreateElement("CustomEncoding");
                XmlAttribute nameNode = xmlDocument.CreateAttribute("name");
                nameNode.Value = customEncoding.Name;
                XmlAttribute active = xmlDocument.CreateAttribute("active");
                active.Value = customEncoding.Active.ToString();
                customEncodingNode.Attributes.Append(nameNode);
                customEncodingNode.Attributes.Append(active);
                customEncodingsNode.AppendChild(customEncodingNode);
            }
            configurationNode.AppendChild(opcConnectionNode);
            configurationNode.AppendChild(mqttConnectionNode);
            configurationNode.AppendChild(publishedNodesNode);
            configurationNode.AppendChild(customEncodingsNode);
            xmlDocument.AppendChild(configurationNode);
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineOnAttributes = false, Encoding = Encoding.UTF8 };
            StringBuilder sb = new StringBuilder();
            XmlWriter writer = XmlWriter.Create(sb, settings);
            xmlDocument.Save(writer);
            return sb.ToString();

        }
        public void SaveSettings()
        {
            XmlDocument xmlDocument = new XmlDocument();
            XmlElement node = xmlDocument.CreateElement("umatiGatewayConfig");
            XmlAttribute version = xmlDocument.CreateAttribute("version");
            version.Value = "1.0";
            node.Attributes.Append(version);
            XmlAttribute autostart = xmlDocument.CreateAttribute("autostart");
            autostart.Value = "1.0";
            node.Attributes.Append(autostart);
            XmlAttribute file = xmlDocument.CreateAttribute("file");
            file.Value = "1.0";
            node.Attributes.Append(file);
            xmlDocument.AppendChild(node);
        }
    }
}
