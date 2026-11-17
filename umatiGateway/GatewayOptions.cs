namespace UmatiGateway;

/// <summary>
/// Gateway configuration. Read from appsettings.json (section "Gateway") and environment variables.
/// The flat environment names (OPCUA_ENDPOINT, MQTT_URL, ...) take precedence; they are the same
/// names the standalone adapter uses.
/// Changeable at runtime through PUT /api/config (OPC UA / MQTT connection settings).
/// </summary>
public sealed class GatewayOptions
{
    // ---- OPC UA ----
    public string OpcUaEndpoint { get; set; } = "opc.tcp://localhost:4840/UA";
    public string? OpcUaUser { get; set; }
    public string? OpcUaPassword { get; set; }
    /// <summary>true = pick the most secure endpoint (certificates are auto-accepted), false = SecurityMode None</summary>
    public bool OpcUaUseSecurity { get; set; }
    /// <summary>browse name of the Machines folder below the Objects folder (OPC 40001-1)</summary>
    public string MachinesFolder { get; set; } = "Machines";
    /// <summary>connect on startup (otherwise use the web page / POST /api/connect)</summary>
    public bool AutoConnect { get; set; } = true;
    public int PublishingInterval { get; set; } = 500;
    public int SamplingInterval { get; set; } = 250;
    /// <summary>seconds between scans of the Machines folder for new machines (0 = off)</summary>
    public int RescanInterval { get; set; } = 60;
    public string PkiPath { get; set; } = "pki";

    // ---- MQTT ----
    public bool MqttEnabled { get; set; } = true;
    public string MqttUrl { get; set; } = "mqtt://localhost:1883";
    public string? MqttUsername { get; set; }
    public string? MqttPassword { get; set; }
    public string MqttClientIdPrefix { get; set; } = "umatigateway";
    public string? TopicPrefix { get; set; }

    // ---- data model ----
    /// <summary>ProductInstanceUri | SerialNumber | BrowseName</summary>
    public string MachineIdSource { get; set; } = "ProductInstanceUri";
    public int HeartbeatInterval { get; set; } = 10;
    public bool HeartbeatRetain { get; set; }
    public string? MappingFile { get; set; }

    public GatewayOptions Clone() => (GatewayOptions)MemberwiseClone();

    public static GatewayOptions Load(IConfiguration c)
    {
        var o = new GatewayOptions();
        string? S(string key, string env) => Nz(c[env]) ?? Nz(c[$"Gateway:{key}"]);
        bool B(string key, string env, bool def) => bool.TryParse(S(key, env), out var b) ? b : def;
        int I(string key, string env, int def) => int.TryParse(S(key, env), out var i) ? i : def;

        o.OpcUaEndpoint = S("OpcUa:Endpoint", "OPCUA_ENDPOINT") ?? o.OpcUaEndpoint;
        o.OpcUaUser = S("OpcUa:User", "OPCUA_USER");
        o.OpcUaPassword = S("OpcUa:Password", "OPCUA_PASSWORD");
        o.OpcUaUseSecurity = B("OpcUa:UseSecurity", "OPCUA_USE_SECURITY", o.OpcUaUseSecurity);
        o.MachinesFolder = S("OpcUa:MachinesFolder", "OPCUA_MACHINES_FOLDER") ?? o.MachinesFolder;
        o.AutoConnect = B("OpcUa:AutoConnect", "OPCUA_AUTOCONNECT", o.AutoConnect);
        o.PublishingInterval = I("OpcUa:PublishingInterval", "OPCUA_PUBLISHING_INTERVAL", o.PublishingInterval);
        o.SamplingInterval = I("OpcUa:SamplingInterval", "OPCUA_SAMPLING_INTERVAL", o.SamplingInterval);
        o.RescanInterval = I("OpcUa:RescanInterval", "OPCUA_RESCAN_INTERVAL", o.RescanInterval);
        o.PkiPath = S("OpcUa:PkiPath", "PKI_PATH") ?? o.PkiPath;

        o.MqttEnabled = B("Mqtt:Enabled", "MQTT_ENABLED", o.MqttEnabled);
        o.MqttUrl = S("Mqtt:Url", "MQTT_URL") ?? o.MqttUrl;
        o.MqttUsername = S("Mqtt:Username", "MQTT_USERNAME");
        o.MqttPassword = S("Mqtt:Password", "MQTT_PASSWORD");
        o.MqttClientIdPrefix = S("Mqtt:ClientIdPrefix", "MQTT_CLIENT_ID") ?? o.MqttClientIdPrefix;
        o.TopicPrefix = S("Mqtt:TopicPrefix", "TOPIC_PREFIX");

        o.MachineIdSource = S("MachineIdSource", "MACHINE_ID_SOURCE") ?? o.MachineIdSource;
        o.HeartbeatInterval = Math.Max(1, I("HeartbeatInterval", "HEARTBEAT_INTERVAL", o.HeartbeatInterval));
        o.HeartbeatRetain = B("HeartbeatRetain", "HEARTBEAT_RETAIN", o.HeartbeatRetain);
        o.MappingFile = S("MappingFile", "MAPPING_FILE");
        return o;
    }

    private static string? Nz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
