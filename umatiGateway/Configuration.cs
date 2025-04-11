// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Opc.Ua;
using UmatiGateway.OPC.CustomEncoding;

namespace UmatiGateway
{
    /// <summary>
    /// This class holds the configuration for the UMATI Gateway. It stores the start conditions
    /// and the settings vor the connections as well as the published Nodes.
    /// </summary>
    public class Configuration
    {
        public string gatewayConfigVersion = "";
        public string configFilePath = "";
        public bool autostart = false;
        public bool readExtraLibs = false;
        public bool singleThreadPolling = false;
        public int pollTime = 2000;
        public string loglevel = "Info";

        public string configVersion = "";
        public string opcServerEndpoint = "";
        public string opcAuthentication = "";
        public string opcUser = "";
        public string opcPassword = "";
        public string mqttServerEndpopint = "";
        public string mqttUser = "";
        public string mqttPassword = "";
        public string mqttClientId = "";
        public string mqttPrefix = "";
        public string mqttCertificateFile = "";
        public string mqttCertificatePassword = "";
        public List<PublishedNode> publishedNodes = [];
        public List<CustomEncoding> customEncodings = [];
        public Configuration() { }
    }
    public class PublishedNode()
    {
        public string Type { get; set; } = "";
        public string NamespaceUrl { get; set; } = "";
        public string NodeId { get; set; } = "";
        public string BaseType { get; set; } = "";
    }
    public class CustomEncoding()
    {
        public string Name { get; set; } = "";
        public bool Active { get; set; } = false;
    }
}
