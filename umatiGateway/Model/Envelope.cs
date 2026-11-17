using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UmatiGateway.Model;

/// <summary>Value envelope of the data model: {value, timestamp, correlationId?, unit?}.</summary>
public sealed class Envelope
{
    [JsonPropertyName("value")] [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public object? Value { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("correlationId")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CorrelationId { get; set; }

    /// <summary>MQTT/REST binding: every number carries the catalog unit name</summary>
    [JsonPropertyName("unit")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unit { get; set; }

    public static Envelope Create(object? value, DateTime timestamp, string? correlationId = null, string? unit = null) => new()
    {
        Value = value,
        Timestamp = Iso(timestamp),
        CorrelationId = string.IsNullOrEmpty(correlationId) ? null : correlationId,
        Unit = unit is not null && value is long or double ? unit : null,
    };

    public static string Iso(DateTime t) =>
        t.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}

/// <summary>Event of the data model: {event, timestamp, value}. Not retained.</summary>
public sealed class DataEvent
{
    [JsonPropertyName("event")] public string Event { get; set; } = "";
    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
    [JsonPropertyName("value")] public Dictionary<string, object?> Value { get; set; } = new();
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = null, // keep data-model paths / field names untouched
        WriteIndented = false,
    };

    public static readonly JsonSerializerOptions Indented = new(Options) { WriteIndented = true };

    public static string Serialize(object? o) => JsonSerializer.Serialize(o, Options);
}
