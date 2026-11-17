using System.Text;
using MQTTnet;
using MQTTnet.Protocol;

namespace UmatiGateway.Output;

/// <summary>
/// MQTT connection of one machine. One connection per machine so that every machine gets its own
/// Last Will (Connection.State = Disconnected). Reconnects automatically and raises
/// <see cref="Connected"/> after every (re)connect so that the machine republishes all values.
/// </summary>
public sealed class MqttMachineClient : IAsyncDisposable
{
    private readonly GatewayOptions _o;
    private readonly string _clientId;
    private readonly string _willTopic;
    private readonly string _willPayload;
    private readonly ILogger _log;
    private readonly IMqttClient _client;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private int _everConnected;

    public event Func<Task>? Connected;
    public bool IsConnected => _client.IsConnected;
    public string? LastError { get; private set; }

    public MqttMachineClient(GatewayOptions o, string clientId, string willTopic, string willPayload, ILogger log)
    {
        _o = o; _clientId = clientId; _willTopic = willTopic; _willPayload = willPayload; _log = log;
        _client = new MqttClientFactory().CreateMqttClient();
    }

    public void Start() => _loop = Task.Run(Loop);

    private MqttClientOptions BuildOptions()
    {
        var uri = new Uri(_o.MqttUrl.Contains("://") ? _o.MqttUrl : "mqtt://" + _o.MqttUrl);
        var b = new MqttClientOptionsBuilder()
            .WithClientId(_clientId)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311) // works with every broker
            .WithCleanSession(true)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
            .WithWillTopic(_willTopic)
            .WithWillPayload(Encoding.UTF8.GetBytes(_willPayload))
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithWillRetain(true);
        switch (uri.Scheme.ToLowerInvariant())
        {
            case "ws":
            case "wss":
                b.WithWebSocketServer(x => x.WithUri(uri.ToString()));
                if (uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase)) b.WithTlsOptions(t => t.UseTls());
                break;
            case "mqtts":
            case "ssl":
                b.WithTcpServer(uri.Host, uri.IsDefaultPort ? 8883 : uri.Port).WithTlsOptions(t => t.UseTls());
                break;
            default:
                b.WithTcpServer(uri.Host, uri.IsDefaultPort ? 1883 : uri.Port);
                break;
        }
        if (!string.IsNullOrEmpty(_o.MqttUsername)) b.WithCredentials(_o.MqttUsername, _o.MqttPassword ?? "");
        return b.Build();
    }

    private async Task Loop()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            if (!_client.IsConnected)
            {
                try
                {
                    await _client.ConnectAsync(BuildOptions(), ct);
                    LastError = null;
                    var again = Interlocked.Exchange(ref _everConnected, 1) == 1;
                    _log.LogInformation("MQTT {What} {Url} (client {Id})", again ? "reconnected to" : "connected to", _o.MqttUrl, _clientId);
                    if (Connected is { } h) await h.Invoke();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    if (LastError != ex.Message) _log.LogWarning("MQTT connect to {Url} failed: {Msg} (retrying)", _o.MqttUrl, ex.Message);
                    LastError = ex.Message;
                }
            }
            try { await Task.Delay(2000, ct); } catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Best effort: returns false when the broker is not reachable (retained state is republished on reconnect).</summary>
    public async Task<bool> PublishAsync(string topic, string payload, bool retain, int qos)
    {
        if (!_client.IsConnected) return false;
        try
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic).WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithRetainFlag(retain)
                .WithQualityOfServiceLevel(qos == 0 ? MqttQualityOfServiceLevel.AtMostOnce : MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await _client.PublishAsync(msg, _cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogDebug("MQTT publish to {Topic} failed: {Msg}", topic, ex.Message);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { if (_loop is not null) await _loop; } catch { /* ignore */ }
        try { if (_client.IsConnected) await _client.DisconnectAsync(); } catch { /* ignore */ }
        _client.Dispose();
    }
}
