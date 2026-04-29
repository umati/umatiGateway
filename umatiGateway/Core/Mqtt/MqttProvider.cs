// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using System.Text;
using MQTTnet;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using System.Reflection;
using Opc.Ua.Client;
using System.Timers;
using System.Xml;
using System.Collections.Concurrent;
using NLog;


using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.OPC;
using umatiGateway.Core.Mqtt.CustomEncoding;
using umatiGateway.Core.Util;

namespace umatiGateway.Core.Mqtt
{
    /// <summary>
    /// This class reads the data from the OPC UA Server and provides it via Mqtt.
    /// </summary>
    public class MqttProvider : OpcUaEventListener
    {
        private readonly object _lockObject = new object();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public List<MachineNode> MachineNodes { get; set; } = new List<MachineNode>();
        private MqttClientFactory mqttFactory = new MqttClientFactory();
        private IMqttClient? mqttClient = null;
        private const string CLIENT_ID = "TestClient";
        private const string TCP = "tcp";
        private const string WEBSOCKET = "websocket";
        public CustomEncodingManager customEncodingManager = new CustomEncodingManager();
        public bool useGMSResultEncoding = false;
        public List<PublishedNode> publishedNodes = new List<PublishedNode>();
        private UmatiGatewayApp app;
        private IOpcUaClient client;
        private Dictionary<NodeId, string> MqttValues = new Dictionary<NodeId, string>();
        private bool connected = false;
        private System.Timers.Timer aTimer = new System.Timers.Timer();
        private bool firstReadFinished = false;
        private bool ReadInProgress = false;
        public bool singleThreadPolling = false;
        public bool ConnectedOnce = false;
        public uint PollTimer = 60000;
        public bool TimerSetup = false;
        public Dictionary<NodeId, IList<string>> errors = new Dictionary<NodeId, IList<string>>();
        private Dictionary<NodeId, MqttSubscription> subscriptions = new Dictionary<NodeId, MqttSubscription>();
        private Dictionary<NodeId, PublishedBrowsePaths> knownBrowsePaths = new Dictionary<NodeId, PublishedBrowsePaths>();
        public volatile JObject machine = new JObject();
        private string InstanceNSU = "";
        private string TypeBrowseName = "";
        private JArray IdentificationArray = new JArray();
        private JSONConverter jsonConverter = new JSONConverter(false);
        private bool shortenVariables = true;
        private BlockingCollection<NodeId> changedNodes = new BlockingCollection<NodeId>();
        private Dictionary<NodeId, string> placeholderVariablesWithTypeDefinition = new Dictionary<NodeId, string>();
        private NodeId? resultFolder = null;
        private List<string> filteredPlaceholderTags = new List<string>();
        private string connectionType = WEBSOCKET;

        public MqttProvider(UmatiGatewayApp app)
        {
            this.app = app;
            this.client = app.OpcUaClient;
            mqttClient = mqttFactory.CreateMqttClient();
            this.PollTimer = app.ActiveConfiguration.MqttProviderConfig.PublishInterval;
        }

        // Connection/Disconnection
        public void Disconnect()
        {
            Logger.Info("Disconnecting");
            AsyncHelper.RunSync(() => mqttClient.DisconnectAsync());
            connected = false;
            Logger.Info("Disconnected");
        }
        private void ResolveConfiguration()
        {
            List<PublishedNode> publishedNodes = this.app.ActiveConfiguration.MqttProviderConfig.PublishedNodes;
            PublishedNodeFilter publishedNodeFilter = new PublishedNodeFilter(this.app);
            this.MachineNodes.AddRange(publishedNodeFilter.FilterMachineNodes(publishedNodes));
            Console.Out.WriteLine();
        }

        public void Connect()
        {
            foreach (Configuration.CustomEncoding customEncoding in this.app.ActiveConfiguration.MqttProviderConfig.CustomEncodings)
            {
                if (customEncoding.Name == "GMSResultDataTypeEncoding" && customEncoding.Active == true)
                {
                    if (customEncoding.Active == true)
                    {
                        useGMSResultEncoding = true;
                    }
                    else
                    {
                        useGMSResultEncoding = false;
                    }
                }
            }
            MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
            this.jsonConverter = new JSONConverter(config.UpperCaseRange);
            Logger.Info("MQTT Connect");
            if (!TimerSetup)
            {
                aTimer.Interval = PollTimer;
                aTimer.Elapsed += OnTimedEvent;
                aTimer.AutoReset = true;
                aTimer.Enabled = true;
                TimerSetup = true;
                this.ResolveConfiguration();
            }

            ConnectedOnce = true;
            connectionType = WEBSOCKET;
            Connect(config.ServerEndpoint, connectionType, "4321", config.UserName, config.Password);
            doPublish();
            client.ConnectEvents(this);
            aTimer.Start();

        }
        public void Reconnect()
        {
            MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
            if (!connected)
            {
                Connect(config.ServerEndpoint, connectionType, "4321", config.UserName, config.Password);
            }
        }
        public void Connect(string connectionString, string connectionType, string port, string user, string pwd)
        {
            MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
            try
            {
                this.connectionType = connectionType;
                Logger.Info("=== MQTT Connection Configuration ===");
                Logger.Info("Connection String : {ServerEndpoint}", config.ServerEndpoint);
                Logger.Info("Connection Type   : {ConnectionType}", this.connectionType);
                Logger.Info("Username          : {UserName}",(string.IsNullOrEmpty(config.UserName) ? "<empty>" : config.UserName));
                Logger.Info("Password Length   : {PasswordLength}", (string.IsNullOrEmpty(config.Password) ? 0 : config.Password.Length));

                if (this.connectionType == TCP)
                {
                    Connect_Client_Using_Tcp();
                }
                else if (this.connectionType == WEBSOCKET)
                {
                    if (config.ServerEndpoint.StartsWith("mqtt"))
                    {
                        Connect_Client_Using_Tcp();
                    }
                    else
                    {
                        Connect_Client_Using_WebSockets();
                    }
                }
                else
                {
                    Console.Out.WriteLine("Unknown MQTT connection type");
                }
                connected = true;
            }
            catch (Exception e)
            {
                Logger.Error("Exception on connecting to MQTT broker. {Exception}", e);
                connected = false;
            }
        }

        private void Connect_Client_Using_WebSockets()
        {
            Logger.Trace("MQTT Connect with Websockets");
            MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
            try
            {
                if (mqttClient == null)
                {
                    Logger.Warn("m_mqttClient is null.");
                    return;
                }

                var tlsParameters = new MqttClientTlsOptions
                {
                    UseTls = true,
                    CertificateValidationHandler = ValidateServerCertificate
                };

                var mqttClientOptionsBuilder = new MqttClientOptionsBuilder()

                    .WithWebSocketServer(options => options.WithUri(config.ServerEndpoint))
                    .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);

                if (!string.IsNullOrEmpty(config.UserName) && config.Password != null)
                {
                    mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithCredentials(config.UserName, config.Password);
                }

                mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithTlsOptions(tlsParameters);

                var mqttClientOptions = mqttClientOptionsBuilder.Build();

                AsyncHelper.RunSync(() => mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None));
            }
            catch (Exception ex)
            {
                Logger.Info(ex.ToString());
            }
        }

        private bool ValidateServerCertificate(MqttClientCertificateValidationEventArgs context)
        {
            Logger.Trace("ValidateServerCertificate");

            try
            {
                var serverCertificate = new X509Certificate2(context.Certificate.GetRawCertData());

                // Zertifikatsinformationen loggen
                Logger.Info("=== Server Certificate Details ===");
                Logger.Info("Thumbprint       : {Thumbprint}", serverCertificate.Thumbprint);
                Logger.Info("Subject          : {Subject}", serverCertificate.Subject);
                Logger.Info("Subject Name     : {SubjectName}", serverCertificate.SubjectName.Name);
                Logger.Info("Issuer           : {Issuer}", serverCertificate.Issuer);
                Logger.Info("Issuer Name      : {IssuerName}", serverCertificate.IssuerName.Name);
                Logger.Info("Valid From       : {ValidFrom}", serverCertificate.NotBefore);
                Logger.Info("Valid Until      : {ValidUntil}", serverCertificate.NotAfter);
                Logger.Info("Key Algorithm    : {Key Algorithm}", serverCertificate.GetKeyAlgorithm());
                string servercertificatePath = this.app.ActiveConfiguration.MqttProviderConfig.ServerCertificatePath;
                string customCertificatePath = this.app.ActiveConfiguration.MqttProviderConfig.CustomCaCertificatePath;

                // Zertifikat speichern, falls nicht vorhanden
                if (servercertificatePath != null && !File.Exists(servercertificatePath))
                {
                    Logger.Info("Saving server certificate to disk.");
                    File.WriteAllBytes(this.app.ActiveConfiguration.MqttProviderConfig.ServerCertificatePath, serverCertificate.Export(X509ContentType.Cert));
                }

                // Schritt 1: Standardmäßige Zertifikatsvalidierung
                using (var chain = new X509Chain())
                {
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                    bool isValid = chain.Build(serverCertificate);

                    if (isValid)
                    {
                        Logger.Info("Standard system certificate validation succeeded.");
                        return true;
                    }
                    else
                    {
                        Logger.Warn("Standard certificate validation failed. Attempting fallback to custom CA.");
                        foreach (var status in chain.ChainStatus)
                        {
                            Logger.Info(" - Status: {Status}, Info: {StatusInformation}", status.Status, status.StatusInformation?.Trim());
                        }
                    }
                }

                // Schritt 2: Validierung mit benutzerdefinierter CA (falls vorhanden)
                if (customCertificatePath != null && File.Exists(customCertificatePath))
                {
                    var customCaCertificate = new X509Certificate2(File.ReadAllBytes(this.app.ActiveConfiguration.MqttProviderConfig.CustomCaCertificatePath));
                    using var customChain = new X509Chain();
                    customChain.ChainPolicy.ExtraStore.Add(customCaCertificate);
                    customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    customChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                    customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    customChain.ChainPolicy.CustomTrustStore.Add(customCaCertificate);

                    bool isCustomValid = customChain.Build(serverCertificate);

                    if (isCustomValid)
                    {
                        Logger.Info("Validation succeeded with custom CA certificate.");
                        return true;
                    }
                    else
                    {
                        Logger.Warn("Custom CA validation failed.");
                        foreach (var status in customChain.ChainStatus)
                        {
                            Logger.Info(" - Status: {Status}, Info: {StatusInformation}", status.Status, status.StatusInformation?.Trim());
                        }
                    }
                }
                else
                {
                    Logger.Warn("Custom CA certificate not found.");
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Certificate validation error: {ExceptionMessage}", ex.Message);
                return false;
            }
        }

        private void Connect_Client_Using_Tcp()
        {
            Logger.Trace("MQTT Connect with TCP");
            MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
            if (mqttClient != null)
            {
                MqttClientOptions mqttClientOptions;
                if (config.ServerEndpoint != null)
                {

                    int Index = config.ServerEndpoint.LastIndexOf(":");
                    string server = config.ServerEndpoint.Substring(7, Index - 7);
                    int port1 = int.Parse(config.ServerEndpoint.Substring(Index + 1));
                    if (config.UserName != null && config.UserName != "" && config.Password != null)
                    {

                        mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithTcpServer(server, port1)
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                        .WithCredentials(config.UserName, config.Password)
                        .Build();
                    }
                    else
                    {
                        mqttClientOptions = new MqttClientOptionsBuilder()
                        .WithTcpServer(server, port1)
                        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                        .Build();
                    }
                    AsyncHelper.RunSync(() => mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None));
                }
            }
            else
            {
                Console.Out.WriteLine("The MqttClient is null");
            }
        }

        //Publishing

        public bool WriteMessage(JObject jObject, string machineId, string type)
        {
            lock (_lockObject)
            {
                MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
                try
                {
                    JObject sortedJsonObj = SortJsonKeysRecursively(jObject);
                    string MyTopic = config.Prefix + "/" + config.ClientId + "/" + type + "/" + machineId;
                    MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(MyTopic)
                    .WithPayload(sortedJsonObj.ToString(Newtonsoft.Json.Formatting.Indented))
                    .Build();
                    if (mqttClient != null)
                    {
                        _ = mqttClient.PublishAsync(applicationMessage, CancellationToken.None).GetAwaiter().GetResult();
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Info(e.ToString());
                    connected = false;
                    throw;
                }
            }
        }
        public bool WriteIdentification(JArray jArray, string machineId, string type)
        {
            lock (_lockObject)
            {
                MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
                try
                {
                    string MyTopic = config.Prefix + "/" + config.ClientId + "/" + "list/" + type;
                    MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(MyTopic)
                    .WithPayload(jArray.ToString(Newtonsoft.Json.Formatting.Indented))
                    .Build();
                    if (mqttClient != null)
                    {
                        _ = mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;

                    }
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Error("Unable to publish Identification", e);
                    throw;
                }
            }
        }
        public void publishOnlineMachinesMachineNode()
        {
            lock (_lockObject)
            {
                MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
                try
                {
                    foreach (MachineNode machineNode in MachineNodes)
                    {
                        string MyTopic = config.Prefix + "/" + config.ClientId + "/" + "online/";
                        MyTopic += machineNode.InstanceNamespace;
                        MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(MyTopic)
                        .WithPayload("1")
                        .Build();
                        if (mqttClient != null)
                        {
                            _ = mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                        }
                        else
                        {
                            //ToDo Add Error Handling
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Info(e.ToString());
                    throw;
                }
            }
        }
        public bool publishBadList()
        {
            lock (_lockObject)
            {
                MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
                try
                {
                    string MyTopic = config.Prefix + "/" + config.ClientId + "/" + "bad_list/errors";
                    JArray errorArray = new JArray();
                    foreach (KeyValuePair<NodeId, IList<string>> entry in errors)
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
                    if (mqttClient != null)
                    {
                        _ = mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Info("Unable to publish BadList", e.ToString());
                    throw;
                }
            }
        }
        public void PublishBadListMachineNodes()
        {
            lock (_lockObject)
            {
                MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
                try
                {
                    string MyTopic = config.Prefix + "/" + config.ClientId + "/" + "bad_list/errors";
                    JArray machinesErrorArray = new JArray();
                    foreach (MachineNode machineNode in MachineNodes)
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
                    if (mqttClient != null)
                    {
                        _ = mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                    }
                    else
                    {
                        //TODO handling for this case
                    }
                }
                catch (Exception e)
                {
                    Logger.Info("Unable to publish BadList", e.ToString());
                    throw;
                }
            }
        }
        public bool publishClientOnline()
        {
            lock (_lockObject)
            {
                MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
                try
                {
                    string MyTopic = config.Prefix + "/" + config.ClientId + "/" + "clientOnline";
                    MqttApplicationMessage applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(MyTopic)
                    .WithPayload("1")
                    .Build();
                    if (mqttClient != null)
                    {
                        _ = mqttClient.PublishAsync(applicationMessage, CancellationToken.None).Result;
                    }
                    string MyTopic1 = config.Prefix + "/" + config.ClientId + "/" + "gw-version";
                    MqttApplicationMessage applicationMessage1 = new MqttApplicationMessageBuilder()
                    .WithTopic(MyTopic1)
                    .WithPayload($"umatiGateway-{getUmatiGatewayVersion()}")
                    .Build();
                    if (mqttClient != null)
                    {
                        _ = mqttClient.PublishAsync(applicationMessage1, CancellationToken.None).Result;
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Info("Unable to publish ClientOnline", e.ToString());
                    throw;
                }
            }
        }

        private static string getUmatiGatewayVersion()
        {
            var version = Assembly.GetExecutingAssembly()
.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
            ;
            return version;
        }


        public void publishNodeMachineNodes()
        {
            try
            {
                foreach (MachineNode machineNode in MachineNodes)
                {
                    NodeId? machineNodeId = machineNode.ResolvedNodeId;
                    if (machineNodeId != null)
                    {
                        JObject body = new JObject();
                        createJSON(body, machineNodeId, machineNode);
                        Node? machine = client.ReadNode(machineNodeId);
                        if (machine != null)
                        {
                            NodeId? typedefinition = client.BrowseTypeDefinition(machineNodeId);
                            if (typedefinition != null)
                            {
                                Node? TypeDefinitionNode = client.ReadNode(typedefinition);
                                if (TypeDefinitionNode != null)
                                {
                                    if (string.IsNullOrEmpty(machineNode.BaseType))
                                    {
                                        WriteMessage(body, getInstanceNsu(machineNodeId), TypeDefinitionNode.BrowseName.Name);
                                    }
                                    else
                                    {
                                        WriteMessage(body, getInstanceNsu(machineNodeId), machineNode.BaseType);
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
                Logger.Info(e.ToString());
                throw;
            }
        }

        public void publishNodeAfterSubscriptionMachineNodes()
        {
            try
            {
                foreach (MachineNode machineNode in MachineNodes)
                {
                    if (string.IsNullOrEmpty(machineNode.BaseType))
                    {
                        WriteMessage(machineNode.Data, machineNode.InstanceNamespace, machineNode.TypeBrowseName);
                    }
                    else
                    {
                        WriteMessage(machineNode.Data, machineNode.InstanceNamespace, machineNode.BaseType);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Info(e.ToString());
                throw;
            }
        }

        private JToken createJSON(NodeId nodeId)
        {
            JObject jObject = new JObject();
            if (nodeId != null)
            {
                Node? childNode = client.ReadNode(nodeId);
                if (childNode != null)
                {
                    if (childNode.NodeClass == NodeClass.Variable)
                    {
                        string? placeholderType = null;
                        placeholderVariablesWithTypeDefinition.TryGetValue(nodeId, out placeholderType);
                        object dataValue = getDataValueAsObject(nodeId);
                        if (dataValue is JValue)
                        {
                            JValue dv = (JValue)dataValue;
                            return dv;
                        }
                        else if (dataValue is JObject)
                        {
                            JObject dv = (JObject)dataValue;
                            //dv.Add("$TypeDefinition", placeholderType);
                            return dv;
                        }
                        else if (dataValue is JArray)
                        {
                            JArray dv = (JArray)dataValue;
                            return dv;
                        }
                    }
                }
            }
            return jObject;
        }
        private void printPlaceholderNodes(List<NodeId> optionalMandatoryPlaceholders, NodeId nodeId, NodeId? parent, List<PlaceholderNode> possiblePlaceholdernodes)
        {
            Logger.Trace("--------------------------------------------------------------------------");
            Node? theNode = client.ReadNode(nodeId);
            if (parent == null)
            {

                if (theNode != null)
                {
                    Logger.Trace("The Placeholders Types in {NodeId} = {BrowseName} are:", nodeId, theNode.BrowseName);
                }
                else
                {
                    Logger.Trace("The Placeholders Types in {NodeId} are:", nodeId);
                }
            }
            else
            {
                Node? parentNode = client.ReadNode(parent);
                if (theNode != null && parentNode != null)
                {
                    Logger.Trace("The Placeholders Types in {NodeId} = {BrowseName} and {Parent} = {ParentBrowseName} are:", nodeId, theNode.BrowseName, parent, parentNode.BrowseName);
                }
                else
                {
                    Logger.Trace("The Placeholders Types in {NodeId} and {Parent} are:", nodeId, parent);
                }
            }
            foreach (NodeId placeholder in optionalMandatoryPlaceholders)
            {
                Node? placeholderNode = client.ReadNode(placeholder);
                if (placeholderNode != null)
                {
                    Logger.Trace("Placeholder Type: {Placeholder} = {PlaceholderBrowseName}", placeholder, placeholderNode.BrowseName);
                }
                else
                {
                    Logger.Trace("Placeholder Type: {Placeholder} ", placeholder);
                }
            }
            Logger.Trace("Valid Placeholder Children are:");
            foreach (PlaceholderNode possiblePlaceHolderNode in possiblePlaceholdernodes)
            {
                possiblePlaceHolderNode.printPlaceholderNode(this.app);
            }
            Logger.Trace("--------------------------------------------------------------------------");
        }
        private void createJSON(JObject jObject, NodeId nodeId, MachineNode? machineNode = null, NodeId? parent = null, bool subscribe = true)
        {
            // Check if for the Parent a PlaceholderRule applies

            List<NodeId> optionalMandatoryPlaceholders = GetOptionalAndMandatoryPlaceHolders(nodeId, parent);
            //Find possible Types for Placeholder/Children
            List<PlaceholderNode> placeholderNodes = new List<PlaceholderNode>();
            foreach (NodeId placeholder in optionalMandatoryPlaceholders)
            {
                Node? placeholderNode = client.ReadNode(placeholder);
                if (placeholderNode != null)
                {
                    NodeId? placeHolderTypeDefinition = client.BrowseTypeDefinition(placeholder);
                    string phBrowseName = placeholderNode.BrowseName.Name;
                    List<string> ignoredPlaceholderTagNames = app.ActiveConfiguration.MqttProviderConfig.IgnoredPlaceholderTags.Select(t => t.Name).ToList();
                    if (!jObject.ContainsKey(phBrowseName) && !ignoredPlaceholderTagNames.Contains(phBrowseName))
                    {
                        JObject placeholderType = new JObject();
                        jObject.Add(phBrowseName, placeholderType);
                        List<NodeId> subTypes = new List<NodeId>();
                        if (placeHolderTypeDefinition != null)
                        {
                            client.BrowseAllHierarchicalSubType(placeHolderTypeDefinition, subTypes);
                            placeholderNodes.Add(new PlaceholderNode(placeholder, placeHolderTypeDefinition, placeholderType, subTypes));
                        }
                    }
                }

            }
            //this.printPlaceholderNodes(optionalMandatoryPlaceholders, nodeId, parent, placeholderNodes);
            List<NodeId> hierarchicalChilds = new List<NodeId>();
            if (app.ActiveConfiguration.MqttProviderConfig.IncludeStructuredComponents)
            {
                hierarchicalChilds = client.BrowseNodeIds(new BrowseDescriptionCollection { BrowseUtils.GetHierarchicalChildren(nodeId, (int)NodeClass.Object | (int)NodeClass.Variable) });
                //hierarchicalChilds = client.BrowseLocalNodeIds(nodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true);
            }
            else
            {
                hierarchicalChilds = client.BrowseNodeIds(
                    new BrowseDescriptionCollection { BrowseUtils.GetHierarchicalChildren(nodeId, (int)NodeClass.Object | (int)NodeClass.Variable) },
                    new BrowseDescriptionCollection { BrowseUtils.ForwardBrowseDescription(nodeId, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HasStructuredComponent, true) });
                //hierarchicalChilds = client.BrowseLocalNodeIdsExcludeReference(nodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true, ReferenceTypeIds.HasStructuredComponent);
            }
            foreach (NodeId child in hierarchicalChilds)
            {
                JObject? placeHolderObject = null;
                NodeId? typeDefinition = client.BrowseTypeDefinition(child);
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
                Node? childNode = client.ReadNode(child);
                if (childNode != null)
                {
                    if (placeHolderObject != null)
                    {
                        if (typeDefinition != null)
                        {
                            Logger.Trace("TypeDefinition: {TypeDefinition}", typeDefinition);
                        }
                        else
                        {
                            Logger.Trace("There is no TypeDefintion.");
                        }
                    }
                    else
                    {
                        Logger.Trace("{Child} = {ChildBrowsename} is NOT a PlaceHolder", child, childNode.BrowseName);
                        if (typeDefinition != null)
                        {
                            Logger.Trace("TypeDefinition: {TypeDefinition}", typeDefinition);
                        }
                        else
                        {
                            Logger.Trace("There is no TypeDefintion.");
                        }
                    }
                    NodeClass childNodeClass = childNode.NodeClass;
                    string browseName = childNode.BrowseName.Name;
                    //Look for resultsfolder
                    if (browseName == "Results" && typeDefinition == ObjectTypeIds.FolderType)
                    {
                        resultFolder = childNode.NodeId;
                        Logger.Trace("Found resultfolder: {ResultFolder}", resultFolder);
                    }
                    JObject childObject = new JObject();
                    if (jObject.ContainsKey(browseName))
                    {
                        //Console.Out.WriteLine($"Warning double browseName {browseName}");
                        continue;
                    }


                    switch (childNodeClass)
                    {
                        case NodeClass.Object:
                            if (placeHolderObject == null)
                            {
                                jObject.Add(browseName, childObject);
                                if (subscribe) addKnownBrowsePath(child, childObject, nodeId, machineNode);
                            }
                            else
                            {
                                childObject.Add("$TypeDefinition", getInstanceNsu(typeDefinition, false));
                                placeHolderObject.Add(browseName, childObject);
                                if (subscribe) addKnownBrowsePath(child, childObject, nodeId, machineNode);
                            }
                            break;
                        case NodeClass.Variable:
                            //ToDo Fix how the placeholder is received from DataType
                            if (!placeholderVariablesWithTypeDefinition.ContainsKey(child))
                            {
                                placeholderVariablesWithTypeDefinition.Add(child, getInstanceNsu(typeDefinition, false));
                            }
                            JToken dataValue = getDataValueAsObject(child);
                            bool shorten = false;
                            bool useValueIndentation = true;
                            if (shortenVariables)
                            {
                                List<NodeId> nodeIds = new List<NodeId>();
                                if (app.ActiveConfiguration.MqttProviderConfig.IncludeStructuredComponents)
                                {
                                    nodeIds = client.BrowseNodeIds(new BrowseDescriptionCollection { BrowseUtils.GetHierarchicalChildren(child, (int)NodeClass.Variable) });
                                    //nodeIds = client.BrowseLocalNodeIds(child, BrowseDirection.Forward, (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true);
                                }
                                else
                                {
                                    nodeIds = client.BrowseNodeIds(
                                        new BrowseDescriptionCollection { BrowseUtils.GetHierarchicalChildren(child, (int)NodeClass.Variable) },
                                        new BrowseDescriptionCollection { BrowseUtils.ForwardBrowseDescription(child, (int)NodeClass.Variable, ReferenceTypeIds.HasStructuredComponent, true) });
                                    //nodeIds = client.BrowseLocalNodeIdsExcludeReference(child, BrowseDirection.Forward, (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true, ReferenceTypeIds.HasStructuredComponent);
                                }
                                if (nodeIds.Count == 0)
                                {
                                    shorten = true;
                                }
                                if (getInstanceNsu(typeDefinition, false) == "nsu=http://opcfoundation.org/UA/GMS/;i=2004")
                                {
                                    Logger.Trace("Not use ValueIndentation");
                                    useValueIndentation = false;
                                }
                            }
                            if (shorten || !useValueIndentation)
                            {
                                if (dataValue is JValue)
                                {
                                    if (placeHolderObject == null)
                                    {
                                        jObject.Add(browseName, (JValue)dataValue);
                                        if (subscribe) addKnownBrowsePath(child, (JValue)dataValue, nodeId, machineNode);
                                    }
                                    else
                                    {
                                        childObject.Add("$TypeDefinition", getInstanceNsu(typeDefinition, false));
                                        placeHolderObject.Add(browseName, childObject);
                                        if (subscribe) addKnownBrowsePath(child, childObject, nodeId, machineNode);

                                    }
                                }
                                else if (dataValue is JObject)
                                {
                                    if (placeHolderObject == null)
                                    {
                                        jObject.Add(browseName, (JObject)dataValue);
                                        if (subscribe) addKnownBrowsePath(child, (JObject)dataValue, nodeId, machineNode);

                                    }
                                    else
                                    {
                                        JObject dv = (JObject)dataValue;
                                        dv.Add("$TypeDefinition", getInstanceNsu(typeDefinition, false));
                                        placeHolderObject.Add(browseName, dv);
                                        if (subscribe) addKnownBrowsePath(child, dv, nodeId, machineNode);

                                    }
                                }
                                else if (dataValue is JArray)
                                {
                                    if (placeHolderObject == null)
                                    {
                                        jObject.Add(browseName, (JArray)dataValue);
                                        if (subscribe) addKnownBrowsePath(child, (JArray)dataValue, nodeId, machineNode);
                                    }
                                    else
                                    {
                                        JArray array = (JArray)dataValue;
                                        placeHolderObject.Add(browseName, array);
                                        if (subscribe) addKnownBrowsePath(child, array, nodeId, machineNode);

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
                                    valueObject.Add("$TypeDefinition", getInstanceNsu(typeDefinition, false));
                                    placeHolderObject.Add(browseName, valueObject);
                                }
                                if (dataValue is JValue)
                                {
                                    valueObject.Add("value", (JValue)dataValue);
                                    if (subscribe) addKnownBrowsePath(child, (JValue)dataValue, nodeId, machineNode);
                                }
                                else if (dataValue is JObject)
                                {
                                    valueObject.Add("value", (JObject)dataValue);
                                    if (subscribe) addKnownBrowsePath(child, (JObject)dataValue, nodeId, machineNode);
                                }
                                else if (dataValue is JArray)
                                {
                                    valueObject.Add("value", (JArray)dataValue);
                                    if (subscribe) addKnownBrowsePath(child, (JArray)dataValue, nodeId, machineNode);
                                }
                                valueObject.Add("properties", childObject);

                            }
                            break;
                        default: Logger.Info("Unexpected NodeClass detected! {ChildNodeclass}", childNodeClass); break;
                    }
                    createJSON(childObject, child, machineNode, nodeId);
                }
            }
        }
        private void GetTypeParentNodeIds(NodeId nodeId, List<NodeId> typeParents)
        {
            NodeId? typeParent = client.BrowseLocalNodeId(nodeId, BrowseDirection.Inverse, (int)NodeClass.ObjectType | (int)NodeClass.VariableType, ReferenceTypeIds.HasSubtype, true);
            if (typeParent != null && typeParent != ObjectTypeIds.BaseObjectType && typeParent != VariableTypeIds.BaseDataVariableType)
            {
                typeParents.Add(typeParent);
                GetTypeParentNodeIds(typeParent, typeParents);
            }
        }

        private List<NodeId> GetOptionalAndMandatoryPlaceHolders(NodeId nodeId, NodeId? parent)
        {
            List<NodeId> optionalMandatoryPlaceholdersOverParent = new List<NodeId>();
            List<NodeId> OptionalPlaceholdersByTypeClasses = new List<NodeId>();
            if (parent != null)
            {
                Node? node = client.ReadNode(nodeId);
                if (node != null)
                {
                    QualifiedName browseName = node.BrowseName;
                    NodeId? parentTypeDefinition = client.BrowseTypeDefinition(parent);
                    if (parentTypeDefinition != null)
                    {
                        NodeId? typeNodeIdofNodeID = client.BrowseLocalNodeIdWithBrowseName(parentTypeDefinition, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true, browseName);
                        if (typeNodeIdofNodeID != null)
                        {
                            optionalMandatoryPlaceholdersOverParent = client.GetOptionalAndMandatoryPlaceholders(typeNodeIdofNodeID);
                        }
                        //
                        List<NodeId> typeParentNodeIds = new List<NodeId>();
                        GetTypeParentNodeIds(parentTypeDefinition, typeParentNodeIds);
                        foreach (NodeId typeParentNodeId in typeParentNodeIds)
                        {
                            NodeId? typeNodeIdofNodeIDFromSubType = client.BrowseLocalNodeIdWithBrowseName(typeParentNodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true, browseName);
                            if (typeNodeIdofNodeIDFromSubType != null)
                            {
                                optionalMandatoryPlaceholdersOverParent.AddRange(client.GetOptionalAndMandatoryPlaceholders(typeNodeIdofNodeIDFromSubType));
                            }
                        }
                    }
                }
            }
            // Check for OptionalPlaceholder and MandatoryPlaceholder in the node Type
            List<NodeId> optionalMandatoryPlaceholders = new List<NodeId>();
            NodeId? ptypeDefinition = client.BrowseTypeDefinition(nodeId);
            if (ptypeDefinition != null)
            {
                List<NodeId> typeAndSuperTypes = new List<NodeId>();
                SuperTypeList(ptypeDefinition, typeAndSuperTypes);
                foreach (NodeId typeDefinition in typeAndSuperTypes)
                {
                    optionalMandatoryPlaceholders.AddRange(this.client.GetOptionalAndMandatoryPlaceholders(typeDefinition));
                }
            }
            List<TypeClassNode> TypeClassNodes = client.GetTypeClassNodesForNodeId(nodeId);
            OptionalPlaceholdersByTypeClasses = client.GetOptionalAndMandatoryPlaceholdersForTypeClassNodes(TypeClassNodes);
            foreach (NodeId TypeClassNodeId in OptionalPlaceholdersByTypeClasses)
            {
                optionalMandatoryPlaceholders.AddRange(client.GetOptionalAndMandatoryPlaceholders(TypeClassNodeId));
            }
            optionalMandatoryPlaceholders.AddRange(optionalMandatoryPlaceholdersOverParent);
            return optionalMandatoryPlaceholders;
        }
        private void SuperTypeList(NodeId typeNodeId, List<NodeId> superTypes)
        {
            superTypes.Add(typeNodeId);
            List<NodeId> superTypesOfType = this.client.BrowseNodeIds(new BrowseDescriptionCollection { BrowseUtils.InverseBrowseDescription(typeNodeId, (int)NodeClass.VariableType | (int)NodeClass.ObjectType, ReferenceTypeIds.HasSubtype, true) });
            //List<NodeId> superTypesOfType = this.client.BrowseLocalNodeIds(typeNodeId, BrowseDirection.Inverse, (int)NodeClass.VariableType | (int)NodeClass.ObjectType, ReferenceTypeIds.HasSubtype, true);
            foreach (NodeId superType in superTypesOfType)
            {
                SuperTypeList(superType, superTypes);
            }
        }

        private void addKnownBrowsePath(NodeId childNodeId, JToken childObject, NodeId? ParentId, MachineNode? machineNode = null)
        {
            if (machineNode == null)
            {
                if (!knownBrowsePaths.ContainsKey(childNodeId))
                {
                    PublishedBrowsePaths publishedBrowsePaths = new PublishedBrowsePaths(childNodeId, ParentId);
                    publishedBrowsePaths.browsePaths.Add(childObject.Path, childObject);
                    knownBrowsePaths.Add(childNodeId, publishedBrowsePaths);
                }
                else
                {
                    PublishedBrowsePaths? publishedBrowsePaths;
                    if (knownBrowsePaths.TryGetValue(childNodeId, out publishedBrowsePaths))
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
                    Logger.Info(childObject.Path);
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
                WriteIdentification(IdentificationArray, InstanceNSU, TypeBrowseName);
            }
            catch (Exception e)
            {
                Logger.Info(e.ToString());
                throw;
            }
        }
        public void publishIdentificationMachineNodes()
        {
            try
            {
                MqttProviderConfig config = this.app.ActiveConfiguration.MqttProviderConfig;
                JArray identificationArray = new JArray();
                foreach (MachineNode machineNode in MachineNodes)
                {
                    if (machineNode != null && machineNode.ResolvedNodeId != null)
                    {
                        {
                            List<NodeId> identificationNodes = client.BrowseNodeIds(new BrowseDescriptionCollection { BrowseUtils.GetHierarchicalChildren(machineNode.ResolvedNodeId, (int)NodeClass.Object) });
                            //List<NodeId> identificationNodes = client.BrowseLocalNodeIds(machineNode.ResolvedNodeId, BrowseDirection.Forward, (int)NodeClass.Object, ReferenceTypeIds.HierarchicalReferences, true);
                            foreach (NodeId child in identificationNodes)
                            {
                                Node? childNode = client.ReadNode(child);
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
                                            data.Add("Topic", config.Prefix + "/" + config.ClientId + "/" + machineNode.TypeBrowseName + "/" + machineNode.InstanceNamespace);
                                            data.Add("TypeDefinition", machineNode.TypeBrowseName);
                                        }
                                        else
                                        {
                                            data.Add("Topic", config.Prefix + "/" + config.ClientId + "/" + machineNode.BaseType + "/" + machineNode.InstanceNamespace);
                                            data.Add("TypeDefinition", machineNode.BaseType);
                                        }
                                        identificationArray.Add(data);
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(machineNode.BaseType))
                            {
                                WriteIdentification(identificationArray, machineNode.InstanceNamespace, machineNode.TypeBrowseName);
                            }
                            else
                            {
                                WriteIdentification(identificationArray, machineNode.InstanceNamespace, machineNode.BaseType);
                            }
                            IdentificationArray = identificationArray;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Info(e.ToString());
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
                Node? node = client.ReadNode(nodeId);
                if (node == null)
                {
                    AddError(nodeId, $"Unable to retrieve Node for nodeId: {nodeId}");
                    return "";
                }
                if (node.NodeClass != NodeClass.Variable)
                {
                    return "";
                }
                DataValue? dv = client.ReadValue(nodeId);
                if (dv == null)
                {
                    return jsonConverter.GetDefaultNullValue();
                }
                object value = dv.Value;
                switch (value)
                {
                    case null: return jsonConverter.GetDefaultNullValue();
                    case bool booleanValue: return jsonConverter.Convert(booleanValue);
                    case byte byteValue: return jsonConverter.Convert(byteValue);
                    case byte[] byteStringvalue: return jsonConverter.Convert(byteStringvalue);
                    case DateTime dateTimeValue: return jsonConverter.Convert(dateTimeValue);
                    case DiagnosticInfo diagnosticInfoValue: return jsonConverter.Convert(diagnosticInfoValue);
                    case double doubleValue: return jsonConverter.Convert(doubleValue);
                    case ExpandedNodeId expandedNodeId: return jsonConverter.Convert(expandedNodeId);
                    case float floatValue: return jsonConverter.Convert(floatValue);
                    case Guid guidValue: return jsonConverter.Convert(guidValue);
                    case short int16Value: return jsonConverter.Convert(int16Value);
                    case int int32Value: return jsonConverter.Convert(int32Value);
                    case long int64Value: return jsonConverter.Convert(int64Value);
                    case LocalizedText localizedTextValue: return jsonConverter.Convert(localizedTextValue);
                    case NodeId nodeIdValue: return jsonConverter.Convert(nodeIdValue);
                    case QualifiedName qualifiedNameValue: return jsonConverter.Convert(qualifiedNameValue);
                    case sbyte sByteValue: return jsonConverter.Convert(sByteValue);
                    case StatusCode statusCodeValue: return jsonConverter.Convert(statusCodeValue);
                    case string stringValue: return jsonConverter.Convert(stringValue);
                    case ushort uint16Value: return jsonConverter.Convert(uint16Value);
                    case uint uint32Value: return jsonConverter.Convert(uint32Value);
                    case ulong uint64Value: return jsonConverter.Convert(uint64Value);
                    //TODO investigate in this...
                    case Variant variantValue: return variantValue.ToString();
                    case XmlElement xmlElementValue: return jsonConverter.Convert(xmlElementValue);
                    case ExtensionObject extensionObjectValue:
                        JObject jobject = new JObject();
                        ExtensionObject extObject = (ExtensionObject)value;
                        ExtensionObjectEncoding encoding = extObject.Encoding;
                        if (encoding == ExtensionObjectEncoding.Binary)
                        {
                            jobject = decode(extObject);
                        }
                        else if (encoding == ExtensionObjectEncoding.Json)
                        {
                            AddError(nodeId, "JSON encoding is currently not implemented for ExtensionObjects.");
                        }
                        else if (encoding == ExtensionObjectEncoding.Xml)
                        {
                            AddError(nodeId, "XML encoding is currently not implemented for ExtensionObjects.");
                        }
                        else if (encoding == ExtensionObjectEncoding.EncodeableObject)
                        {
                            return decodeEncodeable(extObject, nodeId);
                        }
                        return jobject;
                    case ExtensionObject[] extensionObjects:
                        JArray jArray = new JArray();
                        foreach (ExtensionObject extensionObject in extensionObjects)
                        {
                            ExtensionObjectEncoding encodinga = extensionObject.Encoding;
                            if (encodinga == ExtensionObjectEncoding.Binary)
                            {
                                jArray.Add(decode(extensionObject));
                            }
                            else if (encodinga == ExtensionObjectEncoding.Json)
                            {
                                AddError(nodeId, "JSON encoding is currently not implemented for ExtensionObjects.");
                            }
                            else if (encodinga == ExtensionObjectEncoding.Xml)
                            {
                                AddError(nodeId, "XML encoding is currently not implemented for ExtensionObjects.");
                            }
                            else if (encodinga == ExtensionObjectEncoding.EncodeableObject)
                            {
                                jArray.Add(decodeEncodeable(extensionObject, nodeId));
                            }
                        }
                        return jArray;
                    default: AddError(nodeId, $"Unimplemented Type for nodeId: {nodeId.ToString()}."); return "";
                }
            }
            catch (OpcUaException opcUaException)
            {
                Exception? innerException = opcUaException.InnerException;
                if (innerException != null && innerException is ServiceResultException)
                {
                    return innerException.Message;
                }
                return opcUaException.Message;
            }
            catch (Exception ex)
            {
                return ex.Message;
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
                    case Argument argument: return jsonConverter.Convert(argument);
                    case EUInformation euInformation: return jsonConverter.Convert(euInformation);
                    case Opc.Ua.Range range: return jsonConverter.Convert(range);
                    case EnumValueType enumValueType: return jsonConverter.Convert(enumValueType);
                    default: AddError(nodeId, $"The type of the iEncodeable is not implemented {iEncodeable.GetType()}"); return new JObject();
                }
            }
            else
            {
                AddError(nodeId, "The Encodeable Object is not of Type: IEncodeable");
                return new JObject();
            }
        }
        /// <summary>
        /// Adds an error to the Error List.
        /// </summary>
        /// <param name="nodeId">The nodeId on that the Error occurred.</param>
        /// <param name="message">The message related to the error.</param>
        private void AddError(NodeId? nodeId, string message)
        {
            if (nodeId != null)
            {
                if (errors.ContainsKey(nodeId))
                {
                    errors.TryGetValue(nodeId, out var errorList);
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
                        errors.Add(nodeId, errorList);
                    }
                }
                else
                {
                    errors.Add(nodeId, new List<string> { message });
                }
            }
        }
        public object decode(Variant variant)
        {
            JObject jObject = new JObject();
            object obj = variant.Value;
            switch (obj)
            {
                case bool boolValue: return this.jsonConverter.Convert(boolValue);
                case sbyte sByteValue: return this.jsonConverter.Convert(sByteValue);
                case byte byteValue: return this.jsonConverter.Convert(byteValue);
                case short shortValue: return this.jsonConverter.Convert(shortValue);
                case ushort ushortValue: return this.jsonConverter.Convert(ushortValue);
                case int intValue: return this.jsonConverter.Convert(intValue);
                case uint uintValue: return this.jsonConverter.Convert(uintValue);
                case long longValue: return this.jsonConverter.Convert(longValue);
                case ulong ulongValue: return this.jsonConverter.Convert(ulongValue);
                case float floatValue: return this.jsonConverter.Convert(floatValue);
                case double doubleValue: return this.jsonConverter.Convert(doubleValue);
                case string stringValue: return this.jsonConverter.Convert(stringValue);
                case DateTime dateTimeValue: return this.jsonConverter.Convert(dateTimeValue);
                case Guid guidValue: return this.jsonConverter.Convert(guidValue);
                case byte[] byteStringValue: return this.jsonConverter.Convert(byteStringValue);
                case XmlElement xmlElementValue: return this.jsonConverter.Convert(xmlElementValue);
                case NodeId nodeIdValue: return this.jsonConverter.Convert(nodeIdValue);
                case ExpandedNodeId expandedNodeIdValue: return this.jsonConverter.Convert(expandedNodeIdValue);
                case StatusCode statusCodeValue: return this.jsonConverter.Convert(statusCodeValue);
                case QualifiedName qualifiedNameValue: return this.jsonConverter.Convert(qualifiedNameValue);
                case LocalizedText localizedTextValue: return this.jsonConverter.Convert(localizedTextValue);
                case ExtensionObject extensionObject: return decode(extensionObject);
                case DataValue dataValue: return this.jsonConverter.Convert(dataValue, this.decode((Variant)dataValue.Value));
                case DiagnosticInfo diagnosticInfo: return this.jsonConverter.Convert(diagnosticInfo);
                default: return jObject;
            }
        }
        public JObject decode(ExtensionObject extensionObject)
        {
            ICustomEncoding? customEncoding = customEncodingManager.GetActiveEncodingForNodeId(extensionObject.TypeId);
            if (customEncoding != null)
            {

                JObject? decoded = customEncoding.decode(extensionObject);
                if (decoded != null) return decoded;
                else return new JObject();

            }
            JObject jObject = new JObject();
            Logger.Trace("ExtensionObject Expanded NodeId: {ExtensionObjectTypeId}", extensionObject.TypeId);
            NodeId etoId = ExpandedNodeId.ToNodeId(extensionObject.TypeId, client.GetNamespaceTable());
            Logger.Trace("ExtensionObject NodeId: {ExtensionObjectId}", etoId);
            NodeId? dataType = client.BrowseLocalNodeId(etoId, BrowseDirection.Inverse, (uint)NodeClass.DataType, ReferenceTypeIds.HasEncoding, true);
            if (dataType != null)
            {
                Logger.Trace("DataType NodeId: {DataType}", dataType);
            }
            else
            {
                dataType = etoId;
                Logger.Trace("DataType NodeId: {DataType} Took otherId as NodeId", dataType);
            }
            Dictionary<NodeId, Node> dataTypes = app.OpcUaClient.GetTypeDictionaries().GetDataTypes();
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
                    Dictionary<GeneratedDataTypeDefinition, GeneratedDataClass> gclasses = app.OpcUaClient.GetTypeDictionaries().generatedDataTypes;
                    DataTypeNode dtn = (DataTypeNode)value;
                    GeneratedDataTypeDefinition generatedDataTypeDefinition = new GeneratedDataTypeDefinition(client.GetNamespaceTable().GetString(dtn.NodeId.NamespaceIndex), dtn.BrowseName.Name);
                    gclasses.TryGetValue(generatedDataTypeDefinition, out GeneratedDataClass? gdc);
                    ExtensionObject dtd = dtn.DataTypeDefinition;
                    if (gdc != null)
                    {
                        BinaryDecoder BinaryDecoder = new BinaryDecoder((byte[])extensionObject.Body, ServiceMessageContext.GlobalContext);
                        jObject = decode(BinaryDecoder, gdc);
                    }
                }
            }
            return jObject;
        }
        public GeneratedDataClass? GetGeneratedDataClass(string namespaceurl, string browsename)
        {
            Dictionary<GeneratedDataTypeDefinition, GeneratedDataClass> gclasses = app.OpcUaClient.GetTypeDictionaries().generatedDataTypes;
            GeneratedDataTypeDefinition generatedDataTypeDefinition = new GeneratedDataTypeDefinition(namespaceurl, browsename);
            gclasses.TryGetValue(generatedDataTypeDefinition, out GeneratedDataClass? gdc);
            return gdc;
        }
        public JObject decode(BinaryDecoder BinaryDecoder, GeneratedDataClass generatedDataClass)
        {
            string currentFieldName = "";
            string currentFieldType = "";
            JObject jObject = new JObject();
            try
            {
                if (generatedDataClass != null)
                {
                    if (generatedDataClass is GeneratedStructure)
                    {
                        GeneratedStructure generatedStructure = (GeneratedStructure)generatedDataClass;
                        int previousInt32 = 0;
                        uint mask = 0;
                        int currentSwitchBit = 0;
                        bool lastFieldWasSwitchedOff = false;
                        foreach (GeneratedField field in generatedStructure.fields)
                        {
                            Logger.Trace("Decode: {FieldName} {FieldType}", field.Name, field.TypeName);
                            currentFieldName = field.Name;
                            currentFieldType = field.TypeName;
                            if (field.IsLengthField == true)
                            {
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
                                        string valueString = BinaryDecoder.ReadString(field.Name);
                                        array.Add(valueString);
                                    }
                                    jObject.Add(field.Name, array);
                                }
                                else if (field.TypeName == "opc:Double")
                                {
                                    JArray array = new JArray();
                                    for (int i = 0; i < previousInt32; i++)
                                    {
                                        double doubleValue = BinaryDecoder.ReadDouble(field.Name);
                                        array.Add(doubleValue.ToString());
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
                                                GeneratedDataClass? gdc2 = GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.ua, field.TypeName.Substring(3));
                                                if (gdc2 != null)
                                                {
                                                    array.Add(decode(BinaryDecoder, gdc2));
                                                }
                                            }
                                        }
                                    }
                                    jObject.Add(field.Name, array);
                                }
                                else if (field.TypeName == "ua:LocalizedText")
                                {
                                    JArray array = new JArray();
                                    for (int i = 0; i < previousInt32; i++)
                                    {
                                        LocalizedText localizedTextValue = BinaryDecoder.ReadLocalizedText(field.Name);
                                        Logger.Info("Value: {LocalizedTextValue}", localizedTextValue);
                                        JObject localizedTextObject = new JObject();
                                        localizedTextObject.Add("locale", localizedTextValue.Locale);
                                        localizedTextObject.Add("text", localizedTextValue.Text);
                                        array.Add(localizedTextObject);
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
                                                GeneratedDataClass? gdc2 = GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.ua, field.TypeName.Substring(3));
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
                                                GeneratedDataClass? gdc2 = GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.tns, field.TypeName.Substring(4));
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
                                    bool optionalFieldPresent = IsBitSet(mask, currentSwitchBit);
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
                                    bool valueBoolean = BinaryDecoder.ReadBoolean(field.Name);
                                    jObject.Add(field.Name, valueBoolean);
                                }
                                else if (field.TypeName == "opc:Byte")
                                {
                                    byte valueByte = BinaryDecoder.ReadByte(field.Name);
                                    jObject.Add(field.Name, valueByte.ToString());
                                }
                                else if (field.TypeName == "opc:ByteString")
                                {
                                    ByteCollection valueByteCollection = BinaryDecoder.ReadByteString(field.Name);
                                    jObject.Add(field.Name, valueByteCollection.ToString());
                                }
                                else if (field.TypeName == "opc:CharArray")
                                {
                                    string valueString = BinaryDecoder.ReadString(field.Name);
                                    jObject.Add(field.Name, valueString);
                                }
                                else if (field.TypeName == "opc:DateTime")
                                {
                                    DateTime dateTimeValue = BinaryDecoder.ReadDateTime(field.Name);
                                    jObject.Add(field.Name, dateTimeValue.ToString());
                                }
                                else if (field.TypeName == "opc:Double")
                                {
                                    double doubleValue = BinaryDecoder.ReadDouble(field.Name);
                                    jObject.Add(field.Name, doubleValue.ToString());
                                }
                                else if (field.TypeName == "opc:Float")
                                {
                                    float floatValue = BinaryDecoder.ReadFloat(field.Name);
                                    jObject.Add(field.Name, floatValue.ToString());
                                }
                                else if (field.TypeName == "opc:Guid")
                                {
                                    Uuid valueGuid = BinaryDecoder.ReadGuid(field.Name);
                                    jObject.Add(field.Name, valueGuid.ToString());
                                }
                                else if (field.TypeName == "opc:Int16")
                                {
                                    short valueInt16 = BinaryDecoder.ReadInt16(field.Name);
                                    jObject.Add(field.Name, valueInt16.ToString());
                                }
                                else if (field.TypeName == "opc:Int32")
                                {
                                    int valueInt32 = BinaryDecoder.ReadInt32(field.Name);
                                    if (!field.Name.StartsWith("NoOf"))
                                    {
                                        jObject.Add(field.Name, valueInt32.ToString());
                                    }
                                    previousInt32 = valueInt32;
                                }
                                else if (field.TypeName == "opc:Int64")
                                {
                                    long valueInt64 = BinaryDecoder.ReadInt64(field.Name);
                                    jObject.Add(field.Name, valueInt64.ToString());
                                }
                                else if (field.TypeName == "opc:SByte")
                                {
                                    sbyte valueSByte = BinaryDecoder.ReadSByte(field.Name);
                                    jObject.Add(field.Name, valueSByte.ToString());
                                }
                                else if (field.TypeName == "opc:String")
                                {
                                    string valueString = BinaryDecoder.ReadString(field.Name);
                                    jObject.Add(field.Name, valueString);
                                }
                                else if (field.TypeName == "opc:UInt16")
                                {
                                    ushort uint16Value = BinaryDecoder.ReadUInt16(field.Name);
                                    jObject.Add(field.Name, uint16Value.ToString());
                                }
                                else if (field.TypeName == "opc:UInt32")
                                {
                                    uint uint32Value = BinaryDecoder.ReadUInt32(field.Name);
                                    jObject.Add(field.Name, uint32Value.ToString());
                                }
                                else if (field.TypeName == "opc:UInt64")
                                {
                                    ulong uint64Value = BinaryDecoder.ReadUInt64(field.Name);
                                    jObject.Add(field.Name, uint64Value.ToString());
                                }
                                else if (field.TypeName == "ua:LocalizedText")
                                {
                                    LocalizedText localizedTextValue = BinaryDecoder.ReadLocalizedText(field.Name);
                                    JObject localizedTextObject = new JObject();
                                    localizedTextObject.Add("locale", localizedTextValue.Locale);
                                    localizedTextObject.Add("text", localizedTextValue.Text);
                                    jObject.Add(field.Name, localizedTextObject);
                                }
                                if (field.TypeName == "ua:ExtensionObject")
                                {
                                    if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.tns != null && field.Name == "ResultMetaData" && useGMSResultEncoding)
                                    {
                                        GeneratedDataClass? gdc2 = GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.tns, "ResultMetaDataType");
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
                                    if (value is string)
                                    {
                                        jObject.Add(field.Name, (string)value);
                                    }
                                    else if (value is JToken)
                                    {
                                        jObject.Add(field.Name, (JToken)value);
                                    }
                                }
                                else if (field.TypeName.StartsWith("ua:") && field.TypeName != "ua:LocalizedText" && field.TypeName != "ua:Variant")
                                {
                                    if (generatedDataClass.DataTypeDefinition != null && generatedDataClass.DataTypeDefinition.ua != null)
                                    {
                                        GeneratedDataClass? gdc2 = GetGeneratedDataClass(generatedDataClass.DataTypeDefinition.ua, field.TypeName.Substring(3));
                                        if (gdc2 != null)
                                        {
                                            jObject.Add(field.Name, decode(BinaryDecoder, gdc2));
                                        }
                                    }
                                }
                                else if (field.TypeName.StartsWith("ns") || field.TypeName.StartsWith("tns:"))
                                {
                                    string typename = field.TypeName;
                                    string namespaceKey = typename.Substring(0, 3);
                                    string browseName = typename.Substring(4);
                                    string? nameSpaceUrl;
                                    if (generatedDataClass.DataTypeDefinition != null)
                                    {
                                        if (generatedDataClass.DataTypeDefinition.extraNameSpaces.TryGetValue(namespaceKey, out nameSpaceUrl))
                                        {
                                            GeneratedDataClass? gdc2 = GetGeneratedDataClass(nameSpaceUrl, browseName);
                                            if (gdc2 != null)
                                            {
                                                if (gdc2 is GeneratedStructure)
                                                {
                                                    JObject decoded = decode(BinaryDecoder, gdc2);
                                                    jObject.Add(field.Name, decoded);
                                                }
                                                else if (gdc2 is GeneratedEnumeratedType)
                                                {
                                                    GeneratedEnumeratedType gde = (GeneratedEnumeratedType)gdc2;
                                                    if (!gde.IsOptionSet)
                                                    {
                                                        uint uint32Value = BinaryDecoder.ReadUInt32(field.Name);
                                                        string valueName = "";
                                                        foreach (GeneratedEnumeratedValue enumeratedValue in gde.enumValues)
                                                        {
                                                            if (enumeratedValue.Value == uint32Value.ToString())
                                                            {
                                                                valueName = enumeratedValue.Name;
                                                                break;
                                                            }
                                                        }
                                                        jObject.Add(field.Name, $"{uint32Value} ({valueName})");
                                                    }
                                                    else
                                                    {
                                                        uint uint32Value = BinaryDecoder.ReadUInt32(field.Name);
                                                        List<string> names = new List<string>();
                                                        foreach (GeneratedEnumeratedValue enumeratedValue in gde.enumValues)
                                                        {
                                                            uint bit = uint.Parse(enumeratedValue.Value);
                                                            if ((uint32Value & 1u << (int)bit) != 0)
                                                            {
                                                                names.Add(enumeratedValue.Name);
                                                            }
                                                        }
                                                        jObject.Add(field.Name, $"{uint32Value} ({string.Join("|", names)})");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Logger.Error("Could not find GeneratedDataClass for field: {Field}", field);
                                            }
                                        }
                                        else
                                        {
                                            Logger.Error("Could not find Namespace {NamespaceKey} for GeneratedDataClass {GeneratedDataClass}", namespaceKey, generatedDataClass);
                                        }
                                    }
                                    else
                                    {
                                        Logger.Error("Could not find DataTypeDefinition for GeneratedDataClass: {GeneratedDataClass}", generatedDataClass);
                                    }
                                }
                            }
                        }
                    }
                    else if (generatedDataClass is GeneratedEnumeratedType)
                    {
                        GeneratedEnumeratedType gde = (GeneratedEnumeratedType)generatedDataClass;
                        if (!gde.IsOptionSet)
                        {
                            uint uint32Value = BinaryDecoder.ReadUInt32(gde.Name);
                            string valueName = "";
                            foreach (GeneratedEnumeratedValue enumeratedValue in gde.enumValues)
                            {
                                if (enumeratedValue.Value == uint32Value.ToString())
                                {
                                    valueName = enumeratedValue.Name;
                                    break;
                                }
                            }
                            jObject.Add(gde.Name, $"{uint32Value} ({valueName})");
                        }
                        else
                        {
                            uint uint32Value = BinaryDecoder.ReadUInt32(gde.Name);
                            List<string> names = new List<string>();
                            foreach (GeneratedEnumeratedValue enumeratedValue in gde.enumValues)
                            {
                                uint bit = uint.Parse(enumeratedValue.Value);
                                if ((uint32Value & 1u << (int)bit) != 0)
                                {
                                    names.Add(enumeratedValue.Name);
                                }
                            }
                            jObject.Add(gde.Name, $"{uint32Value} ({string.Join("|", names)})");
                        }
                    }
                }
                return jObject;
            }
            catch (Exception ex)
            {
                jObject.Add($"Exception reading field:'{currentFieldName}' with type '{currentFieldType}'", ex.Message);
                return jObject;
            }
        }

        private string GetNameSpaceForIndex(ushort NamespaceIndex)
        {
            string ns = "";
            DataValue? dv = client.ReadValue(VariableIds.Server_NamespaceArray);
            if (dv != null)
            {
                string[] namespaces = (string[])dv.Value;
                ns = namespaces[NamespaceIndex];
            }
            return ns;
        }

        /// <summary>
        /// Builds a fully qualified OPC UA NodeId string with the format: 
        /// "nsu=&lt;encodedNamespaceUri&gt;;&lt;encodedIdentifier&gt;".
        /// All non-alphanumeric characters in the namespace URI and string identifiers
        /// are URL-encoded using an underscore (_) instead of a percent sign (%).
        /// </summary>
        /// <param name="nodeId">The NodeId to encode. Null values will return an empty string.</param>
        /// <param name="replace">
        /// Optional flag to enable custom URL encoding for namespace URI and string-based identifiers.
        /// Default is true.
        /// </param>
        /// <returns>
        /// A fully qualified NodeId string compliant with OPC UA string representations,
        /// or an empty string if <paramref name="nodeId"/> is null.
        /// </returns>
        private string getInstanceNsu(NodeId? nodeId, bool replace = true)
        {
            const string nsuPrefix = "nsu=";
            string identifier = "";

            if (nodeId == null)
            {
                Logger.Error("No NodeId for Instance");
                return "";
            }

            // Retrieve the namespace URI from the namespace index
            string namespaceUri = GetNameSpaceForIndex(nodeId.NamespaceIndex);
            // Apply custom encoding if required
            if (replace)
            {
                namespaceUri = CustomUrlEncode(namespaceUri);
                identifier = nodeId.IdType switch
                {
                    IdType.Numeric => $"i={(uint)nodeId.Identifier}",
                    IdType.String => $"s={CustomUrlEncode((string)nodeId.Identifier)}",
                    IdType.Guid => $"g={nodeId.Identifier.ToString()}",
                    IdType.Opaque => $"b={Convert.ToBase64String((byte[])nodeId.Identifier)}",
                    _ => throw new NotSupportedException($"Unsupported IdType: {nodeId.IdType}")
                };
            }
            else
            {
                identifier = nodeId.IdType switch
                {
                    IdType.Numeric => $"i={(uint)nodeId.Identifier}",
                    IdType.String => $"s={(string)nodeId.Identifier}",
                    IdType.Guid => $"g={nodeId.Identifier.ToString()}",
                    IdType.Opaque => $"b={Convert.ToBase64String((byte[])nodeId.Identifier)}",
                    _ => throw new NotSupportedException($"Unsupported IdType: {nodeId.IdType}")
                };
            }
            return nsuPrefix + namespaceUri + ";" + identifier;
        }

        /// <summary>
        /// Encodes a string using a custom URL-encoding (RFC3986) scheme:
        /// - Unreserved characters [A-Za-z0-9], '-', '.', '_', and '~' remain unchanged.
        /// - All other characters are replaced with an underscore (_) followed by
        ///   their two-digit uppercase hexadecimal ASCII value.
        /// Example: "My Value/Path" becomes "My_20Value_2FPath".
        /// </summary>
        /// <param name="input">The input string to encode.</param>
        /// <returns>The encoded string.</returns>
        private string CustomUrlEncode(string input)
        {
            StringBuilder encoded = new StringBuilder();

            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '_' || c == '~')
                {
                    encoded.Append(c);
                }
                else
                {
                    encoded.Append($"_{(int)c:X2}");
                }
            }

            return encoded.ToString();
        }



        private void doPublish()
        {
            if (connected)
            {
                try
                {
                    if (!firstReadFinished)
                    {
                        if (!ReadInProgress)
                        {
                            Logger.Info("Start Initial Reading.");
                            ReadInProgress = true;
                            Logger.Info("Read InstanceNsu and BrowseName");
                            ReadInstanceNsuAndBrowseName();
                            Logger.Info("Publish BadList MachineNodes");
                            PublishBadListMachineNodes();
                            Logger.Info("Publish BadList MachineNodes.");
                            Logger.Info("Publish Client Online");
                            publishClientOnline();
                            Logger.Info("Publish Client Online finish.");
                            Logger.Info("Publish Machine");
                            publishNodeMachineNodes();
                            Logger.Info("Publish Machine finished.");
                            publishNodeAfterSubscriptionMachineNodes();
                            Logger.Info("Publish Online Machines Machine Node");
                            publishOnlineMachinesMachineNode();
                            Logger.Info("Publish Online Machines Machine Node finish.");
                            Logger.Info("Publish Identification Machine Node");
                            publishIdentificationMachineNodes();
                            Logger.Info("Publish Identification Machine Node finish.");
                            foreach (MachineNode machineNode in MachineNodes)
                            {
                                List<NodeId> nodeIdsToSubscribe = new List<NodeId>();
                                foreach (KeyValuePair<NodeId, PublishedBrowsePaths> entry in machineNode.KnownBrowsePaths)
                                {
                                    nodeIdsToSubscribe.Add(entry.Key);
                                }
                                client.SubscribeToDataChanges(nodeIdsToSubscribe, updateDataValue);
                            }
                            ReadInProgress = false;
                            firstReadFinished = true;
                        }

                    }
                    else
                    {
                        //Detect OPC disconnect
                        _ = client.ReadNode(ObjectIds.Server);
                        Logger.Info("Publish BadList Machine Nodes");
                        PublishBadListMachineNodes();
                        Logger.Info("Publish Bad List Machine Nodes finish.");
                        Logger.Info("Publish Client Online");
                        publishClientOnline();
                        Logger.Info("Publish Client Online finish.");
                        Logger.Info("Publish Online Machines MachineNode");
                        publishOnlineMachinesMachineNode();
                        Logger.Info("Publish Online Machines Machine Node finish.");
                        Logger.Info("Publish Identification Object");
                        publishIdentificationMachineNodes();
                        Logger.Info("Publish Identification Object");
                        Logger.Info("Publish Machine Object Machine Nodes");
                        publishNodeAfterSubscriptionMachineNodes();
                        Logger.Info("Publish Machine Object Machine Nodes finished.");
                    }

                }
                //Opc.Ua.ServiceResultException: BadNotConnected
                //BadNotConnected
                catch (ServiceResultException ex2)
                {
                    firstReadFinished = false;
                    TypeBrowseName = "";
                    InstanceNSU = "";
                    subscriptions.Clear();
                    client.ClearSubscriptions();
                    Logger.Info("Message:" + ex2.Message);
                    if (ex2.Message == "BadNotConnected")
                    {
                        Logger.Info("Reconnecting OPC");
                        client.Connect();
                    }
                }
                catch (OpcUaException opcUaException)
                {
                    Exception? innerException = opcUaException.InnerException;
                    if (innerException != null && innerException is ServiceResultException)
                    {
                        firstReadFinished = false;
                        TypeBrowseName = "";
                        InstanceNSU = "";
                        subscriptions.Clear();
                        client.ClearSubscriptions();
                        Logger.Info("Message:" + innerException.Message);
                        if (innerException.Message == "BadNotConnected")
                        {
                            Logger.Info("Reconnecting OPC");
                            client.Connect();
                        }
                    }
                }
                catch (MQTTnet.Exceptions.MqttClientNotConnectedException ex)
                {
                    Logger.Info(ex.ToString());
                    connected = false;
                }
                catch (Exception ex)
                {
                    Logger.Info(ex.ToString());
                }
            }
            else
            {
                try
                {
                    if (ConnectedOnce)
                    {
                        Logger.Info("Reconnecting MQTT");
                        Reconnect();
                    }
                }
                catch (Exception ex1)
                {
                    Logger.Info(ex1.ToString());
                }
            }
        }
        private void OnTimedEvent(object? source, ElapsedEventArgs e)
        {
            doPublish();
        }
        private void ReadInstanceNsuAndBrowseName()
        {
            try
            {
                Logger.Debug("Read Instance Nsu for Online Machines");
                foreach (MachineNode machineNode1 in MachineNodes)
                {
                    NodeId? machine = machineNode1.ResolvedNodeId;
                    if (machine != null)
                    {
                        Logger.Debug("Machine Node Id:\t{Machine}", machine);
                        Node? machineNode = client.ReadNode(machine);
                        if (machineNode != null)
                        {
                            NodeId? typedefinition = client.BrowseTypeDefinition(machine);
                            if (typedefinition != null)
                            {
                                Logger.Debug("TypeDefinition NodeId is:\t{TypeDefinition}", typedefinition);
                                Node? TypeDefinitionNode = client.ReadNode(typedefinition);
                                if (TypeDefinitionNode != null)
                                {
                                    InstanceNSU = getInstanceNsu(machine);
                                    TypeBrowseName = TypeDefinitionNode.BrowseName.Name;
                                    Logger.Debug("InstanceNsu:\t{InstanceNSU}\tTypeBrowseName:\t{TypeBrowseName}", InstanceNSU, TypeBrowseName);
                                }
                                else
                                {
                                    Logger.Error("Unable to get TypeDefinitionNode for type NodeId:\t{TypeDefinition}", typedefinition);
                                }
                            }
                            else
                            {
                                Logger.Error("Unable to browse NodeId of TypeDefinition for machine NodeId:\t{Machine}", machine);
                            }
                        }
                        else
                        {
                            Logger.Error("Unable to read machine for NodeId:\t{Machine}", machine);
                        }
                    }
                    else
                    {
                        Logger.Error("Unable to read machine Node. Machine is null.");
                    }
                }
                Logger.Debug("Read InstanceNsu for published Machines");
                foreach (MachineNode machineNode in MachineNodes)
                {
                    Logger.Debug("Machine:\t{NodeIdType}\t{NamespaceUrl}\t{NodeIdString}\t{BaseType}", machineNode.NodeIdType, machineNode.NamespaceUrl, machineNode.NodeIdString, machineNode.BaseType);
                    NodeId? machineNodeId = machineNode.ResolvedNodeId;
                    if (machineNodeId != null)
                    {
                        Logger.Debug("Resolved NodeId is:\t{MachineNodeId}", machineNodeId);
                        NodeId? typedefinitionNodeId = client.BrowseTypeDefinition(machineNodeId);
                        if (typedefinitionNodeId != null)
                        {
                            Logger.Debug("TypeDefinition NodeId is:\t{TypeDefinitionNodeId}", typedefinitionNodeId);
                            Node? typeDefinitionNode = client.ReadNode(typedefinitionNodeId);
                            if (typeDefinitionNode != null)
                            {
                                machineNode.InstanceNamespace = getInstanceNsu(machineNodeId);
                                machineNode.TypeBrowseName = typeDefinitionNode.BrowseName.Name;
                                Logger.Debug("InstanceNsu:\t{InstanceNamespace}\tTypeBrowseName:\t{TypeBrowseName}", machineNode.InstanceNamespace, machineNode.TypeBrowseName);
                            }
                            else
                            {
                                Logger.Error("Unable to get TypeDefinitionNode for Type NodeId:\t{TypeDefinitionNodeId}", typedefinitionNodeId);
                            }
                        }
                        else
                        {
                            Logger.Error("Unable to browse NodeId of Typedefinition for machine NodeId:\t{MachineNodeId}", machineNodeId);
                        }
                    }
                    else
                    {
                        Logger.Error("ResolvedNodeId for machine is null");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Info(e.ToString());
                throw;
            }

        }

        private void updateDataValue(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs monitoredItemsArgs)
        {
            foreach (MachineNode machineNode in MachineNodes)
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
                                JToken replaceToken = createJSON(monitoredItem.ResolvedNodeId);
                                affectedObject.Replace(replaceToken);
                            }
                        }
                    }
                }
            }
            publishNodeAfterSubscriptionMachineNodes();
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
            public void printPlaceholderNode(UmatiGatewayApp client)
            {
                Logger.Trace("PlaceHolderNodeId: {PlaceholderNodeId} = ", placeholderNodeId);
                Node? placeholderNodeIdNode = client.OpcUaClient.ReadNode(placeholderNodeId);
                if (placeholderNodeIdNode != null)
                {
                    Logger.Trace("{PlaceholderBrowsename}", placeholderNodeIdNode.BrowseName);
                }
                else
                {
                    Logger.Trace("Unknown");
                }
                Logger.Trace("TypeDefinitionNodeId: {TypeDefinitionNodeId} = ", typeDefinitionNodeId);
                Node? TypeDefinitionNodeIdNode = client.OpcUaClient.ReadNode(typeDefinitionNodeId);
                if (TypeDefinitionNodeIdNode != null)
                {
                    Logger.Trace("{TypeDefinitionNodeBrowseName}", TypeDefinitionNodeIdNode.BrowseName);
                }
                else
                {
                    Logger.Trace("Unknown");
                }
                Logger.Trace("phList: {PlaceholderList}", phList);
                Logger.Trace("SubTypeNodeIds:");
                foreach (NodeId nodeId in subTypeNodeIds)
                {
                    Logger.Trace("SubTypeNodeId: {NodeId} = ", nodeId);
                    Node? subTypeNode = client.OpcUaClient.ReadNode(nodeId);
                    if (subTypeNode != null)
                    {
                        Logger.Trace("{SubTypeNodeBrowseName}", subTypeNode.BrowseName);
                    }
                    else
                    {
                        Logger.Trace("Unknown");
                    }
                }
            }

        }

        bool IsBitSet(uint value, int pos)
        {
            return (value >> pos & 1) != 0;
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

        // Method, um die Sortierung je nach Token-Typ (JObject, JArray or JValue) rekursiv anzuwenden
        public JToken SortToken(JToken token)
        {
            if (token is JObject)
            {
                // Sortiere rekursiv, wenn es sich um ein JObject handelt
                return SortJsonKeysRecursively((JObject)token);
            }
            else if (token is JArray)
            {
                // Check if elements have to be sorted
                var array = (JArray)token;
                return new JArray(array.Select(SortToken));
            }
            else
            {
                // Wenn es sich um einen Wert (JValue) handelt, bleibt der Wert gleich
                return token;
            }
        }
        public void updateNode(NodeId affectedNode)
        {
            foreach (MachineNode machineNode in MachineNodes)
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

                                createJSON(jObject, path.NodeId, machineNode, path.ParentId);
                                affectedObject.Replace(jObject);
                            }
                        }
                    }
                }
            }
            publishNodeAfterSubscriptionMachineNodes();
        }
        void OpcUaEventListener.ModelChangeEvent(NodeId affectedNode)
        {
            Logger.Info("Update Affected Node {AffectedNode}", affectedNode);
            updateNode(affectedNode);
        }
        void OpcUaEventListener.ResultReadyEvent()
        {
            Logger.Trace("ResultReadyEvent received.");
            if (resultFolder != null)
            {
                Logger.Trace("Update Resultfolder: {ResultFolder}", resultFolder);
                updateNode(resultFolder);
            }
            else
            {
                Logger.Trace("Unable to determine Result Folder");
            }

        }
    }
    public class MqttSubscription
    {
        public NodeId NodeId { get; set; }
        public uint Subscriptionhandle { get; set; }
        public JObject parent;
        public string browseName;

        public MqttSubscription(NodeId nodeId, JObject parent, string browseName, uint SubscriptionHandle)
        {
            NodeId = nodeId;
            this.parent = parent;
            this.browseName = browseName;
            Subscriptionhandle = SubscriptionHandle;
        }
    }
    public class PublishedBrowsePaths
    {
        public NodeId NodeId { get; set; }
        public NodeId? ParentId { get; set; }
        public Dictionary<string, JToken> browsePaths = new Dictionary<string, JToken>();
        public PublishedBrowsePaths(NodeId nodeId, NodeId? ParentId)
        {
            NodeId = nodeId;
            this.ParentId = ParentId;
        }
        public override string ToString()
        {
            string returnString = $"NodeId: {NodeId}";
            foreach (KeyValuePair<string, JToken> entry in browsePaths)
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

        public string PublishedNodeType { get; set; } = "PublishedNode";

        public NodeId? ResolvedNodeId { get; set; }
        public Dictionary<NodeId, PublishedBrowsePaths> KnownBrowsePaths { get; set; } = new Dictionary<NodeId, PublishedBrowsePaths>();

        public MachineNode(string machineNodeId, string namespaceUrl)
        {
            NodeIdString = machineNodeId;
            NamespaceUrl = namespaceUrl;
        }
    }
}