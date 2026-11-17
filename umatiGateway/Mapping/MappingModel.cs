using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UmatiGateway.Mapping;

/// <summary>One data-model element and how it is derived from OPC UA nodes of a machine instance.</summary>
public sealed class MappingElement
{
    public string Path { get; set; } = "";

    /// <summary>input name -> browse path relative to the machine instance ('/'-separated browse names)</summary>
    public Dictionary<string, string>? Inputs { get; set; }

    /// <summary>inputs that may be missing on the server without making the element unsupported</summary>
    public List<string>? OptionalInputs { get; set; }

    public string? Transform { get; set; }
    public JsonObject? Params { get; set; }

    /// <summary>unit name from the catalog (UNECE name, e.g. "second"); sent with every number</summary>
    public string? Unit { get; set; }

    /// <summary>"source" (OPC UA SourceTimestamp, default) or "adapter" (gateway time)</summary>
    public string? TimestampSource { get; set; }

    public bool CorrelationId { get; set; }

    /// <summary>fixed value, published once</summary>
    public JsonNode? Constant { get; set; }

    /// <summary>heartbeat | connectionState | programExecution</summary>
    public string? Special { get; set; }

    public List<string>? Events { get; set; }
    public string? Note { get; set; }

    /// <summary>
    /// Multi-instance elements (Channel.&lt;n&gt;.…, Spindle.&lt;n&gt;.…): the element is expanded once per child of
    /// <see cref="MappingInstances.Under"/> whose browse name matches the pattern; "&lt;n&gt;" in the path and in the
    /// inputs is replaced by the number captured by the pattern.
    /// </summary>
    public MappingInstances? Instances { get; set; }

    public MappingElement Clone() => (MappingElement)MemberwiseClone();

    [JsonIgnore]
    public bool IsSpecial => !string.IsNullOrEmpty(Special);
}

public sealed class MappingInstances
{
    /// <summary>data-model group: "Channel" or "Spindle" (key of manifest.instances)</summary>
    public string Group { get; set; } = "";
    /// <summary>browse path below the machine whose children are the instances, e.g. "Monitoring"</summary>
    public string Under { get; set; } = "";
    /// <summary>regex over the browse name; group 1 = instance number</summary>
    public string Pattern { get; set; } = "";
}

public sealed class MappingDefinition
{
    public string? Description { get; set; }
    public string ModelVersion { get; set; } = "";
    public List<string> Profiles { get; set; } = new();
    public List<MappingElement> Elements { get; set; } = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static MappingDefinition Load(string file)
    {
        var m = JsonSerializer.Deserialize<MappingDefinition>(File.ReadAllText(file), Json)
                ?? throw new InvalidDataException($"mapping {file}: empty");
        m.Validate();
        return m;
    }

    public void Validate()
    {
        if (string.IsNullOrEmpty(ModelVersion)) throw new InvalidDataException("mapping: modelVersion missing");
        if (!Profiles.Contains("Core")) throw new InvalidDataException("mapping: profiles must contain Core");
        var seen = new HashSet<string>();
        foreach (var e in Elements)
        {
            if (string.IsNullOrEmpty(e.Path)) throw new InvalidDataException("mapping: element without path");
            if (!seen.Add(e.Path)) throw new InvalidDataException($"mapping: duplicate path {e.Path}");
            if (!e.IsSpecial && e.Constant is null && e.Transform is null)
                throw new InvalidDataException($"mapping: {e.Path} needs transform, constant or special");
            if (e.Transform is not null && !Transforms.Known(e.Transform))
                throw new InvalidDataException($"mapping: {e.Path}: unknown transform {e.Transform}");
            if (e.Transform is not null && (e.Inputs is null || e.Inputs.Count == 0))
                throw new InvalidDataException($"mapping: {e.Path}: transform requires inputs");
            if (e.Instances is not null && (!e.Path.Contains("<n>") || string.IsNullOrEmpty(e.Instances.Pattern) || string.IsNullOrEmpty(e.Instances.Group)))
                throw new InvalidDataException($"mapping: {e.Path}: instances need a path with <n>, a group and a pattern");
            if (e.Special is not null && e.Special is not ("heartbeat" or "connectionState" or "programExecution"))
                throw new InvalidDataException($"mapping: {e.Path}: unknown special {e.Special}");
        }
    }

    public static IEnumerable<string> AllInputPaths(IEnumerable<MappingElement> elements) =>
        elements.SelectMany(e => e.Inputs?.Values ?? Enumerable.Empty<string>()).Distinct();
}
