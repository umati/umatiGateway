using System.Text.Json;
using UmatiGateway.Gateway;
using UmatiGateway.Model;
using UmatiGateway.Output;

namespace UmatiGateway.Api;

/// <summary>REST binding of the MachineTool-DataModel plus gateway control endpoints used by the web page.</summary>
public static class ApiEndpoints
{
    public sealed record ConfigRequest(
        string? OpcUaEndpoint, string? OpcUaUser, string? OpcUaPassword, bool? OpcUaUseSecurity, string? MachinesFolder,
        bool? MqttEnabled, string? MqttUrl, string? MqttUsername, string? MqttPassword, string? TopicPrefix,
        string? MachineIdSource, int? HeartbeatInterval, bool? Connect);

    /// <summary>mandatory (M) elements of the Core profile; a machine lacking one is not a conformant data-model machine</summary>
    private static readonly string[] CoreElements =
    {
        "Identification.Manufacturer", "Identification.SerialNumber", "Identification.ProductInstanceUri",
        "Connection.Heartbeat", "Connection.State", "State.Status", "Notification.ActiveAlerts",
    };

    private static IResult J(object? o, int status = 200) => Results.Json(o, Json.Options, statusCode: status);
    private static IResult NotFound(string msg) => J(new { error = msg }, 404);

    public static void MapGatewayApi(this WebApplication app)
    {
        var gw = app.Services.GetRequiredService<GatewayService>();
        var hub = app.Services.GetRequiredService<MessageHub>();

        app.MapGet("/health", () => J(new { status = "ok", opcUa = gw.State }));

        var api = app.MapGroup("/api");

        // ------------------------------------------------------------ gateway control

        api.MapGet("/status", () => J(Status(gw)));
        api.MapGet("/config", () => J(ConfigView(gw.Options)));

        api.MapPut("/config", (ConfigRequest r) =>
        {
            var o = gw.Options.Clone();
            if (!string.IsNullOrWhiteSpace(r.OpcUaEndpoint)) o.OpcUaEndpoint = r.OpcUaEndpoint.Trim();
            if (r.OpcUaUser is not null) o.OpcUaUser = string.IsNullOrWhiteSpace(r.OpcUaUser) ? null : r.OpcUaUser;
            if (r.OpcUaPassword is not null) o.OpcUaPassword = r.OpcUaPassword;
            if (r.OpcUaUseSecurity is { } sec) o.OpcUaUseSecurity = sec;
            if (!string.IsNullOrWhiteSpace(r.MachinesFolder)) o.MachinesFolder = r.MachinesFolder.Trim();
            if (r.MqttEnabled is { } me) o.MqttEnabled = me;
            if (!string.IsNullOrWhiteSpace(r.MqttUrl)) o.MqttUrl = r.MqttUrl.Trim();
            if (r.MqttUsername is not null) o.MqttUsername = string.IsNullOrWhiteSpace(r.MqttUsername) ? null : r.MqttUsername;
            if (r.MqttPassword is not null) o.MqttPassword = r.MqttPassword;
            if (r.TopicPrefix is not null) o.TopicPrefix = r.TopicPrefix.Trim('/');
            if (r.MachineIdSource is { } src)
            {
                if (src is not ("ProductInstanceUri" or "SerialNumber" or "BrowseName")) return J(new { error = "machineIdSource must be ProductInstanceUri, SerialNumber or BrowseName" }, 400);
                o.MachineIdSource = src;
            }
            if (r.HeartbeatInterval is { } hb) o.HeartbeatInterval = Math.Max(1, hb);
            gw.ApplyOptions(o, r.Connect ?? true);
            return J(new { applied = true, config = ConfigView(o) });
        });

        api.MapPost("/connect", () => { gw.Connect(); return J(new { requested = "connect" }); });
        api.MapPost("/disconnect", () => { gw.Disconnect(); return J(new { requested = "disconnect" }); });
        api.MapPost("/rescan", () => { gw.Rescan(); return J(new { requested = "rescan" }); });
        api.MapGet("/mapping", () => gw.Mapping is { } m ? J(m) : NotFound("no mapping loaded (not connected yet)"));

        // ------------------------------------------------------------ machines

        api.MapGet("/machines", () => J(gw.Machines.Where(m => m.Started).Select(m => Summary(m, false)).ToList()));

        api.MapGet("/machines/{machine}", (string machine) =>
            gw.Find(machine) is { } m ? J(Summary(m, true)) : NotFound($"machine '{machine}' not found"));

        api.MapGet("/machines/{machine}/manifest", (string machine) =>
            gw.Find(machine) is { } m ? J(m.Manifest()) : NotFound($"machine '{machine}' not found"));

        // all values: {path: {value, timestamp, correlationId?, unit?}} (runtime-values format of the data model)
        api.MapGet("/machines/{machine}/values", (string machine) =>
            gw.Find(machine) is { } m ? J(m.Values()) : NotFound($"machine '{machine}' not found"));

        // one value, or (prefix) all values below a group, e.g. values/Consumption.Electricity or values/State
        api.MapGet("/machines/{machine}/values/{**path}", (string machine, string path) =>
        {
            if (gw.Find(machine) is not { } m) return NotFound($"machine '{machine}' not found");
            path = NormalizePath(path);
            if (m.Value(path) is { } v) return J(v);
            var sub = m.Values().Where(kv => kv.Key.StartsWith(path + ".", StringComparison.Ordinal)).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (sub.Count > 0) return J(sub);
            return NotFound($"'{path}' is not supported by this machine (not in its manifest)");
        });

        api.MapGet("/machines/{machine}/events", (string machine, int? limit) =>
            gw.Find(machine) is { } m ? J(m.Events(Math.Clamp(limit ?? 50, 1, 200))) : NotFound($"machine '{machine}' not found"));

        api.MapGet("/machines/{machine}/events/{**name}", (string machine, string name, int? limit) =>
            gw.Find(machine) is { } m ? J(m.Events(Math.Clamp(limit ?? 50, 1, 200), NormalizePath(name))) : NotFound($"machine '{machine}' not found"));

        // ------------------------------------------------------------ live streams (server-sent events)

        // everything the gateway publishes (what an MQTT subscriber to '#' would see); ?machine=<id|name> filters
        api.MapGet("/stream", (HttpContext ctx, string? machine) =>
        {
            string? id = machine is null ? null : gw.Find(machine)?.MachineId;
            if (machine is not null && id is null) return NotFound($"machine '{machine}' not found");
            return Sse(ctx, hub, m => id is null || m.MachineId == id, replay: ctx.Request.Query["replay"] != "0");
        });

        api.MapGet("/machines/{machine}/stream", (HttpContext ctx, string machine) =>
        {
            if (gw.Find(machine) is not { } m) return NotFound($"machine '{machine}' not found");
            var id = m.MachineId;
            return Sse(ctx, hub, x => x.MachineId == id, replay: true);
        });

        api.MapGet("/messages", (string? machine, int? limit) =>
        {
            string? id = machine is null ? null : gw.Find(machine)?.MachineId;
            return J(hub.Recent(m => id is null || m.MachineId == id, Math.Clamp(limit ?? 100, 1, 300)).Select(MessageHub.ToWire).ToList());
        });
    }

    // ================================================================ helpers

    private static string NormalizePath(string p) => p.Trim('/').Replace('/', '.');

    private static object Status(GatewayService gw) => new
    {
        opcUa = new
        {
            state = gw.State,
            message = gw.Message,
            endpoint = gw.Options.OpcUaEndpoint,
            server = gw.ServerName,
            connectedSince = gw.ConnectedSince is { } t ? Envelope.Iso(t) : null,
            machinesFolder = gw.Options.MachinesFolder,
            machinesFolderFound = gw.MachinesFolderFound,
        },
        mqtt = new
        {
            enabled = gw.Options.MqttEnabled,
            url = gw.Options.MqttUrl,
            topicPrefix = gw.Options.TopicPrefix ?? "",
            connected = gw.Machines.Count(m => m.MqttConnected),
            clients = gw.Machines.Count(m => m.Started),
        },
        machineCount = gw.Machines.Count(m => m.Started),
        failures = gw.Failures,
    };

    private static object ConfigView(GatewayOptions o) => new
    {
        opcUaEndpoint = o.OpcUaEndpoint,
        opcUaUser = o.OpcUaUser,
        opcUaPassword = string.IsNullOrEmpty(o.OpcUaPassword) ? null : "********",
        opcUaUseSecurity = o.OpcUaUseSecurity,
        machinesFolder = o.MachinesFolder,
        mqttEnabled = o.MqttEnabled,
        mqttUrl = o.MqttUrl,
        mqttUsername = o.MqttUsername,
        mqttPassword = string.IsNullOrEmpty(o.MqttPassword) ? null : "********",
        topicPrefix = o.TopicPrefix ?? "",
        machineIdSource = o.MachineIdSource,
        heartbeatInterval = o.HeartbeatInterval,
        rescanInterval = o.RescanInterval,
        mappingFile = o.MappingFile,
    };

    private static object Summary(MachineAdapter m, bool detail)
    {
        var id = m.MachineId; // the id is already percent-encoded (same string as in the MQTT topic): usable verbatim in the URL
        var self = $"/api/machines/{id}";
        var core = new Dictionary<string, object?>
        {
            ["id"] = m.MachineId,
            ["name"] = m.Key,
            ["displayName"] = m.DisplayName,
            ["type"] = m.TypeName,
            ["nodeId"] = m.NodeId.ToString(),
            ["productInstanceUri"] = m.ProductInstanceUri(),
            ["connectionState"] = m.State,
            ["profiles"] = m.Manifest().TryGetValue("profiles", out var pr) ? pr : null,
            ["coreComplete"] = CoreElements.All(m.IsSupportedPath),
            ["missingCore"] = CoreElements.Where(p => !m.IsSupportedPath(p)).ToList(),
            ["elementCount"] = m.Elements().Count,
            ["mqtt"] = new { topicRoot = m.TopicRoot, subscribe = $"{m.TopicRoot}/#", manifest = m.ManifestTopic, connected = m.MqttConnected },
            ["links"] = new
            {
                self,
                manifest = $"{self}/manifest",
                values = $"{self}/values",
                events = $"{self}/events",
                stream = $"{self}/stream",
            },
        };
        if (detail)
        {
            core["elements"] = m.Elements();
            core["events"] = m.EventTypes();
            core["unsupportedElements"] = m.UnsupportedElements();
            core["unresolvedInputs"] = m.MissingInputs();
        }
        return core;
    }

    private static IResult Sse(HttpContext ctx, MessageHub hub, Func<GatewayMessage, bool> filter, bool replay)
        => new SseResult(hub, filter, replay);

    private sealed class SseResult : IResult
    {
        private readonly MessageHub _hub; private readonly Func<GatewayMessage, bool> _filter; private readonly bool _replay;
        public SseResult(MessageHub hub, Func<GatewayMessage, bool> filter, bool replay) { _hub = hub; _filter = filter; _replay = replay; }

        public async Task ExecuteAsync(HttpContext ctx)
        {
            var res = ctx.Response; var ct = ctx.RequestAborted;
            res.Headers.ContentType = "text/event-stream";
            res.Headers.CacheControl = "no-cache";
            res.Headers["X-Accel-Buffering"] = "no";
            using var sub = _hub.Subscribe(_filter);
            async Task Write(GatewayMessage m)
            {
                await res.WriteAsync($"data: {JsonSerializer.Serialize(MessageHub.ToWire(m), Json.Options)}\n\n", ct);
            }
            await res.WriteAsync(": connected\n\n", ct);
            if (_replay) foreach (var m in _hub.Recent(_filter, 150)) await Write(m);
            await res.Body.FlushAsync(ct);
            Task<GatewayMessage>? pending = null;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    pending ??= sub.Reader.ReadAsync(ct).AsTask();
                    var done = await Task.WhenAny(pending, Task.Delay(15000, ct));
                    if (done == pending) { var m = await pending; pending = null; await Write(m); }
                    else await res.WriteAsync(": keepalive\n\n", ct);
                    await res.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (System.Threading.Channels.ChannelClosedException) { }
        }
    }
}
