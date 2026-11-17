using System.Text.Json;
using System.Threading.Channels;

namespace UmatiGateway.Output;

/// <summary>One message as it is published to MQTT (and mirrored to the web page).</summary>
public sealed record GatewayMessage(
    DateTime Time, string MachineId, string Kind, string Path, string Topic, string Payload, bool Retain, int Qos);

/// <summary>
/// Fan-out of everything the gateway publishes: keeps a short history and feeds live subscribers
/// (server-sent events). Independent of whether an MQTT broker is reachable.
/// </summary>
public sealed class MessageHub
{
    private const int HistorySize = 300;
    private readonly object _gate = new();
    private readonly LinkedList<GatewayMessage> _history = new();
    private readonly List<Subscriber> _subs = new();

    public void Publish(GatewayMessage m)
    {
        Subscriber[] subs;
        lock (_gate)
        {
            _history.AddLast(m);
            while (_history.Count > HistorySize) _history.RemoveFirst();
            subs = _subs.ToArray();
        }
        foreach (var s in subs)
            if (s.Filter(m)) s.Channel.Writer.TryWrite(m);
    }

    public IReadOnlyList<GatewayMessage> Recent(Func<GatewayMessage, bool>? filter = null, int limit = 100)
    {
        lock (_gate)
            return _history.Where(m => filter?.Invoke(m) ?? true).TakeLast(limit).ToList();
    }

    public Subscription Subscribe(Func<GatewayMessage, bool> filter)
    {
        var s = new Subscriber(filter, Channel.CreateBounded<GatewayMessage>(new BoundedChannelOptions(1000)
        { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true }));
        lock (_gate) _subs.Add(s);
        return new Subscription(this, s);
    }

    public sealed record Subscriber(Func<GatewayMessage, bool> Filter, Channel<GatewayMessage> Channel);

    public sealed class Subscription : IDisposable
    {
        private readonly MessageHub _hub; private readonly Subscriber _s;
        internal Subscription(MessageHub hub, Subscriber s) { _hub = hub; _s = s; }
        public ChannelReader<GatewayMessage> Reader => _s.Channel.Reader;
        public void Dispose() { lock (_hub._gate) _hub._subs.Remove(_s); _s.Channel.Writer.TryComplete(); }
    }

    /// <summary>JSON line used by the SSE streams: payload is embedded as JSON, not as a string.</summary>
    public static object ToWire(GatewayMessage m)
    {
        object? payload;
        try { payload = JsonSerializer.Deserialize<JsonElement>(m.Payload); } catch { payload = m.Payload; }
        return new { time = Model.Envelope.Iso(m.Time), machine = m.MachineId, kind = m.Kind, path = m.Path, topic = m.Topic, retain = m.Retain, qos = m.Qos, payload };
    }
}
