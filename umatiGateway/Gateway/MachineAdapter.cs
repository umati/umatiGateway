using System.Text.Json.Nodes;
using System.Threading.Channels;
using Opc.Ua;
using UmatiGateway.Mapping;
using UmatiGateway.Model;
using UmatiGateway.Opc;
using UmatiGateway.Output;

namespace UmatiGateway.Gateway;

/// <summary>
/// One machine found in the Machines folder: resolves the mapping inputs on the server, subscribes to
/// them, turns every change into data-model values/events and hands them out through
/// MQTT (retained values, events), the REST value store and the web page stream.
/// </summary>
public sealed class MachineAdapter : IAsyncDisposable
{
    private sealed record InputSample(object? Value, DateTime? Timestamp);
    private sealed record Out(string Topic, string Payload, bool Retain, int Qos, string Kind, string Path);

    public sealed record ElementInfo(string Path, string Topic, string RestPath, string? Unit, bool CorrelationId, string? TimestampSource, string? Note);
    public sealed record EventInfo(string Name, string Topic);

    // ---- identity
    public string Key { get; }                    // browse name below the Machines folder (unique)
    public NodeId NodeId { get; private set; }
    public string DisplayName { get; }
    public string? TypeName { get; }
    public string MachineId { get; private set; } = "";
    public string TopicRoot => _topics?.Root ?? "";
    public bool Started => _topics is not null;

    private readonly GatewayOptions _o;
    private readonly MappingDefinition _m;
    private readonly MessageHub _hub;
    private readonly ILogger _log;
    private OpcUaConnection _conn;

    private readonly object _gate = new();
    private readonly Dictionary<string, NodeId> _nodes = new();
    private readonly List<string> _missing = new();
    private readonly HashSet<string> _supported = new();
    private readonly Dictionary<string, InputSample> _samples = new();
    private readonly Dictionary<string, Envelope> _values = new();
    private readonly LinkedList<DataEvent> _events = new();
    private readonly Dictionary<string, List<MappingElement>> _dependents = new();
    private List<MappingElement> _elements = new();                       // mapping elements after expanding the instances
    private readonly Dictionary<string, Dictionary<string, string>> _instances = new(); // group -> number -> browse name
    private readonly Dictionary<string, string> _lastSent = new();
    private readonly HashSet<string> _warnedStale = new();
    private readonly ProgramExecutionTracker _tracker = new();
    private ProgramExecutionTracker.Snapshot? _lastSnapshot;
    private Dictionary<string, object?> _manifest = new();
    private TopicBuilder? _topics;
    private string _state = "Disconnected";
    private long _heartbeat;
    private readonly DateTime _startedAt = DateTime.UtcNow;

    private readonly Channel<Out> _out = Channel.CreateUnbounded<Out>(new UnboundedChannelOptions { SingleReader = true });
    private Task? _sender;
    private Task? _heartbeatTask;
    private readonly CancellationTokenSource _cts = new();
    private MqttMachineClient? _mqtt;
    private IDisposable? _sub;

    /// <summary>true after the OPC UA session was lost: the owner has to call <see cref="RebindAsync"/></summary>
    public bool NeedsRebind { get; private set; }

    public MachineAdapter(OpcChild child, string? typeName, OpcUaConnection conn, GatewayOptions o, MappingDefinition m, MessageHub hub, ILogger log)
    {
        Key = child.BrowseName; NodeId = child.NodeId; DisplayName = child.DisplayName; TypeName = typeName;
        _conn = conn; _o = o; _m = m; _hub = hub; _log = log;
    }

    // ============================================================== lifecycle

    public async Task StartAsync(ISet<string> takenIds, CancellationToken ct)
    {
        await ResolveAndReadAsync(ct);

        lock (_gate)
        {
            var raw = DeriveMachineId();
            MachineId = TopicBuilder.EncodeMachineId(raw);
            if (takenIds.Contains(MachineId))
            {
                // several machines of one server can report the same ProductInstanceUri: keep the ids unique
                var unique = TopicBuilder.EncodeMachineId($"{raw}#{Key}");
                _log.LogWarning("Machine {Key}: id {Id} is already used by another machine, using {Unique}", Key, MachineId, unique);
                MachineId = unique;
            }
            _topics = new TopicBuilder(_o.TopicPrefix, MachineId);
        }
        _log.LogInformation("Machine {Key}: id {Id}, topic root {Root}, {N} elements supported ({M} inputs missing)",
            Key, MachineId, TopicRoot, _supported.Count, _missing.Count);

        _sender = Task.Run(SenderLoop);

        if (_o.MqttEnabled)
        {
            var will = Json.Serialize(Envelope.Create("Disconnected", DateTime.UtcNow));
            _mqtt = new MqttMachineClient(_o, $"{_o.MqttClientIdPrefix}-{Guid.NewGuid():N}".Substring(0, 30), _topics!.Value("Connection.State"), will, _log);
            _mqtt.Connected += () => { lock (_gate) { if (_state == "Connected") PublishEverything(); } return Task.CompletedTask; };
            _mqtt.Start();
        }

        lock (_gate) PublishEverything();
        _sub = await _conn.SubscribeAsync(_nodes, _o.PublishingInterval, _o.SamplingInterval, OnChange, ct);
        _heartbeatTask = Task.Run(HeartbeatLoop);
    }

    /// <summary>The OPC UA session was lost: keep the last values (retained), only flag the connection.</summary>
    public void OnSourceLost()
    {
        lock (_gate)
        {
            NeedsRebind = true;
            if (_topics is null) return;
            SetState("Disconnected");
        }
        try { _sub?.Dispose(); } catch { /* session is gone */ }
        _sub = null;
    }

    /// <summary>After an OPC UA reconnect: resolve again, republish everything, then Connected.</summary>
    public async Task RebindAsync(OpcUaConnection conn, OpcChild child, CancellationToken ct)
    {
        _conn = conn; NodeId = child.NodeId;
        await ResolveAndReadAsync(ct);
        lock (_gate)
        {
            NeedsRebind = false;
            PublishEverything();
        }
        _sub = await _conn.SubscribeAsync(_nodes, _o.PublishingInterval, _o.SamplingInterval, OnChange, ct);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { _sub?.Dispose(); } catch { /* ignore */ }
        lock (_gate) { if (_topics is not null) SetState("Disconnected"); }
        _out.Writer.TryComplete();
        try { if (_sender is not null) await _sender.WaitAsync(TimeSpan.FromSeconds(3)); } catch { /* ignore */ }
        try { if (_heartbeatTask is not null) await _heartbeatTask.WaitAsync(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
        if (_mqtt is not null) await _mqtt.DisposeAsync();
    }

    // ============================================================== OPC UA side

    private async Task ResolveAndReadAsync(CancellationToken ct)
    {
        var (elements, instances) = await ExpandInstancesAsync(ct);
        var nodes = new Dictionary<string, NodeId>();
        var missing = new List<string>();
        foreach (var key in MappingDefinition.AllInputPaths(elements))
        {
            // "a/b|c/d": alternative paths, the first one that exists wins
            NodeId? n = null;
            foreach (var alt in key.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                n = await _conn.ResolvePathAsync(NodeId, alt, ct);
                if (n is not null) break;
            }
            if (n is not null) nodes[key] = n; else missing.Add(key);
        }
        var read = await _conn.ReadAsync(nodes, ct);
        lock (_gate)
        {
            _elements = elements;
            _instances.Clear(); foreach (var kv in instances) _instances[kv.Key] = kv.Value;
            _dependents.Clear();
            foreach (var e in elements)
                foreach (var p in e.Inputs?.Values ?? Enumerable.Empty<string>())
                {
                    if (!_dependents.TryGetValue(p, out var l)) _dependents[p] = l = new();
                    if (!l.Contains(e)) l.Add(e);
                }
            _nodes.Clear(); foreach (var kv in nodes) _nodes[kv.Key] = kv.Value;
            _missing.Clear(); _missing.AddRange(missing);
            _samples.Clear();
            foreach (var (k, s) in read) _samples[k] = new InputSample(s.Value, s.Timestamp);
            _supported.Clear();
            foreach (var e in elements) if (IsSupported(e)) _supported.Add(e.Path);
        }
        foreach (var e in elements.Where(e => !_supported.Contains(e.Path)))
            _log.LogDebug("Machine {Key}: {Path} not supported (inputs missing)", Key, e.Path);
    }

    /// <summary>Expands Channel.&lt;n&gt;… / Spindle.&lt;n&gt;… template elements into one element per instance found on the machine.</summary>
    private async Task<(List<MappingElement>, Dictionary<string, Dictionary<string, string>>)> ExpandInstancesAsync(CancellationToken ct)
    {
        var result = new List<MappingElement>();
        var found = new Dictionary<string, Dictionary<string, string>>();
        var cache = new Dictionary<string, List<(string Number, string Name)>>();
        foreach (var e in _m.Elements)
        {
            if (e.Instances is null) { result.Add(e); continue; }
            var key = e.Instances.Under + "|" + e.Instances.Pattern;
            if (!cache.TryGetValue(key, out var list))
            {
                list = new();
                var under = string.IsNullOrEmpty(e.Instances.Under) ? NodeId : await _conn.ResolvePathAsync(NodeId, e.Instances.Under, ct);
                if (under is not null)
                {
                    var rx = new System.Text.RegularExpressions.Regex(e.Instances.Pattern);
                    foreach (var c in await _conn.BrowseAsync(under, ct))
                        if (rx.Match(c.BrowseName) is { Success: true } mt && mt.Groups.Count > 1 && int.TryParse(mt.Groups[1].Value, out var n) && n >= 1)
                            list.Add((n.ToString(), c.BrowseName)); // no leading zeros, numbers start at 1
                }
                cache[key] = list;
            }
            foreach (var (num, name) in list.DistinctBy(x => x.Number))
            {
                var x = e.Clone();
                x.Path = e.Path.Replace("<n>", num);
                x.Inputs = e.Inputs?.ToDictionary(kv => kv.Key, kv => kv.Value.Replace("<n>", num));
                x.Instances = null;
                result.Add(x);
                if (!found.TryGetValue(e.Instances.Group, out var g)) found[e.Instances.Group] = g = new();
                g[num] = name;
            }
        }
        return (result, found);
    }

    private bool IsSupported(MappingElement e)
    {
        if (e.Special is "heartbeat" or "connectionState") return true;
        if (e.Constant is not null) return true;
        if (e.Inputs is null || e.Inputs.Count == 0) return false;
        var required = e.Inputs.Where(kv => !(e.OptionalInputs?.Contains(kv.Key) ?? false)).ToList();
        return required.Count > 0 ? required.All(kv => _nodes.ContainsKey(kv.Value)) : e.Inputs.Values.Any(_nodes.ContainsKey);
    }

    private void OnChange(string key, Sample s)
    {
        lock (_gate)
        {
            var ts = s.Timestamp;
            if (_samples.TryGetValue(key, out var prev) && prev.Timestamp is { } pt && (ts is null || ts <= pt)
                && Json.Serialize(prev.Value) != Json.Serialize(s.Value))
            {
                // some servers do not advance the SourceTimestamp with value changes: a changed value must not carry an older timestamp
                if (_warnedStale.Add(key)) _log.LogWarning("Machine {Key}: SourceTimestamp of {Input} does not advance with value changes; using receive time", Key, key);
                ts = DateTime.UtcNow;
            }
            _samples[key] = new InputSample(s.Value, ts);
            if (_state != "Connected" || _topics is null) return;
            if (!_dependents.TryGetValue(key, out var deps)) return;
            foreach (var e in deps)
            {
                try { EmitElement(e); }
                catch (Exception ex) { _log.LogWarning("Machine {Key}: publishing {Path} failed: {Msg}", Key, e.Path, ex.Message); }
            }
        }
    }

    // ============================================================== data-model side (call with _gate held)

    private MappingElement? El(string path) => _elements.FirstOrDefault(e => e.Path == path);

    private (object? Value, DateTime? Timestamp) Evaluate(MappingElement e)
    {
        var inputs = new Dictionary<string, object?>();
        DateTime? ts = null;
        foreach (var (name, path) in e.Inputs ?? new())
        {
            _samples.TryGetValue(path, out var s);
            inputs[name] = s?.Value;
            if (s?.Timestamp is { } t && (ts is null || t > ts)) ts = t;
        }
        return (Transforms.Apply(e.Transform!, inputs, e.Params), ts);
    }

    private string DeriveMachineId()
    {
        string? raw = null;
        switch (_o.MachineIdSource)
        {
            case "BrowseName": raw = Key; break;
            case "SerialNumber": raw = El("Identification.SerialNumber") is { } s && _supported.Contains(s.Path) ? Evaluate(s).Value as string : null; break;
            default: raw = El("Identification.ProductInstanceUri") is { } p && _supported.Contains(p.Path) ? Evaluate(p).Value as string : null; break;
        }
        if (string.IsNullOrEmpty(raw))
        {
            _log.LogWarning("Machine {Key}: no {Src} available, using the browse name as machine id", Key, _o.MachineIdSource);
            raw = Key;
        }
        return raw;
    }

    private DateTime TsFor(MappingElement e, DateTime? src) => e.TimestampSource == "adapter" ? DateTime.UtcNow : src ?? _startedAt;

    private void Enqueue(string topic, object payload, bool retain, int qos, string kind, string path, bool dedupe)
    {
        var body = Json.Serialize(payload);
        if (dedupe)
        {
            if (_lastSent.TryGetValue(topic, out var last) && last == body) return;
            _lastSent[topic] = body;
        }
        _out.Writer.TryWrite(new Out(topic, body, retain, qos, kind, path));
    }

    private void SetValue(string path, Envelope env, bool retain = true, int qos = 1, string kind = "value", bool dedupe = true)
    {
        _values[path] = env;
        Enqueue(_topics!.Value(path), env, retain, qos, kind, path, dedupe);
    }

    private void SetState(string state, bool force = false)
    {
        if (!force && _state == state) return;
        _state = state;
        SetValue("Connection.State", Envelope.Create(state, DateTime.UtcNow), kind: "state", dedupe: false);
    }

    /// <summary>Manifest, every value, then Connected. Used at start, after OPC UA reconnect and after MQTT reconnect.</summary>
    private void PublishEverything()
    {
        _lastSent.Clear();
        PublishManifest();
        foreach (var e in _elements) EmitElement(e, resync: true);
        SetState("Connected", force: true);
    }

    private static string ProfileOf(string path) =>
        path.StartsWith("Consumption.") ? "Energy"
        : path.StartsWith("Production.") ? "Production"
        : path.StartsWith("Channel.") ? "Channel"
        : path.StartsWith("Spindle.") ? "Spindle"
        : path.StartsWith("Stacklight.") ? "Stacklight"
        : path.StartsWith("Identification.") && path is not ("Identification.Manufacturer" or "Identification.SerialNumber" or "Identification.ProductInstanceUri") ? "IdentificationExtended"
        : path is "OperationCounters.OperationDuration" ? "MonitoringExtended"
        : "Core";

    private void PublishManifest()
    {
        var piu = El("Identification.ProductInstanceUri") is { } p && _supported.Contains(p.Path) ? Evaluate(p).Value as string : null;
        var elements = _elements.Where(e => _supported.Contains(e.Path)).ToList();
        var profiles = _m.Profiles.Where(pr => pr == "Core" || elements.Any(e => ProfileOf(e.Path) == pr)).ToList();
        _manifest = new Dictionary<string, object?>
        {
            ["modelVersion"] = _m.ModelVersion,
            ["machine"] = new Dictionary<string, object?> { ["ProductInstanceUri"] = string.IsNullOrEmpty(piu) ? $"urn:umatigateway:{MachineId}" : piu },
            ["profiles"] = profiles,
            ["heartbeatInterval"] = _o.HeartbeatInterval,
            ["elements"] = elements.Select(e =>
            {
                var d = new Dictionary<string, object?> { ["path"] = e.Path };
                if (e.TimestampSource is not null) d["timestampSource"] = e.TimestampSource;
                if (e.CorrelationId) d["correlationId"] = true;
                return d;
            }).ToList(),
            ["events"] = elements.SelectMany(e => e.Events ?? new()).ToList(),
        };
        if (_instances.Count > 0)
            _manifest["instances"] = _instances.ToDictionary(g => g.Key, g => (object)g.Value.OrderBy(kv => int.Parse(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
        Enqueue(_topics!.Manifest, _manifest, true, 1, "manifest", "_manifest", dedupe: true);
    }

    /// <summary>Publishes one element's current value (retained).</summary>
    private void EmitElement(MappingElement e, bool resync = false)
    {
        if (!_supported.Contains(e.Path) || e.Special is "heartbeat" or "connectionState") return;
        if (e.Special == "programExecution") { EmitProgramExecution(e, initial: resync); return; }

        object? value; DateTime ts;
        if (e.Constant is not null)
        {
            value = ToPlain(e.Constant); ts = _startedAt;
        }
        else
        {
            var r = Evaluate(e);
            value = r.Value; ts = TsFor(e, r.Timestamp);
        }
        var corr = e.CorrelationId ? _tracker.CurrentExecutionId : null;
        SetValue(e.Path, Envelope.Create(value, ts, corr, e.Unit));
    }

    private static object? ToPlain(JsonNode? n) => n switch
    {
        null => null,
        JsonArray a => a.Select(ToPlain).ToList(),
        JsonObject o => o.ToDictionary(kv => kv.Key, kv => ToPlain(kv.Value)),
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when v.TryGetValue<long>(out var l) => l,
        JsonValue v when v.TryGetValue<double>(out var d) => d,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        _ => n.ToJsonString(),
    };

    /// <summary>Runs the ProgramExecution tracker on the current inputs and publishes snapshot + events.</summary>
    private void EmitProgramExecution(MappingElement e, bool initial)
    {
        var inp = e.Inputs ?? new();
        object? Sample(string name) => inp.TryGetValue(name, out var path) && _samples.TryGetValue(path, out var s) ? s.Value : null;
        var stateNum = Transforms.ToNumber(Sample("state"));
        var stateMap = e.Params?["stateMap"]?.AsObject();
        string? state = null;
        if (stateNum is { } sn && stateMap is not null && stateMap.TryGetPropertyValue(((long)sn).ToString(), out var sv)) state = sv?.GetValue<string>();
        var program = Transforms.ToText(Sample("program"));
        var stateTs = inp.TryGetValue("state", out var sp) && _samples.TryGetValue(sp, out var ss) ? ss.Timestamp : null;

        var res = _tracker.Update(
            new ProgramExecutionTracker.Input(state, program, null,
                new ProgramExecutionTracker.Counters(Transforms.ToNumber(Sample("electricity")), Transforms.ToNumber(Sample("compressedAir")))),
            initial ? DateTime.UtcNow : stateTs ?? DateTime.UtcNow);

        if (res.Snapshot is not null) _lastSnapshot = res.Snapshot;
        if (res.Snapshot is not null || initial || !_values.ContainsKey(e.Path))
        {
            var snap = _lastSnapshot ?? new ProgramExecutionTracker.Snapshot(null, DateTime.UtcNow, null);
            SetValue(e.Path, Envelope.Create(snap.Value, snap.Timestamp, snap.CorrelationId));
        }
        foreach (var ev in res.Events)
        {
            _log.LogInformation("Machine {Key}: event {Event} {Value}", Key, ev.Event, Json.Serialize(ev.Value));
            _events.AddLast(ev);
            while (_events.Count > 200) _events.RemoveFirst();
            Enqueue(_topics!.Event(ev.Event), ev, false, 1, "event", ev.Event, dedupe: false);
        }
        if (res.Snapshot is not null)
        {
            // elements carrying the ExecutionId as correlationId follow the tracker
            foreach (var o in _elements)
                if (o.CorrelationId && o.Special != "programExecution") EmitElement(o);
        }
    }

    // ============================================================== background loops

    private async Task SenderLoop()
    {
        await foreach (var o in _out.Reader.ReadAllAsync())
        {
            try
            {
                _hub.Publish(new GatewayMessage(DateTime.UtcNow, MachineId, o.Kind, o.Path, o.Topic, o.Payload, o.Retain, o.Qos));
                if (_mqtt is not null) await _mqtt.PublishAsync(o.Topic, o.Payload, o.Retain, o.Qos);
            }
            catch (Exception ex) { _log.LogDebug("send failed: {Msg}", ex.Message); }
        }
    }

    private async Task HeartbeatLoop()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_o.HeartbeatInterval));
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                lock (_gate)
                {
                    if (_state != "Connected") continue;
                    _heartbeat = (_heartbeat + 1) % 2_147_483_647;
                    SetValue("Connection.Heartbeat", Envelope.Create(_heartbeat, DateTime.UtcNow), retain: _o.HeartbeatRetain, qos: 0, kind: "heartbeat", dedupe: false);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    // ============================================================== read access for the REST API

    public string State { get { lock (_gate) return _state; } }
    public IReadOnlyDictionary<string, Envelope> Values() { lock (_gate) return new Dictionary<string, Envelope>(_values); }
    public Envelope? Value(string path) { lock (_gate) return _values.TryGetValue(path, out var v) ? v : null; }
    public bool IsSupportedPath(string path) { lock (_gate) return _supported.Contains(path); }
    public IReadOnlyDictionary<string, object?> Manifest() { lock (_gate) return new Dictionary<string, object?>(_manifest); }
    public IReadOnlyList<DataEvent> Events(int limit = 50, string? name = null)
    {
        lock (_gate)
            return _events.Where(ev => name is null || ev.Event == name).Reverse().Take(limit).ToList();
    }
    public IReadOnlyList<string> MissingInputs() { lock (_gate) return _missing.ToList(); }

    public IReadOnlyList<string> UnsupportedElements()
    {
        lock (_gate) return _elements.Where(e => !_supported.Contains(e.Path)).Select(e => e.Path).ToList();
    }

    public string? ProductInstanceUri()
    {
        lock (_gate) return _manifest.TryGetValue("machine", out var m) && m is Dictionary<string, object?> d ? d["ProductInstanceUri"] as string : null;
    }

    public IReadOnlyList<ElementInfo> Elements()
    {
        lock (_gate)
            return _elements.Where(e => _supported.Contains(e.Path) && _topics is not null).Select(e => new ElementInfo(
                e.Path, _topics!.Value(e.Path), $"/api/machines/{MachineId}/values/{e.Path}",
                e.Unit, e.CorrelationId, e.TimestampSource, e.Note)).ToList();
    }

    public IReadOnlyList<EventInfo> EventTypes()
    {
        lock (_gate)
            return _elements.Where(e => _supported.Contains(e.Path) && _topics is not null).SelectMany(e => e.Events ?? new())
                .Select(n => new EventInfo(n, _topics!.Event(n))).ToList();
    }

    public bool MqttConnected => _mqtt?.IsConnected ?? false;
    public string? MqttError => _mqtt?.LastError;
    public string ManifestTopic => _topics?.Manifest ?? "";
}
