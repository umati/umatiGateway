// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using System.Text;
using System.Xml;

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
