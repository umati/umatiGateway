// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Opc.Ua;

namespace umatiGateway.Core.Configuration
{
    /// <summary>
    /// This class holds the configuration for the UMATI Gateway. It stores the start conditions
    /// and the settings vor the connections as well as the published Nodes.
    /// </summary>
    public class UmatiConfiguration
    {
        public string Version { get; set; } = "";
        public string LogLevel { get; set; } = "Info";
        public StartConfiguration StartConfiguration { get; set; } = new StartConfiguration();
        public WebUI WebUI { get; set; } = new WebUI();
        public OPCConnection OPCConnection { get; set; } = new OPCConnection();
        public MqttProviderConfig MqttProviderConfig { get; set; } = new MqttProviderConfig();
        public PubSubProviderConfig PubSubProviderConfig { get; set; } = new PubSubProviderConfig();
        public override string ToString()
        {
            return $"UmatiConfiguration:\n" +
                   $"- Version: {Version}\n" +
                   $"- LogLevel: {LogLevel}\n" +
                   $"- StartConfiguration: {StartConfiguration}\n" +
                   $"- WebUI: {WebUI}\n" +
                   $"- OPCConnection: {OPCConnection}\n" +
                   $"- MqttProviderConfig: {MqttProviderConfig}\n" +
                   $"- PubSubProviderConfig: {PubSubProviderConfig}";
        }

    }
    public class PublishedNode
    {
        public string Type { get; set; } = "";
        public string NamespaceUrl { get; set; } = "";
        public string NodeId { get; set; } = "";
        public string BaseType { get; set; } = "";
        public override string ToString()
        {
            return $"PublishedNode(Type={Type}, NamespaceUrl={NamespaceUrl}, NodeId={NodeId}, BaseType={BaseType})";
        }
    }
    public class PublishedChildNodes : PublishedNode
    {
        public List<Filter> Filter { get; set; } = new List<Filter>();
        public PublishedChildNodes()
        {

        }
    }
    public class Filter
    {
        public FilterType FilterType { get; set; }
        public List<Conditions> ConditionsList { get; set; } = new List<Conditions> ();
    }
    public enum FilterType
    {
        Blacklist,
        Whitelist
    }
    public class Conditions
    {
        public ConditionType ConditionType { get; set; } = ConditionType.And;
        public List<Condition> ConditionList { get; set; } = new List<Condition>();
    }
    public class Condition
    {
        
    }
    public enum ConditionType
    {
        And,
        Or
    }
    public class TypeIdCondition : Condition
    {
        public string Type { get; set; } = "";
        public string NamespaceUrl { get; set; } = "";
        public string NodeId { get; set; } = "";
    }
    public class RelationCondition : Condition
    {
        public string Type { get; set; } = "";
        public string NamespaceUrl { get; set; } = "";
        public string NodeId { get; set; } = "";
        public bool IncludeSubTypes { get; set; } = false;
    }

    public class CustomEncoding
    {
        public string Name { get; set; } = "";
        public bool Active { get; set; } = false;
        public override string ToString()
        {
            return $"CustomEncoding(Name={Name}, Active={Active})";
        }
    }
    public class WebUI
    {
        public string URL { get; set; } = "";
        public override string ToString()
        {
            return $"WebUI(URL={URL})";
        }
    }
    public class MqttProviderConfig
    {
        public string ServerEndpoint { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string Prefix { get; set; } = "";
        public bool IncludeStructuredComponents { get; set; } = false;
        public uint PublishInterval { get; set; } = 5000;

        public List<CustomEncoding> CustomEncodings { get; set; } = new List<CustomEncoding>();
        public List<PublishedNode> PublishedNodes { get; set; } = new List<PublishedNode>();
        public override string ToString()
        {
            var encodings = string.Join(", ", CustomEncodings.Select(e => e.ToString()));
            var nodes = string.Join(", ", PublishedNodes.Select(n => n.ToString()));
            return $"MqttProviderConfig(Server={ServerEndpoint}, User={UserName}, Password={UmatiConfigurationUtils.MaskPassword(Password)}, " +
                   $"ClientId={ClientId}, Prefix={Prefix}, Interval={PublishInterval}, Structured={IncludeStructuredComponents}, CustomEncodings=[{encodings}], " +
                   $"PublishedNodes=[{nodes}])";
        }
    }
    public class PubSubProviderConfig
    {
        public string ServerEndpoint { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string Prefix { get; set; } = "";
        public List <PublishedNode> PublishedNodes { get; set; } = new List<PublishedNode> { };
        public override string ToString()
        {
            var nodes = string.Join(", ", PublishedNodes.Select(n => n.ToString()));
            return $"PubSubProviderConfig(Server={ServerEndpoint}, User={UserName}, Password={UmatiConfigurationUtils.MaskPassword(Password)}, " +
                   $"ClientId={ClientId}, Prefix={Prefix}, PublishedNodes=[{nodes}])";
        }
    }
    public class OPCConnection
    {
        public string ServerEndpoint { get; set; } = "";
        public string Authentication { get; set; } = "NONE";
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
        public bool ReadExtraLibs { get; set; } = false;
        public override string ToString()
        {
            return $"OPCConnection(Server={ServerEndpoint}, Auth={Authentication}, User={UserName}, Password={UmatiConfigurationUtils.MaskPassword(Password)}, ReadExtraLibs={ReadExtraLibs})";
        }
    }
    public class StartConfiguration
    {
        public bool StartWebUI { get; set; } = false;
        public bool StartOPCConnection { get; set; } = false;
        public bool StartMQTTProvider { get; set; } = false;
        public bool StartPubSubProvider { get; set; } = false;
        public override string ToString()
        {
            return $"StartConfiguration(WebUI={StartWebUI}, OPC={StartOPCConnection}, MQTT={StartMQTTProvider}, PubSub={StartPubSubProvider})";
        }
    }
    
}
