// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Authentication;
using System.Threading.Tasks;
using MQTTnet;
using System.Threading;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using Opc.Ua.Client.ComplexTypes;
using Microsoft.AspNetCore.Authentication;
using Opc.Ua.Schema.Binary;
using Org.BouncyCastle.Utilities.Encoders;
using System.Reflection.PortableExecutable;
using Org.BouncyCastle.Crypto.IO;
using static UmatiGateway.OPC.MqttProvider;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Opc.Ua.Client;
using System.IO;
using System.Text.Json.Nodes;
using System.Timers;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Xml;
using Org.BouncyCastle.Utilities;
using System.Reflection.Metadata;
using Org.BouncyCastle.Tls.Crypto;
using System.Collections.Concurrent;
using NLog;
using UmatiGateway.OPC.CustomEncoding;


namespace UmatiGateway.OPC
{
    /// <summary>
    /// This class reads the data from the OPC UA Server and provides it via Mqtt.
    /// </summary>
    public class MqttProvider : OpcUaEventListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private MqttClientFactory mqttFactory = new MqttClientFactory();
        private IMqttClient? mqttClient = null;
        private const string CLIENT_ID = "TestClient";
        private const string TCP = "tcp";
        private const string WEBSOCKET = "websocket";
        public CustomEncodingManager customEncodingManager = new CustomEncodingManager();
        public string connectionType = "";
        public string connectionString = "";
        public string connectionPort = "";
        public string user = "";
        public string pwd = "";
        public string mqttPrefix = "";
        public string clientId = "";
        public bool useGMSResultEncoding = false;
        public List<MachineNode> publishedMachines = new List<MachineNode>();
        public List<PublishedNode> publishedNodes = new List<PublishedNode>();
        public List<NodeId> onlineMachines = new List<NodeId>();
        private UmatiGatewayApp client;
        private Dictionary<NodeId, string> MqttValues = new Dictionary<NodeId, string>();
        private Boolean connected = false;
        private System.Timers.Timer aTimer = new System.Timers.Timer();
        private bool firstReadFinished = false;
        private bool debug = false;
        private bool ReadInProgress = false;
        public bool singleThreadPolling = false;
        public bool ConnectedOnce = false;
        public int PollTimer = 60000;
        public bool TimerSetup = false;
        public Dictionary<NodeId, IList<string>> errors = new Dictionary<NodeId, IList<string>>();
        private Dictionary<NodeId, MqttSubscription> subscriptions = new Dictionary<NodeId, MqttSubscription>();
        private Dictionary<NodeId, PublishedBrowsePaths> knownBrowsePaths = new Dictionary<NodeId, PublishedBrowsePaths>();
        public volatile JObject machine = new JObject();
        private string InstanceNSU = "";
        private string TypeBrowseName = "";
        private JArray IdentificationArray = new JArray();
        private JSONConverter jsonConverter = new JSONConverter();
        private bool shortenVariables = true;
        private BlockingCollection<NodeId> changedNodes = new BlockingCollection<NodeId>();
        private List<MachineNode> machineNodes = new List<MachineNode>();

        public MqttProvider(UmatiGatewayApp client)
        {
            this.client = client;
            this.mqttClient = mqttFactory.CreateMqttClient();
        }

        // Connection/Disconnection
        public void Disconnect()
        {
            Console.WriteLine("Disconnecting");
            AsyncHelper.RunSync(() => this.mqttClient.DisconnectAsync());
            this.connected = false;
            Console.WriteLine("Disconnected");
        }
        public void Connect()
        {
            if (!TimerSetup)
            {
                aTimer.Interval = PollTimer;
                aTimer.Elapsed += OnTimedEvent;
                aTimer.AutoReset = true;
                aTimer.Enabled = true;
                TimerSetup = true;
            }

            this.ConnectedOnce = true;
            this.connectionType = WEBSOCKET;
            this.Connect(this.connectionString, this.connectionType, this.connectionPort, this.user, this.pwd);
            foreach (PublishedNode publishedNode in this.publishedNodes)
            {
                int namespaceIndex = this.client.GetNamespaceTable().GetIndex(publishedNode.namespaceUrl);
                if (publishedNode.type == "Numeric")
                {
                    this.onlineMachines.Add(new NodeId(Convert.ToUInt32(publishedNode.nodeId), (ushort)namespaceIndex));
                }
                else if (publishedNode.type == "String")
                {
                    this.onlineMachines.Add(new NodeId(publishedNode.nodeId, (ushort)namespaceIndex));
                }
            }
            // Todo use a switch case here and error handling

            this.doPublish();
            this.client.ConnectEvents(this);
            aTimer.Start();

        }
        public void Reconnect()
        {
            if (!connected)
            {
                this.Connect(this.connectionString, this.connectionType, this.connectionPort, this.user, this.pwd);
            }
        }
        public void Connect(string connectionString, string connectionType, string port, string user, string pwd)
        {
            try
            {
                this.connectionString = connectionString;
                this.connectionType = connectionType;
                this.connectionPort = port;
                this.user = user;
                this.pwd = pwd;
                if (this.connectionType == TCP)
                {
                    this.Connect_Client_Using_Tcp();
                }
                else if (this.connectionType == WEBSOCKET)
                {
                    if (this.connectionString.StartsWith("mqtt"))
                    {
                        this.Connect_Client_Using_Tcp();
                    }
                    else
                    {
                        this.Connect_Client_Using_WebSockets();
                    }
                }
                else
                {
                    Console.Out.WriteLine("Unkonown Mqtt Connection Type");
                }
                connected = true;
            }
            catch (Exception e)
            {
                Logger.Error($"Exception on Connecting to MqttBroker. {e}");
                connected = false;
            }
        }
        private void Connect_Client_Using_WebSockets()
        {
            try
            {
                if (this.mqttClient != null)
                {
                    MqttClientOptions mqttClientOptions;
                    if (this.user != null && this.user != "" && this.pwd != null)
                    {
                        mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithWebSocketServer(options => { options.WithUri(this.connectionString); })
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                        .WithCredentials(this.user, this.pwd)
                        .WithTlsOptions(o => { })
                        .Build();
                    }
                    else
                    {
                        mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithWebSocketServer(options => { options.WithUri(this.connectionString); })
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                        .Build();
                    }
                    AsyncHelper.RunSync(() => this.mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None));
                }
                else
                {
                    Console.Out.WriteLine("m_mqttClient is null.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        private void Connect_Client_Using_Tcp()
        {
            if (this.mqttClient != null)
            {
                MqttClientOptions mqttClientOptions;
                if (this.connectionString != null)
                {

                    int Index = this.connectionString.LastIndexOf(":");
                    string server = this.connectionString.Substring(7, Index - 7);
                    int port1 = int.Parse(this.connectionString.Substring(Index + 1));
                    if (this.user != null && this.user != "" && this.pwd != null)
                    {

                        mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithTcpServer(server, port1)
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                        .WithCredentials(this.user, this.pwd)
                        .Build();
                    }
                    else
                    {
                        mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithTcpServer(server, port1)
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                        .Build();
                    }
                    AsyncHelper.RunSync(() => this.mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None));
                }
            }
            else
            {
                Console.Out.WriteLine("The MqttClient is null");
            }
        }

        //Publishing

        public bool WriteMessage(JObject jObject, string machineId, String type)
        {
            try
            {
                JObject sortedJsonObj = this.SortJsonKeysRecursively(jObject);
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + type + "/" + machineId;
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload(sortedJsonObj.ToString(Newtonsoft.Json.Formatting.Indented))
                .Build();
                if (this.mqttClient != null)
                {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.connected = false;
                throw;
                //return false;
            }
        }
        public bool WriteIdentification(JArray jArray, string machineId, String type)
        {
            try
            {
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "list/" + type;
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload(jArray.ToString(Newtonsoft.Json.Formatting.Indented))
                .Build();
                if (this.mqttClient != null)
                {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;

                }
                return true;
            }
            catch (Exception e)
            {
                Logger.Error("Unable to publish Identification", e);
                throw;
            }
        }
        public void publishOnlineMachines()
        {
            try
            {
                foreach (NodeId machine in this.onlineMachines)
                {
                    string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "online/";
                    MyTopic += this.getInstanceNsu(machine);
                    MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(MyTopic)
                    .WithPayload("1")
                    .Build();
                    if (this.mqttClient != null)
                    {
                        _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                //this.connected = false;
                throw;
            }
        }
        public void publishOnlineMachinesMachineNode()
        {
            try
            {
                foreach (MachineNode machineNode in this.machineNodes)
                {
                    string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "online/";
                    MyTopic += machineNode.InstanceNamespace;
                    MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(MyTopic)
                    .WithPayload("1")
                    .Build();
                    if (this.mqttClient != null)
                    {
                        _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                    }
                    else
                    {
                        //ToDo Add Error Handling
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }
        public bool publishBadList()
        {
            try
            {
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "bad_list/errors";
                JArray errorArray = new JArray();
                foreach (KeyValuePair<NodeId, IList<string>> entry in this.errors)
                {
                    JObject errorObject = new JObject();
                    errorObject.Add("NodeId", entry.Key.ToString());
                    JArray messageArray = new JArray();
                    foreach (string message in entry.Value)
                    {
                        messageArray.Add(message);
                    }
                    errorObject.Add("Messages", messageArray);
                    errorArray.Add(errorObject);
                }
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload(errorArray.ToString())
                .Build();
                if (this.mqttClient != null)
                {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to publish BadList", e.ToString());
                throw;
            }
        }
        public void PublishBadListMachineNodes()
        {
            try
            {
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "bad_list/errors";
                JArray machinesErrorArray = new JArray();
                foreach (MachineNode machineNode in this.machineNodes)
                {
                    JObject machineErrorObject = new JObject();
                    machineErrorObject.Add("Machine", machineNode.NodeIdString.ToString());
                    JArray machineErrorArray = new JArray();
                    foreach (KeyValuePair<NodeId, IList<string>> entry in machineNode.Errors)
                    {
                        JObject errorNode = new JObject();
                        errorNode.Add("NodeId", entry.Value.ToString());
                        JArray errorMessages = new JArray();
                        foreach (string error in entry.Value)
                        {
                            errorMessages.Add(error);
                        }
                        errorNode.Add("Messages", errorMessages);
                        machineErrorArray.Add(errorNode);
                    }
                    machineErrorObject.Add("Errors", machineErrorArray);
                }
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(MyTopic)
                    .WithPayload(machinesErrorArray.ToString())
                    .Build();
                if (this.mqttClient != null)
                {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                }
                else
                {
                    //TODO handling for this case
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to publish BadList", e.ToString());
                throw;
            }
        }
        public bool publishClientOnline()
        {
            try
            {
                string MyTopic = this.mqttPrefix + "/" + this.clientId + "/" + "clientOnline";
                MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic)
                .WithPayload("1")
                .Build();
                if (this.mqttClient != null)
                {
                    _ = this.mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                }
                string MyTopic1 = this.mqttPrefix + "/" + this.clientId + "/" + "gw-version";
                MqttApplicationMessage applicationMessage1 = new MqttApplicationMessageBuilder()
                .WithTopic(MyTopic1)
                .WithPayload("Umatigateway_1.0.0")
                .Build();
                if (this.mqttClient != null)
                {
                    _ = this.mqttClient.PublishAsync(applicationMessage1, CancellationToken.None).Result;
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to publish ClientOnline", e.ToString());
                throw;
            }
        }

        public void publishNodeMachineNodes()
        {
            try
            {
                foreach (MachineNode machineNode in this.machineNodes)
                {
                    NodeId? machineNodeId = machineNode.ResolvedNodeId;
                    if (machineNodeId != null)
                    {
                        JObject body = new JObject();
                        createJSON(body, machineNodeId, machineNode);
                        Node? machine = this.client.ReadNode(machineNodeId);
                        if (machine != null)
                        {
                            NodeId? typedefinition = this.client.BrowseTypeDefinition(machineNodeId);
                            if (typedefinition != null)
                            {
                                Node? TypeDefinitionNode = this.client.ReadNode(typedefinition);
                                if (TypeDefinitionNode != null)
                                {
                                    if (String.IsNullOrEmpty(machineNode.BaseType))
                                    {
                                        this.WriteMessage(body, this.getInstanceNsu(machineNodeId), TypeDefinitionNode.BrowseName.Name);
                                    } else
                                    {
                                        this.WriteMessage(body, this.getInstanceNsu(machineNodeId), machineNode.BaseType);
                                    }
                                    machineNode.Data = body;
                                }
                            }
                        }
                    }
                }


            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }
        public void publishNodeAfterSubscription()
        {
            try
            {
                foreach (NodeId machine in this.onlineMachines)
                {
                    this.WriteMessage(this.machine, this.InstanceNSU, this.TypeBrowseName);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }

        public void publishNodeAfterSubscriptionMachineNodes()
        {
            try
            {
                foreach (MachineNode machineNode in this.machineNodes)
                {
                    if (string.IsNullOrEmpty(machineNode.BaseType))
                    {
                        this.WriteMessage(machineNode.Data, machineNode.InstanceNamespace, machineNode.TypeBrowseName);
                    } else
                    {
                        this.WriteMessage(machineNode.Data, machineNode.InstanceNamespace, machineNode.BaseType);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }

        private JToken createJSON(NodeId nodeId)
        {
            JObject jObject = new JObject();
            if (nodeId != null)
            {
                Node? childNode = this.client.ReadNode(nodeId);
                if (childNode != null)
                {
                    if (childNode.NodeClass == NodeClass.Variable)
                    {
                        object dataValue = getDataValueAsObject(nodeId);
                        if (dataValue is JValue)
                        {
                            return (JValue)dataValue;
                        }
                        else if (dataValue is JObject)
                        {
                            return (JObject)dataValue;
                        }
                        else if (dataValue is JArray)
                        {
                            return (JArray)dataValue;
                        }
                    }
                }
            }
            return jObject;
        }
        private void createJSON(JObject jObject, NodeId nodeId, MachineNode? machineNode = null, NodeId? parent = null, bool subscribe = true)
        {
            // Check if for the Parent a PlaceholderRule applies

            List<NodeId> optionalMandatoryPlaceholders = this.GetOptionalAndMandatoryPlaceHolders(nodeId, parent);
            //Find possible Types for Placeholder/Children
            List<PlaceholderNode> placeholderNodes = new List<PlaceholderNode>();
            foreach (NodeId placeholder in optionalMandatoryPlaceholders)
            {
                Node? placeholderNode = this.client.ReadNode(placeholder);
                if (placeholderNode != null)
                {
                    NodeId? placeHolderTypeDefinition = this.client.BrowseTypeDefinition(placeholder);
                    string phBrowseName = placeholderNode.BrowseName.Name;
                    if (!jObject.ContainsKey(phBrowseName))
                    {
                        JObject placeholderType = new JObject();
                        jObject.Add(phBrowseName, placeholderType);
                        List<NodeId> subTypes = new List<NodeId>();
                        if (placeHolderTypeDefinition != null)
                        {
                            this.client.BrowseAllHierarchicalSubType(placeHolderTypeDefinition, subTypes);
                            placeholderNodes.Add(new PlaceholderNode(placeholder, placeHolderTypeDefinition, placeholderType, subTypes));
                        }
                    }
                }

            }

            List<NodeId> hierarchicalChilds = this.client.BrowseLocalNodeIds(nodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true);
            foreach (NodeId child in hierarchicalChilds)
            {
                JObject? placeHolderObject = null;
                NodeId? typeDefinition = this.client.BrowseTypeDefinition(child);
                if (typeDefinition != null)
                {
                    foreach (PlaceholderNode placeHolderNode in placeholderNodes)
                    {
                        if (typeDefinition == placeHolderNode.typeDefinitionNodeId || placeHolderNode.subTypeNodeIds.Contains(typeDefinition))
                        {
                            placeHolderObject = placeHolderNode.phList;
                        }
                    }
                }
                Node? childNode = this.client.ReadNode(child);
                if (childNode != null)
                {
                    NodeClass childNodeClass = childNode.NodeClass;
                    String browseName = childNode.BrowseName.Name;
                    JObject childObject = new JObject();
                    if (jObject.ContainsKey(browseName))
                    {
                        Console.Out.WriteLine($"Warning double browseName {browseName}");
                        continue;
                    }


                    switch (childNodeClass)
                    {
                        case NodeClass.Object:
                            if (placeHolderObject == null)
                            {
                                jObject.Add(browseName, childObject);
                                if (subscribe) this.addKnownBrowsePath(child, childObject, nodeId, machineNode);
                            }
                            else
                            {
                                childObject.Add("$TypeDefinition", this.getInstanceNsu(typeDefinition, false));
                                placeHolderObject.Add(browseName, childObject);
                                if (subscribe) this.addKnownBrowsePath(child, childObject, nodeId, machineNode);
                            }
                            break;
                        case NodeClass.Variable:
                            JToken dataValue = getDataValueAsObject(child);
                            bool isProperty = false;
                            bool shorten = false;

                            if (typeDefinition == VariableTypeIds.PropertyType)
                            {
                                isProperty = true;
                            }
                            else
                            {
                                if (shortenVariables)
                                {
                                    List<NodeId> nodeIds = this.client.BrowseLocalNodeIds(child, BrowseDirection.Forward, (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true);
                                    if (nodeIds.Count == 0)
                                    {
                                        shorten = true;
                                    }
                                }
                            }
                            if (isProperty || shorten)
                            {
                                if (dataValue is JValue)
                                {
                                    if (placeHolderObject == null)
                                    {
                                        jObject.Add(browseName, (JValue)dataValue);
                                        if (subscribe) this.addKnownBrowsePath(child, (JValue)dataValue, nodeId, machineNode);
                                    }
                                    else
                                    {
                                        childObject.Add("$TypeDefinition", this.getInstanceNsu(typeDefinition, false));
                                        placeHolderObject.Add(browseName, childObject);
                                        if (subscribe) this.addKnownBrowsePath(child, childObject, nodeId, machineNode);

                                    }
                                }
                                else if (dataValue is JObject)
                                {
                                    if (placeHolderObject == null)
                                    {
                                        jObject.Add(browseName, (JObject)dataValue);
                                        if (subscribe) this.addKnownBrowsePath(child, (JObject)dataValue, nodeId, machineNode);

                                    }
                                    else
                                    {
                                        JObject dv = (JObject)dataValue;
                                        dv.Add("$TypeDefinition", this.getInstanceNsu(typeDefinition, false));
                                        placeHolderObject.Add(browseName, dv);
                                        if (subscribe) this.addKnownBrowsePath(child, dv, nodeId, machineNode);

                                    }
                                }
                                else if (dataValue is JArray)
                                {
                                    if (placeHolderObject == null)
                                    {
                                        jObject.Add(browseName, (JArray)dataValue);
                                        if (subscribe) this.addKnownBrowsePath(child, (JArray)dataValue, nodeId, machineNode);
                                    }
                                    else
                                    {
                                        JArray array = (JArray)dataValue;
                                        placeHolderObject.Add(browseName, array);
                                        if (subscribe) this.addKnownBrowsePath(child, array, nodeId, machineNode);

                                    }
                                }
                            }
                            else
                            {
                                JObject valueObject = new JObject();
                                if (placeHolderObject == null)
                                {
                                    jObject.Add(browseName, valueObject);
                                }
                                else
                                {
                                    valueObject.Add("$TypeDefinition", this.getInstanceNsu(typeDefinition, false));
                                    placeHolderObject.Add(browseName, valueObject);
                                }
                                if (dataValue is JValue)
                                {
                                    valueObject.Add("value", (JValue)dataValue);
                                    if (subscribe) this.addKnownBrowsePath(child, (JValue)dataValue, nodeId, machineNode);
                                }
                                else if (dataValue is JObject)
                                {
                                    valueObject.Add("value", (JObject)dataValue);
                                    if (subscribe) this.addKnownBrowsePath(child, (JObject)dataValue, nodeId, machineNode);
                                }
                                else if (dataValue is JArray)
                                {
                                    valueObject.Add("value", (JArray)dataValue);
                                    if (subscribe) this.addKnownBrowsePath(child, (JArray)dataValue, nodeId, machineNode);
                                }
                                valueObject.Add("properties", childObject);

                            }
                            break;
                        default: Console.WriteLine($"Unexpected NodeClass Detected! {childNodeClass}"); break;
                    }
                    createJSON(childObject, child, machineNode, nodeId);
                }
            }
        }
        private List<NodeId> GetOptionalAndMandatoryPlaceHolders(NodeId nodeId, NodeId? parent)
        {
            List<NodeId> optionalMandatoryPlaceholdersOverParent = new List<NodeId>();
            if (parent != null)
            {
                Node? node = this.client.ReadNode(nodeId);
                if (node != null)
                {
                    QualifiedName browseName = node.BrowseName;
                    NodeId? parentTypeDefinition = this.client.BrowseTypeDefinition(parent);
                    if (parentTypeDefinition != null)
                    {
                        NodeId? typeNodeIdofNodeID = this.client.BrowseLocalNodeIdWithBrowseName(parentTypeDefinition, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true, browseName);
                        if (typeNodeIdofNodeID != null)
                        {
                            optionalMandatoryPlaceholdersOverParent = this.client.GetOptionalAndMandatoryPlaceholders(typeNodeIdofNodeID);
                        }
                    }
                }

            }
            // Check for OptionalPlaceholder and MandatoryPlaceholder in the parents TypeDefinition
            List<NodeId> optionalMandatoryPlaceholders = new List<NodeId>();
            NodeId? ptypeDefinition = this.client.BrowseTypeDefinition(nodeId);
            if (ptypeDefinition != null)
            {
                optionalMandatoryPlaceholders = this.client.GetOptionalAndMandatoryPlaceholders(ptypeDefinition);
            }
            optionalMandatoryPlaceholders.AddRange(optionalMandatoryPlaceholdersOverParent);
            return optionalMandatoryPlaceholders;
        }

        private void addKnownBrowsePath(NodeId childNodeId, JToken childObject, NodeId? ParentId, MachineNode? machineNode = null)
        {
            if (machineNode == null)
            {
                if (!this.knownBrowsePaths.ContainsKey(childNodeId))
                {
                    PublishedBrowsePaths publishedBrowsePaths = new PublishedBrowsePaths(childNodeId, ParentId);
                    publishedBrowsePaths.browsePaths.Add(childObject.Path, childObject);
                    this.knownBrowsePaths.Add(childNodeId, publishedBrowsePaths);
                }
                else
                {
                    PublishedBrowsePaths? publishedBrowsePaths;
                    if (this.knownBrowsePaths.TryGetValue(childNodeId, out publishedBrowsePaths))
                    {
                        if (publishedBrowsePaths != null)
                        {
                            if (!publishedBrowsePaths.browsePaths.ContainsKey(childObject.Path))
                            {
                                publishedBrowsePaths.browsePaths.Add(childObject.Path, childObject);
                            }
                        }
                    }
                }
            }
            else
            {
                if (!machineNode.KnownBrowsePaths.ContainsKey(childNodeId))
                {
                    PublishedBrowsePaths publishedBrowsePaths = new PublishedBrowsePaths(childNodeId, ParentId);
                    publishedBrowsePaths.browsePaths.Add(childObject.Path, childObject);
                    Console.WriteLine(childObject.Path);
                    machineNode.KnownBrowsePaths.Add(childNodeId, publishedBrowsePaths);
                }
                else
                {
                    PublishedBrowsePaths? publishedBrowsePaths;
                    if (machineNode.KnownBrowsePaths.TryGetValue(childNodeId, out publishedBrowsePaths))
                    {
                        if (publishedBrowsePaths != null)
                        {
                            if (!publishedBrowsePaths.browsePaths.ContainsKey(childObject.Path))
                            {
                                publishedBrowsePaths.browsePaths.Add(childObject.Path, childObject);
                            }
                        }
                    }
                }
            }
        }

        public void publishIdentificationObject()
        {
            try
            {
                this.WriteIdentification(this.IdentificationArray, this.InstanceNSU, this.TypeBrowseName);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }
        public void publishIdentificationMachineNodes()
        {
            try
            {
                JArray identificationArray = new JArray();
                foreach (MachineNode machineNode in this.machineNodes)
                {
                    if (machineNode != null && machineNode.ResolvedNodeId != null)
                    {
                        {
                            List<NodeId> identificationNodes = this.client.BrowseLocalNodeIds(machineNode.ResolvedNodeId, BrowseDirection.Forward, (int)NodeClass.Object, ReferenceTypeIds.HierarchicalReferences, true, ObjectTypeIds.FolderType);
                            foreach (NodeId child in identificationNodes)
                            {
                                Node? childNode = this.client.ReadNode(child);
                                if (childNode != null)
                                {
                                    if (childNode.BrowseName.Name == "Identification")
                                    {
                                        JObject data = new JObject();
                                        JObject ident = new JObject();
                                        createJSON(ident, child, machineNode, null, false);
                                        data.Add("Data", ident);
                                        data.Add("MachineId", machineNode.InstanceNamespace);
                                        data.Add("ParentId", "nsu=http:_2F_2Fopcfoundation.org_2FUA_2FMachinery_2F;i=1001");
                                        if (string.IsNullOrEmpty(machineNode.BaseType))
                                        {
                                            data.Add("Topic", this.mqttPrefix + "/" + this.clientId + "/" + machineNode.TypeBrowseName + "/" + machineNode.InstanceNamespace);
                                            data.Add("TypeDefinition", machineNode.TypeBrowseName);
                                        } else
                                        {
                                            data.Add("Topic", this.mqttPrefix + "/" + this.clientId + "/" + machineNode.BaseType + "/" + machineNode.InstanceNamespace);
                                            data.Add("TypeDefinition", machineNode.BaseType);
                                        }
                                        identificationArray.Add(data);
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(machineNode.BaseType))
                            {
                                this.WriteIdentification(identificationArray, machineNode.InstanceNamespace, machineNode.TypeBrowseName);
                            } else
                            {
                                this.WriteIdentification(identificationArray, machineNode.InstanceNamespace, machineNode.BaseType);
                            }
                            this.IdentificationArray = identificationArray;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// Reads the DataValue of a NodeId and returns it as object.
        /// </summary>
        /// <param name="nodeId"> The Node Id of the Node for that the DataValue is retrieved.</param>
        /// <returns>A JToken representing the DataValue.</returns>
        public JToken getDataValueAsObject(NodeId nodeId)
        {
            try
            {
                Node? node = this.client.ReadNode(nodeId);
                if (node == null)
                {
                    this.AddError(nodeId, $"Unable to retrieve Node for nodeId: {nodeId}");
                    return "";
                }
                if (node.NodeClass != NodeClass.Variable)
                {
                    return "";
                }
                DataValue? dv = this.client.ReadValue(nodeId);
                if (dv == null)
                {
                    return this.jsonConverter.GetDefaultNullValue();
                }
                Object value = dv.Value;
                switch (value)
                {
                    case null: return this.jsonConverter.GetDefaultNullValue();
                    case Boolean booleanValue: return this.jsonConverter.Convert(booleanValue);
                    case Byte byteValue: return this.jsonConverter.Convert(byteValue);
                    case byte[] byteStringvalue: return this.jsonConverter.Convert(byteStringvalue);
                    case DateTime dateTimeValue: return this.jsonConverter.Convert(dateTimeValue);
                    case DiagnosticInfo diagnosticInfoValue: return this.jsonConverter.Convert(diagnosticInfoValue);
                    case Double doubleValue: return this.jsonConverter.Convert(doubleValue);
                    case ExpandedNodeId expandedNodeId: return this.jsonConverter.Convert(expandedNodeId);
                    case float floatValue: return this.jsonConverter.Convert(floatValue);
                    case Guid guidValue: return this.jsonConverter.Convert(guidValue);
                    case Int16 int16Value: return this.jsonConverter.Convert(int16Value);
                    case Int32 int32Value: return this.jsonConverter.Convert(int32Value);
                    case Int64 int64Value: return this.jsonConverter.Convert(int64Value);
                    case LocalizedText localizedTextValue: return this.jsonConverter.Convert(localizedTextValue);
                    case NodeId nodeIdValue: return this.jsonConverter.Convert(nodeIdValue);
                    case QualifiedName qualifiedNameValue: return this.jsonConverter.Convert(qualifiedNameValue);
                    case SByte sByteValue: return this.jsonConverter.Convert(sByteValue);
                    case StatusCode statusCodeValue: return this.jsonConverter.Convert(statusCodeValue);
                    case String stringValue: return this.jsonConverter.Convert(stringValue);
                    case UInt16 uint16Value: return this.jsonConverter.Convert(uint16Value);
                    case UInt32 uint32Value: return this.jsonConverter.Convert(uint32Value);
                    case UInt64 uint64Value: return this.jsonConverter.Convert(uint64Value);
                    //TODO investigate in this...
                    case Variant variantValue: return variantValue.ToString();
                    case XmlElement xmlElementValue: return this.jsonConverter.Convert(xmlElementValue);
                    case ExtensionObject extensionObjectValue:
                        JObject jobject = new JObject();
                        ExtensionObject eto = (ExtensionObject)value;
                        ExtensionObjectEncoding encoding = eto.Encoding;
                        if (encoding == ExtensionObjectEncoding.Binary)
                        {
                            jobject = this.decode(eto);
                        }
                        else if (encoding == ExtensionObjectEncoding.Json)
                        {
                            this.AddError(nodeId, "JSON encoding is currently not implemented for ExtensionObjects.");
                        }
                        else if (encoding == ExtensionObjectEncoding.Xml)
                        {
                            this.AddError(nodeId, "XML encoding is currently not implemented for ExtensionObjects.");
                        }
                        else if (encoding == ExtensionObjectEncoding.EncodeableObject)
                        {
                            return this.decodeEncodeable(eto, nodeId);
                        }
                        return jobject;
                    case ExtensionObject[] extensionObjects:
                        JArray jArray = new JArray();
                        foreach (ExtensionObject extensionObject in extensionObjects)
                        {
                            ExtensionObjectEncoding encodinga = extensionObject.Encoding;
                            if (encodinga == ExtensionObjectEncoding.Binary)
                            {
                                jArray.Add(this.decode(extensionObject));
                            }
                            else if (encodinga == ExtensionObjectEncoding.Json)
                            {
                                this.AddError(nodeId, "JSON encoding is currently not implemented for ExtensionObjects.");
                            }
                            else if (encodinga == ExtensionObjectEncoding.Xml)
                            {
                                this.AddError(nodeId, "XML encoding is currently not implemented for ExtensionObjects.");
                            }
                            else if (encodinga == ExtensionObjectEncoding.EncodeableObject)
                            {
                                jArray.Add(this.decodeEncodeable(extensionObject, nodeId));
                            }
                        }
                        return jArray;
                    default: this.AddError(nodeId, $"Unimplemented Type for nodeId: {nodeId.ToString()}."); return "";
                }
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
        /// <summary>
        /// Decodes an IEncodeable DataType. This is only relevant for the build in DataTypes from
        /// the UA Base namespace.
        /// </summary>
        /// <param name="extensionObject">The extensionObject that is to be decoded.</param>
        /// <param name="nodeId">The nodeId for debug purposes.</param>
        /// <returns>An JObject representing the DataType.</returns>
        public JToken decodeEncodeable(ExtensionObject extensionObject, NodeId? nodeId)
        {
            JObject jObject = new JObject();
            if (extensionObject.Body is IEncodeable)
            {
                IEncodeable iEncodeable = (IEncodeable)extensionObject.Body;
                switch (iEncodeable)
                {
                    case Argument argument: return this.jsonConverter.Convert(argument);
                    case EUInformation euInformation: return this.jsonConverter.Convert(euInformation);
                    case Opc.Ua.Range range: return this.jsonConverter.Convert(range);
                    default: this.AddError(nodeId, $"The type of the iEncodeable is not implemented {iEncodeable.GetType()}"); return new JObject();
                }
            }
            else
            {
                this.AddError(nodeId, "The Encodeable Object is not of Type: IEncodeable");
                return new JObject();
            }
        }
        /// <summary>
        /// Adds an error to the Error List.
        /// </summary>
        /// <param name="nodeId">The nodeId on that the Error occured.</param>
        /// <param name="message">The message related to the error.</param>
        private void AddError(NodeId? nodeId, string message)
        {
            if (nodeId != null)
            {
                if (this.errors.ContainsKey(nodeId))
                {
                    this.errors.TryGetValue(nodeId, out var errorList);
                    if (errorList != null)
                    {
                        if (!errorList.Contains(message))
                        {
                            errorList.Add(message);
                        }
                    }
                    else
                    {
                        errorList = new List<string>();
                        errorList.Add(message);
                        this.errors.Add(nodeId, errorList);
                    }
                }
                else
                {
                    this.errors.Add(nodeId, new List<string> { message });
                }
            }
        }
        public object decode(Variant variant)
        {
            JObject jObject = new JObject();
            object obj = variant.Value;
            if (obj is String)
            {
                return (String)obj;
            }
            else if (obj is ExtensionObject)
            {
                return decode((ExtensionObject)obj);
            }
            return jObject;
        }
        public JObject decode(ExtensionObject eto)
        {
            ICustomEncoding? customEncoding = this.customEncodingManager.GetActiveEncodingForNodeId(eto.TypeId);
            if (customEncoding != null)
            {

                JObject? decoded = customEncoding.decode(eto);
                if (decoded != null) return decoded;
                else return new JObject();

            }
            JObject jObject = new JObject();
            this.Debug("Eto Expanded NodeId:" + eto.TypeId.ToString());
            NodeId etoId = ExpandedNodeId.ToNodeId(eto.TypeId, this.client.GetNamespaceTable());
            this.Debug("Eto NodeId:" + etoId.ToString());
            NodeId? dataType = this.client.BrowseLocalNodeId(etoId, BrowseDirection.Inverse, (uint)NodeClass.DataType, ReferenceTypeIds.HasEncoding, true);
            if (dataType != null)
            {
                this.Debug("DataType NodeId:" + dataType.ToString());
            }
            else
            {
                dataType = etoId;
                this.Debug("DataType NodeId:" + dataType.ToString() + "Took otherId as NodeId");
            }
            Dictionary<NodeId, Node> dataTypes = this.client.TypeDictionaries.GetDataTypes();
            NodeId search = dataType;
            bool success = dataTypes.TryGetValue(search, out Node? value);
            if (!success)
            {
                if (value != null)
                {
                    jObject.Add("Error", "Unable to get TypeInformation.");
                }
            }
            else
            {
                if (value != null)
                {
                    Dictionary<GeneratedDataTypeDefinition, GeneratedDataClass> gclasses = this.client.TypeDictionaries.generatedDataTypes;
                    DataTypeNode dtn = (DataTypeNode)value;
                    GeneratedDataTypeDefinition generatedDataTypeDefinition = new GeneratedDataTypeDefinition(this.client.GetNamespaceTable().GetString(dtn.NodeId.NamespaceIndex), dtn.BrowseName.Name);
                    gclasses.TryGetValue(generatedDataTypeDefinition, out GeneratedDataClass? gdc);
                    ExtensionObject dtd = dtn.DataTypeDefinition;
                    if (gdc != null)
                    {
                        BinaryDecoder BinaryDecoder = new BinaryDecoder((byte[])(eto.Body), ServiceMessageContext.GlobalContext);
                        jObject = this.decode(BinaryDecoder, gdc);
                    }
                }
            }
            return jObject;
        }
        public GeneratedDataClass? GetGeneratedDataClass(string namespaceurl, string browsename)
        {
            Dictionary<GeneratedDataTypeDefinition, GeneratedDataClass> gclasses = this.client.TypeDictionaries.generatedDataTypes;
            GeneratedDataTypeDefinition generatedDataTypeDefinition = new GeneratedDataTypeDefinition(namespaceurl, browsename);
            gclasses.TryGetValue(generatedDataTypeDefinition, out GeneratedDataClass? gdc);
            return gdc;
        }
        public JObject decode(BinaryDecoder BinaryDecoder, GeneratedDataClass generatedDataClass)
        {
            JObject jObject = new JObject();
            if (generatedDataClass != null)
            {
                if (generatedDataClass is GeneratedStructure)
                {
                    GeneratedStructure generatedStructure = (GeneratedStructure)generatedDataClass;
                    Int32 previousInt32 = 0;
                    UInt32 mask = 0;
                    Int32 currentSwitchBit = 0;
                    bool lastFieldWasSwitchedOff = false;
                    foreach (GeneratedField field in generatedStructure.fields)
                    {
                        this.Debug("Decode:" + field.Name + " " + field.TypeName);
                        if (field.IsLengthField == true)
                        {
                            if (field.Name == "ResultUri")
                            {
                                this.Debug("Here");
                            }
                            if (lastFieldWasSwitchedOff)
                            {
                                lastFieldWasSwitchedOff = false;
                                continue;

                            }
                            if (previousInt32 == -1)
                            {
                                previousInt32 = 0;
                            }
                            if (field.TypeName == "ua:Variant")
                            {
                                Variant[] v = new Variant[previousInt32];
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    if (field.TypeName == "ua:Variant")
                                    {
                                        v[i] = BinaryDecoder.ReadVariant(field.Name);
                                        array.Add(decode(v[i]));
                                    }
                                }
                                jObject.Add(field.Name, array);
                            }
                            else if (field.TypeName == "opc:CharArray")
                            {
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    String valueString = BinaryDecoder.ReadString(field.Name);
                                    this.Debug("Value: " + valueString.ToString());
                                    array.Add(valueString);
                                }
                                jObject.Add(field.Name, array);
                            }
                            else if (field.TypeName == "ua:XVType")
                            {
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    if (field.TypeName.StartsWith("ua:"))
                                    {
                                        if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.ua != null)
                                        {
                                            GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.ua, field.TypeName.Substring(3));
                                            if (gdc2 != null)
                                            {
                                                array.Add(decode(BinaryDecoder, gdc2));
                                            }
                                        }
                                    }
                                }
                                jObject.Add(field.Name, array);
                            }
                            else if (field.TypeName.StartsWith("ua:"))
                            {
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    if (field.TypeName.StartsWith("ua:"))
                                    {
                                        if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.ua != null)
                                        {
                                            GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.ua, field.TypeName.Substring(3));
                                            if (gdc2 != null)
                                            {
                                                array.Add(decode(BinaryDecoder, gdc2));
                                            }
                                        }
                                    }
                                }
                                jObject.Add(field.Name, array);
                            }
                            if (field.TypeName.StartsWith("tns:"))
                            {
                                JArray array = new JArray();
                                for (int i = 0; i < previousInt32; i++)
                                {
                                    if (field.TypeName.StartsWith("tns:"))
                                    {
                                        if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.tns != null)
                                        {
                                            GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.tns, field.TypeName.Substring(4));
                                            if (gdc2 != null)
                                            {
                                                array.Add(decode(BinaryDecoder, gdc2));
                                            }
                                        }
                                    }
                                }
                                jObject.Add(field.Name, array);
                            }
                            previousInt32 = 0;
                        }
                        else
                        {
                            if (field.IsSwitchField)
                            {
                                bool optionalFieldPresent = this.IsBitSet(mask, currentSwitchBit);
                                currentSwitchBit++;
                                if (!optionalFieldPresent)
                                {
                                    lastFieldWasSwitchedOff = true;
                                    continue;
                                }
                                else
                                {
                                    lastFieldWasSwitchedOff = false;
                                }
                            }
                            else
                            {
                                lastFieldWasSwitchedOff = false;
                            }

                            if (field.TypeName == "opc:Bit" && !field.HasLength)
                            {
                                continue;
                            }
                            else if (field.TypeName == "opc:Bit" && field.HasLength)
                            {
                                mask = BinaryDecoder.ReadUInt32("EncodingMask");
                            }
                            else if (field.TypeName == "opc:Boolean")
                            {
                                Boolean valueBoolean = BinaryDecoder.ReadBoolean(field.Name);
                                jObject.Add(field.Name, valueBoolean);
                                //this.Debug("Value: " + valueBoolean);
                            }
                            else if (field.TypeName == "opc:Byte")
                            {
                                Byte valueByte = BinaryDecoder.ReadByte(field.Name);
                                jObject.Add(field.Name, valueByte.ToString());
                                //this.Debug("Value: " + valueByte.ToString());
                            }
                            else if (field.TypeName == "opc:ByteString")
                            {
                                ByteCollection valueByteCollection = BinaryDecoder.ReadByteString(field.Name);
                                jObject.Add(field.Name, valueByteCollection.ToString());
                                //this.Debug("Value: " + valueByteCollection.ToString());
                            }
                            else if (field.TypeName == "opc:CharArray")
                            {
                                String valueString = BinaryDecoder.ReadString(field.Name);
                                jObject.Add(field.Name, valueString);
                                //Console.WriteLine("Value: " + valueString.ToString());
                            }
                            else if (field.TypeName == "opc:DateTime")
                            {
                                DateTime dateTimeValue = BinaryDecoder.ReadDateTime(field.Name);
                                jObject.Add(field.Name, dateTimeValue.ToString());
                                //this.Debug("Value: " + dateTimeValue.ToString());
                            }
                            else if (field.TypeName == "opc:Double")
                            {
                                Double doubleValue = BinaryDecoder.ReadDouble(field.Name);
                                jObject.Add(field.Name, doubleValue.ToString());
                                //this.Debug("Value: " + doubleValue.ToString());
                            }
                            else if (field.TypeName == "opc:Float")
                            {
                                float floatValue = BinaryDecoder.ReadFloat(field.Name);
                                jObject.Add(field.Name, floatValue.ToString());
                                //this.Debug("Value: " + floatValue.ToString());
                            }
                            else if (field.TypeName == "opc:Guid")
                            {
                                Uuid valueGuid = BinaryDecoder.ReadGuid(field.Name);
                                jObject.Add(field.Name, valueGuid.ToString());
                                //this.Debug("Value: " + valueGuid.ToString());
                            }
                            else if (field.TypeName == "opc:Int16")
                            {
                                short valueInt16 = BinaryDecoder.ReadInt16(field.Name);
                                jObject.Add(field.Name, valueInt16.ToString());
                                //this.Debug("Value: " + valueInt16.ToString());
                            }
                            else if (field.TypeName == "opc:Int32")
                            {
                                Int32 valueInt32 = BinaryDecoder.ReadInt32(field.Name);
                                if (!field.Name.StartsWith("NoOf"))
                                {
                                    jObject.Add(field.Name, valueInt32.ToString());
                                }
                                previousInt32 = valueInt32;
                                //this.Debug("Value: " + valueInt32.ToString());
                            }
                            else if (field.TypeName == "opc:Int64")
                            {
                                long valueInt64 = BinaryDecoder.ReadInt64(field.Name);
                                jObject.Add(field.Name, valueInt64.ToString());
                                //this.Debug("Value: " + valueInt64.ToString());
                            }
                            else if (field.TypeName == "opc:SByte")
                            {
                                sbyte valueSByte = BinaryDecoder.ReadSByte(field.Name);
                                jObject.Add(field.Name, valueSByte.ToString());
                                //this.Debug("Value: " + valueSByte.ToString());
                            }
                            else if (field.TypeName == "opc:String")
                            {
                                String valueString = BinaryDecoder.ReadString(field.Name);
                                jObject.Add(field.Name, valueString);
                                //this.Debug("Value: " + valueString.ToString());
                            }
                            else if (field.TypeName == "opc:UInt16")
                            {
                                ushort uint16Value = BinaryDecoder.ReadUInt16(field.Name);
                                jObject.Add(field.Name, uint16Value.ToString());
                                //this.Debug("Value: " + uint16Value.ToString());
                            }
                            else if (field.TypeName == "opc:UInt32")
                            {
                                uint uint32Value = BinaryDecoder.ReadUInt32(field.Name);
                                jObject.Add(field.Name, uint32Value.ToString());
                                //this.Debug("Value: " + uint32Value.ToString());
                            }
                            else if (field.TypeName == "opc:UInt64")
                            {
                                ulong uint64Value = BinaryDecoder.ReadUInt64(field.Name);
                                jObject.Add(field.Name, uint64Value.ToString());
                                //this.Debug("Value: " + uint64Value.ToString());
                            }
                            else if (field.TypeName == "ua:LocalizedText")
                            {
                                LocalizedText localizedTextValue = BinaryDecoder.ReadLocalizedText(field.Name);
                                jObject.Add(field.Name, localizedTextValue.ToString());
                                //this.Debug("Value: " + localizedTextValue.ToString());
                            }
                            if (field.TypeName == "ua:ExtensionObject")
                            {
                                if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.tns != null && field.Name == "ResultMetaData" && useGMSResultEncoding)
                                {
                                    GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.tns, "ResultMetaDataType");
                                    if (gdc2 != null)
                                    {
                                        jObject.Add(field.Name, decode(BinaryDecoder, gdc2));
                                    }
                                }
                                else
                                {
                                    ExtensionObject valueEto = BinaryDecoder.ReadExtensionObject(field.Name);
                                    jObject.Add(field.Name, decode(valueEto));
                                }
                            }
                            else if (field.TypeName == "ua:Variant")
                            {
                                Variant v = BinaryDecoder.ReadVariant(field.Name);
                                object value = decode(v);
                                if (value is String)
                                {
                                    jObject.Add(field.Name, (String)value);
                                }
                                else if (value is JObject)
                                {
                                    jObject.Add(field.Name, (JObject)value);
                                }
                            }
                            else if (field.TypeName.StartsWith("ua:") && field.TypeName != "ua:LocalizedText" && field.TypeName != "ua:Variant")
                            {
                                if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.ua != null)
                                {
                                    GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.ua, field.TypeName.Substring(3));
                                    if (gdc2 != null)
                                    {
                                        jObject.Add(field.Name, decode(BinaryDecoder, gdc2));
                                    }
                                }
                            }
                            else if (field.TypeName == "tns:ResultEvaluationEnum")
                            {
                                Int32 int32Value = BinaryDecoder.ReadInt32(field.Name);
                                jObject.Add(field.Name, int32Value);
                                //this.Debug("Value: " + int32Value.ToString());
                            }
                            else if (field.TypeName.StartsWith("tns:") && field.TypeName != "tns:ResultEvaluationEnum")
                            {
                                if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.tns != null)
                                {
                                    GeneratedDataClass? gdc2 = this.GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.tns, field.TypeName.Substring(4));
                                    if (gdc2 != null)
                                    {
                                        jObject.Add(field.Name, decode(BinaryDecoder, gdc2));
                                    }
                                }
                            }

                        }
                    }
                }
                else if (generatedDataClass is GeneratedEnumeratedType)
                {
                    this.Debug("GeneratedEnum");
                }
            }
            return jObject;
        }

        private string GetNameSpaceForIndex(ushort NamespaceIndex)
        {
            string ns = "";
            DataValue? dv = this.client.ReadValue(VariableIds.Server_NamespaceArray);
            if (dv != null)
            {
                String[] namespaces = (String[])dv.Value;
                ns = namespaces[NamespaceIndex];
            }
            return ns;
        }
        private string getInstanceNsu(NodeId? nodeId, bool replace = true)
        {
            string nsuString = "nsu=";
            string nameSpace = "";
            string identifier = "";
            NodeId? machineId = nodeId;
            if (machineId != null && nodeId != null)
            {
                ushort namespaceIndex = machineId.NamespaceIndex;
                nameSpace = this.GetNameSpaceForIndex(nodeId.NamespaceIndex);
                if (replace)
                {
                    nameSpace = nameSpace.Replace("/", "_2F");
                }
                if (machineId.IdType == IdType.Numeric)
                {
                    identifier = "i=" + (uint)machineId.Identifier;
                }
                else if (machineId.IdType == IdType.String)
                {
                    identifier = "s=" + ((string)machineId.Identifier).Replace(" ", "_20");
                }
                return nsuString + nameSpace + ";" + identifier;
            }
            else
            {
                return "";
            }
        }


        private void doPublish()
        {
            if (this.connected)
            {
                try
                {
                    if (!firstReadFinished)
                    {
                        if (!ReadInProgress)
                        {
                            ReadInProgress = true;
                            this.machineNodes.Clear();
                            foreach (MachineNode machineNode in this.publishedMachines)
                            {
                                int namespaceIndex = this.client.GetNamespaceTable().GetIndex(machineNode.NamespaceUrl);
                                if (machineNode.NodeIdType == "Numeric")
                                {
                                    machineNode.ResolvedNodeId = new NodeId(Convert.ToUInt32(machineNode.NodeIdString), (ushort)namespaceIndex);
                                    this.machineNodes.Add(machineNode);
                                }
                                else if (machineNode.NodeIdType == "String")
                                {
                                    machineNode.ResolvedNodeId = new NodeId(machineNode.NodeIdString, (ushort)namespaceIndex);
                                    this.machineNodes.Add(machineNode);
                                }
                            }

                            Console.WriteLine("Read InstanceNsu and BrowseName");
                            this.ReadInstanceNsuAndBrowseName();
                            Console.WriteLine("Publish BadList MachineNodes");
                            this.PublishBadListMachineNodes();
                            Console.WriteLine("Publish BadList MachineNodes.");
                            Console.WriteLine("Publish Client Online");
                            this.publishClientOnline();
                            Console.WriteLine("Publish Client Online finish.");
                            Console.WriteLine("Publish Maschine");
                            this.publishNodeMachineNodes();
                            Console.WriteLine("Publish Maschine finished.");
                            this.publishNodeAfterSubscriptionMachineNodes();
                            Console.WriteLine("Publish Online Machines Machine Node");
                            this.publishOnlineMachinesMachineNode();
                            Console.WriteLine("Publish Online Machines Machine Node finish.");
                            Console.WriteLine("Publish Identification Machine Node");
                            this.publishIdentificationMachineNodes();
                            Console.WriteLine("Publish Identification Machine Node finish.");
                            foreach (MachineNode machineNode in this.machineNodes)
                            {
                                foreach (KeyValuePair<NodeId, PublishedBrowsePaths> entry in machineNode.KnownBrowsePaths)
                                {
                                    Console.Write(entry.Value.ToString());
                                    this.client.SubscribeToDataChanges(entry.Key, this.updateDataValue);
                                }
                            }
                            ReadInProgress = false;
                            firstReadFinished = true;
                        }

                    }
                    else
                    {
                        //Detect OPC disconnect
                        _ = this.client.ReadNode(ObjectIds.Server);
                        Console.WriteLine("Publish BadList Machine Nodes");
                        this.PublishBadListMachineNodes();
                        Console.WriteLine("Publish Bad List Maschine Nodes finish.");
                        Console.WriteLine("Publish Client Online");
                        this.publishClientOnline();
                        Console.WriteLine("Publish Client Online finish.");
                        Console.WriteLine("Publish Online Machines MachineNode");
                        this.publishOnlineMachinesMachineNode();
                        Console.WriteLine("Publish Online Machines Machine Node finish.");
                        Console.WriteLine("Publish Identification Object");
                        this.publishIdentificationMachineNodes();
                        Console.WriteLine("Publish Identification Object");
                        Console.WriteLine("Publish Maschine Object Machine Nodes");
                        this.publishNodeAfterSubscriptionMachineNodes();
                        Console.WriteLine("Publish Maschine Object Machine Nodes finished.");
                    }

                }
                //Opc.Ua.ServiceResultException: BadNotConnected
                //BadNotConnected
                catch (Opc.Ua.ServiceResultException ex2)
                {
                    this.firstReadFinished = false;
                    this.TypeBrowseName = "";
                    this.InstanceNSU = "";
                    this.subscriptions.Clear();
                    this.client.subscription = null;
                    Console.WriteLine("Message:" + ex2.Message);
                    if (ex2.Message == "BadNotConnected")
                    {
                        Console.WriteLine("Reconnecting OPC");
                        _ = this.client.ConnectAsync(this.client.opcServerUrl).Result;
                    }
                }
                catch (MQTTnet.Exceptions.MqttClientNotConnectedException ex)
                {
                    Console.WriteLine(ex.ToString());
                    this.connected = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            else
            {
                try
                {
                    if (this.ConnectedOnce)
                    {
                        Console.WriteLine("Reconnecting Mqtt");
                        this.Reconnect();
                    }
                }
                catch (Exception ex1)
                {
                    Console.WriteLine(ex1.ToString());
                }
            }
        }
        private void OnTimedEvent(Object? source, System.Timers.ElapsedEventArgs e)
        {
            this.doPublish();
        }
        private void ReadInstanceNsuAndBrowseName()
        {
            try
            {
                foreach (NodeId machine in this.onlineMachines)
                {
                    if (machine != null)
                    {
                        Node? machineNode = this.client.ReadNode(machine);
                        if (machineNode != null)
                        {
                            NodeId? typedefinition = this.client.BrowseTypeDefinition(machine);
                            if (typedefinition != null)
                            {
                                Node? TypeDefinitionNode = this.client.ReadNode(typedefinition);
                                if (TypeDefinitionNode != null)
                                {
                                    this.InstanceNSU = this.getInstanceNsu(machine);
                                    this.TypeBrowseName = TypeDefinitionNode.BrowseName.Name;
                                }
                            }
                        }
                    }
                }
                foreach (MachineNode machineNode in this.publishedMachines)
                {
                    NodeId? machineNodeId = machineNode.ResolvedNodeId;
                    if (machineNodeId != null)
                    {
                        NodeId? typedefinitionNodeId = this.client.BrowseTypeDefinition(machineNodeId);
                        if (typedefinitionNodeId != null)
                        {
                            Node? typeDefinitionNode = this.client.ReadNode(typedefinitionNodeId);
                            if (typeDefinitionNode != null)
                            {
                                machineNode.InstanceNamespace = this.getInstanceNsu(machineNodeId);
                                machineNode.TypeBrowseName = typeDefinitionNode.BrowseName.Name;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }

        }

        private void updateDataValue(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs monitoredItemsArgs)
        {
            foreach (MachineNode machineNode in this.machineNodes)
            {
                Dictionary<NodeId, PublishedBrowsePaths> machineBrowsePaths = machineNode.KnownBrowsePaths;
                if (machineBrowsePaths.TryGetValue(monitoredItem.ResolvedNodeId, out PublishedBrowsePaths? path))
                {
                    if (path != null)
                    {
                        foreach (KeyValuePair<string, JToken> entry in path.browsePaths)
                        {
                            JToken? affectedObject = machineNode.Data.SelectToken(entry.Key);
                            if (affectedObject != null)
                            {
                                JToken replaceToken = this.createJSON(monitoredItem.ResolvedNodeId);
                                affectedObject.Replace(replaceToken);
                            }
                        }
                    }
                }
            }
            this.publishNodeAfterSubscriptionMachineNodes();
        }

        // Helper Methods

        private class PlaceholderNode
        {
            public NodeId placeholderNodeId;
            public NodeId typeDefinitionNodeId;
            public List<NodeId> subTypeNodeIds = new List<NodeId>();
            public JObject phList;
            public PlaceholderNode(NodeId placeholderNodeId, NodeId typeDefinitionNodeId, JObject phList, List<NodeId> subTypeNodeIds)
            {
                this.placeholderNodeId = placeholderNodeId;
                this.typeDefinitionNodeId = typeDefinitionNodeId;
                this.phList = phList;
                this.subTypeNodeIds = subTypeNodeIds;
            }

        }
        private void Debug(String message)
        {
            if (debug)
            {
                Console.WriteLine(message);
            }
        }
        bool IsBitSet(UInt32 value, int pos)
        {
            return ((value >> pos) & 1) != 0;
        }
        public JObject SortJsonKeysRecursively(JObject jsonObj)
        {
            // Erstelle ein neues JObject mit sortierten Keys
            JObject sortedJsonObj = new JObject(
                jsonObj.Properties()
                       .OrderBy(p => !p.Name.StartsWith("$")) // Prioritize keys starting with '$'
                       .ThenBy(p => p.Name) // Alphabetical sorting
                       .Select(p => new JProperty(p.Name, SortToken(p.Value)))
            );
            return sortedJsonObj;
        }

        // Methode, um die Sortierung je nach Token-Typ (JObject, JArray oder JValue) rekursiv anzuwenden
        public JToken SortToken(JToken token)
        {
            if (token is JObject)
            {
                // Sortiere rekursiv, wenn es sich um ein JObject handelt
                return SortJsonKeysRecursively((JObject)token);
            }
            else if (token is JArray)
            {
                // F�r Arrays: �berpr�fe, ob die einzelnen Elemente sortiert werden m�ssen
                var array = (JArray)token;
                return new JArray(array.Select(SortToken));
            }
            else
            {
                // Wenn es sich um einen Wert (JValue) handelt, bleibt der Wert gleich
                return token;
            }
        }
        private void processUpdates()
        {
            // Loop indefinitely, checking for updates
            foreach (NodeId affectedNode in this.changedNodes.GetConsumingEnumerable())
            {
                this.updateNode(affectedNode);

            }
        }
        public void updateNode(NodeId affectedNode)
        {
            foreach (MachineNode machineNode in this.machineNodes)
            {
                Dictionary<NodeId, PublishedBrowsePaths> machineBrowsePaths = machineNode.KnownBrowsePaths;
                if (machineBrowsePaths.TryGetValue(affectedNode, out PublishedBrowsePaths? path))
                {
                    if (path != null)
                    {
                        foreach (KeyValuePair<string, JToken> entry in path.browsePaths)
                        {
                            JToken? affectedObject = machineNode.Data.SelectToken(entry.Key);
                            if (affectedObject != null)
                            {
                                JObject jObject = new JObject();

                                this.createJSON(jObject, path.NodeId, machineNode, path.ParentId);
                                affectedObject.Replace(jObject);
                            }
                        }
                    }
                }
            }
            this.publishNodeAfterSubscriptionMachineNodes();
        }
        void OpcUaEventListener.ModelChangeEvent(NodeId affectedNode)
        {
            Console.WriteLine($"Update Affected Node {affectedNode}");
            this.updateNode(affectedNode);
        }
    }
    public class MqttSubscription
    {
        public NodeId NodeId { get; set; }
        public uint Subscriptionhandle { get; set; }
        public JObject parent;
        public String browseName;

        public MqttSubscription(NodeId nodeId, JObject parent, String browseName, uint SubscriptionHandle)
        {
            this.NodeId = nodeId;
            this.parent = parent;
            this.browseName = browseName;
            this.Subscriptionhandle = SubscriptionHandle;
        }
    }
    public class PublishedBrowsePaths
    {
        public NodeId NodeId { get; set; }
        public NodeId? ParentId { get; set; }
        public Dictionary<String, JToken> browsePaths = new Dictionary<String, JToken>();
        public PublishedBrowsePaths(NodeId nodeId, NodeId? ParentId)
        {
            this.NodeId = nodeId;
            this.ParentId = ParentId;
        }
        public override string ToString()
        {
            string returnString = $"NodeId: {this.NodeId}";
            foreach (KeyValuePair<String, JToken> entry in browsePaths)
            {
                returnString += " Path: " + entry.Key + "\n";
            }
            return returnString;
        }
    }
    public class MachineNode
    {
        public string NodeIdString { get; set; }
        public JObject Data { get; set; } = new JObject();
        public Dictionary<NodeId, IList<string>> Errors { get; set; } = new Dictionary<NodeId, IList<string>>();
        public JObject Identification { get; set; } = new JObject();
        public string InstanceNamespace { get; set; } = "";
        public string NamespaceUrl { get; set; }
        public string TypeBrowseName { get; set; } = "";
        public string NodeIdType { get; set; } = "";
        public string BaseType { get; set; } = "";

        public NodeId? ResolvedNodeId { get; set; }
        public Dictionary<NodeId, PublishedBrowsePaths> KnownBrowsePaths { get; set; } = new Dictionary<NodeId, PublishedBrowsePaths>();

        public MachineNode(string machineNodeId, String namespaceUrl)
        {
            this.NodeIdString = machineNodeId;
            this.NamespaceUrl = namespaceUrl;
        }
    }
}