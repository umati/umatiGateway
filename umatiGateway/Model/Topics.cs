namespace UmatiGateway.Model;

/// <summary>Data-model path -> MQTT topic (spec §8): '.' becomes '/', optional prefix and machine id.</summary>
public sealed class TopicBuilder
{
    private readonly string _base;
    public string MachineId { get; }

    public TopicBuilder(string? prefix, string machineId)
    {
        MachineId = machineId;
        prefix = prefix?.Trim('/');
        _base = string.IsNullOrEmpty(prefix) ? machineId : $"{prefix}/{machineId}";
    }

    public string Root => _base;
    public string Value(string path) => $"{_base}/{PathToSuffix(path)}";
    public string Event(string path) => Value(path);
    public string Manifest => $"{_base}/_manifest";

    public static string PathToSuffix(string path) => path.Replace('.', '/');

    /// <summary>ProductInstanceUri contains '/' and ':' -> percent-encode everything that is not unreserved (spec §8).</summary>
    public static string EncodeMachineId(string raw)
    {
        var s = Uri.EscapeDataString(raw);
        return s.Replace("!", "%21").Replace("'", "%27").Replace("(", "%28").Replace(")", "%29").Replace("*", "%2A");
    }
}
