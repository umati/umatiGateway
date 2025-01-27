// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Opc.Ua;
using System.Xml;

namespace UmatiGateway
{
    public class ConfigurationReader
    {
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
                        if (string.Equals(autostart, "true", StringComparison.OrdinalIgnoreCase))
                        {
                            configuration.autostart = true;
                        }
                        else
                        {
                            configuration.autostart = false;
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
                                Console.WriteLine(e.Message);
                            }
                        }
                        configuration.configFilePath = this.ReadAttribute(node, "file");
                        if (string.IsNullOrWhiteSpace(configuration.configFilePath))
                        {
                            configuration.configFilePath = "";
                        }
                        else
                        {
                            this.ReadConfigFile(configuration);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Root Node not found!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error on Loading ConfigurationFile." + ex.ToString());
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
                    Console.WriteLine($"Error on Rerading Configuration: Attribute \"{node.Name}\" of node \"{attributeName}\" is missing.");
                    value = "";
                }
            }
            else
            {
                Console.WriteLine($"Error on Reading Configuration: Attribute \"{attributeName}\" of node \"{node.Name}\" is missing. The node \"{node.Name}\" does not contain attributes.");
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
                            Console.WriteLine("OPCNode not found!");
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
                            Console.WriteLine("MqttNode not found!");
                        }
                        XmlNodeList? publishedNodes = xmlDoc.SelectNodes("/Configuration/PublishedNodes/PublishedNode");
                        if (publishedNodes != null)
                        {
                            foreach (XmlNode publishedNode in publishedNodes)
                            {
                                PublishedNode published = new PublishedNode();
                                published.type = this.ReadAttribute(publishedNode, "type");
                                published.namespaceUrl = this.ReadAttribute(publishedNode, "namespaceurl");
                                published.nodeId = this.ReadAttribute(publishedNode, "nodeId");
                                configuration.publishedNodes.Add(published);
                            }
                        }
                        else
                        {
                            Console.WriteLine("No nodes to pubish found");
                        }

                    }
                    else
                    {
                        Console.WriteLine($"Wrong Version \"{configuration.gatewayConfigVersion}\".");
                    }
                }
                else
                {
                    Console.WriteLine("Root Node not found!");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error on Loading ConfigurationFile \"{configuration.configFilePath}\"." + ex.ToString());
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
                            configuration.opcServerEndpoint = this.ReadAttribute(opcNode, "serverendpoint");
                            configuration.opcAuthentication = this.ReadAttribute(opcNode, "authentication");
                            configuration.opcUser = this.ReadAttribute(opcNode, "user");
                            configuration.opcPassword = this.ReadAttribute(opcNode, "password");

                        }
                        else
                        {
                            Console.WriteLine("OPCNode not found!");
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
                            Console.WriteLine("MqttNode not found!");
                        }
                        XmlNodeList? publishedNodes = xmlDoc.SelectNodes("/Configuration/PublishedNodes/PublishedNode");
                        if (publishedNodes != null)
                        {
                            foreach (XmlNode publishedNode in publishedNodes)
                            {
                                PublishedNode published = new PublishedNode();
                                published.type = this.ReadAttribute(publishedNode, "type");
                                published.namespaceUrl = this.ReadAttribute(publishedNode, "namespaceurl");
                                published.nodeId = this.ReadAttribute(publishedNode, "nodeId");
                                configuration.publishedNodes.Add(published);
                            }
                        }
                        else
                        {
                            Console.WriteLine("No nodes to pubish found");
                        }

                    }
                    else
                    {
                        Console.WriteLine($"Wrong Version \"{configuration.gatewayConfigVersion}\".");
                    }
                }
                else
                {
                    Console.WriteLine("Root Node not found!");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error on Loading ConfigurationFile \"{configuration.configFilePath}\"." + ex.ToString());
            }

        }

    }
}
