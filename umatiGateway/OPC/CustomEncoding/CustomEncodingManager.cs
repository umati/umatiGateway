// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using System.Text;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Opc.Ua;
using umatiGateway.OPC.CustomEncoding;

namespace UmatiGateway.OPC.CustomEncoding
{
    /// <summary>
    /// This class is used to manage Custom Encodings.
    /// </summary>
    public class CustomEncodingManager
    {
        public IList<ManagedCustomEncoding> managedCustomEncodings { get; set; } = new List<ManagedCustomEncoding>();
        public CustomEncodingManager()
        {
            this.managedCustomEncodings.Add(new ManagedCustomEncoding(new GMSResultDataTypeEncoding()));
            this.managedCustomEncodings.Add(new ManagedCustomEncoding(new ProcessingCategoryDataTypeEncoding()));
        }
        public ManagedCustomEncoding? GetManagedCustomEncodingByName(string name)
        {
            foreach (ManagedCustomEncoding managedCustomEncoding in this.managedCustomEncodings)
            {
                ICustomEncoding customEncoding = managedCustomEncoding.CustomEncoding;
                if (customEncoding.FullFieldName == name) return managedCustomEncoding;
            }
            return null;
        }
        public ICustomEncoding? GetActiveEncodingForNodeId(ExpandedNodeId nodeId)
        {
            foreach (ManagedCustomEncoding managedCustomEncoding in this.managedCustomEncodings)
            {
                ICustomEncoding customEncoding = managedCustomEncoding.CustomEncoding;
                if (customEncoding.NodeId == nodeId && managedCustomEncoding.IsActive == true) return customEncoding;
            }
            return null;
        }
        /// <summary>
        /// Reads the configuration for the custom encodings from the given file.
        /// </summary>
        /// <param name="file">The file from which the configuration is read.</param>
        public void ReadConfiguration(string file)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(file);
            XmlNodeList? customEncodingNodes = xmlDoc.SelectNodes("/Configuration/CustomEncodings/CustomEncoding");
            if (customEncodingNodes != null)
            {
                foreach (XmlNode customEncodingNode in customEncodingNodes)
                {
                    if (customEncodingNode.Attributes != null)
                    {
                        bool encodingActive = false;
                        string? name = customEncodingNode.Attributes["name"]?.Value;
                        string? active = customEncodingNode.Attributes["active"]?.Value;
                        if (active != null && active.Equals("True", StringComparison.OrdinalIgnoreCase))
                        {
                            encodingActive = true;
                        }
                        if (name != null)
                        {
                            ManagedCustomEncoding? managedCustomEncoding = this.GetManagedCustomEncodingByName(name);
                            if (managedCustomEncoding != null) managedCustomEncoding.IsActive = encodingActive;
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("No nodes for Custom Encodings found.");
            }

        }
        /// <summary>
        /// Stores the configuration for the custom encodings to the given file.
        /// </summary>
        /// <param name="file">The file to which the configuration is stored.</param>
        public void SaveConfiguration(string file)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(file);
            XmlNode? customEncodingsNode = xmlDoc.SelectSingleNode("/Configuration/CustomEncodings");
            if (customEncodingsNode == null)
            {
                XmlNode? configurationNode = xmlDoc.SelectSingleNode("/Configuration");
                if (configurationNode != null)
                {
                    customEncodingsNode = xmlDoc.CreateElement("CustomEncodings");
                    configurationNode.AppendChild(customEncodingsNode);
                }
            }
            if (customEncodingsNode != null)
            {
                customEncodingsNode.RemoveAll();
                foreach (ManagedCustomEncoding managedCustomEncoding in this.managedCustomEncodings)
                {
                    XmlElement managedCustomEncodingNode = xmlDoc.CreateElement("CustomEncoding");
                    customEncodingsNode.AppendChild(managedCustomEncodingNode);
                    XmlAttribute name = xmlDoc.CreateAttribute("name");
                    name.Value = managedCustomEncoding.CustomEncoding.FullFieldName;
                    XmlAttribute active = xmlDoc.CreateAttribute("active");
                    active.Value = managedCustomEncoding.IsActive.ToString();
                    managedCustomEncodingNode.Attributes.Append(name);
                    managedCustomEncodingNode.Attributes.Append(active);
                }
            }
            else
            {
                Console.WriteLine("Unable to save Custom ENcodings");
            }

            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineOnAttributes = false, Encoding = Encoding.UTF8 };
            XmlWriter writer = XmlWriter.Create(file, settings);
            xmlDoc.Save(writer);
            writer.Close();
        }
    }
    public class ManagedCustomEncoding
    {
        public ICustomEncoding CustomEncoding { get; }
        public bool IsActive { get; set; } = false;
        public ManagedCustomEncoding(ICustomEncoding CustomEncoding)
        {
            this.CustomEncoding = CustomEncoding;
        }
    }
}
