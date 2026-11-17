using System.Globalization;
using System.Text.Json.Nodes;

namespace UmatiGateway.Mapping;

/// <summary>
/// Pure value transforms: OPC UA values -> data-model values. A transform gets the already
/// converted input values (null, string, long, double, bool, Dictionary, List) and returns the
/// data-model value, or null when the value is unavailable.
/// </summary>
public static class Transforms
{
    private static readonly HashSet<string> Names = new() { "text", "boolean", "integer", "number", "scale", "structSum", "enumObject", "enumMap" };
    public static bool Known(string name) => Names.Contains(name);

    /// <summary>Removes floating point noise (293.15 - 273.15 = 20.000000000000...).</summary>
    public static double Clean(double n) => double.Parse(n.ToString("G12", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public static double? ToNumber(object? v)
    {
        switch (v)
        {
            case null: return null;
            case bool b: return b ? 1 : 0;
            case string s:
                if (string.IsNullOrWhiteSpace(s)) return null;
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d : null;
            case double d2: return double.IsFinite(d2) ? d2 : null;
            case float f: return float.IsFinite(f) ? f : null;
            case IConvertible c when v is not string:
                try { return c.ToDouble(CultureInfo.InvariantCulture); } catch { return null; }
            default: return null;
        }
    }

    /// <summary>string / LocalizedText / QualifiedName (already converted) -> string</summary>
    public static string? ToText(object? v) => v switch
    {
        null => null,
        string s => s,
        IDictionary<string, object?> o when o.TryGetValue("text", out var t) && t is string ts => ts,
        IDictionary<string, object?> o when o.TryGetValue("name", out var n) && n is string ns => ns,
        _ => Convert.ToString(v, CultureInfo.InvariantCulture),
    };

    private static object? Single(IReadOnlyDictionary<string, object?> inputs) => inputs.Count == 0 ? null : inputs.Values.First();

    public static object? Apply(string name, IReadOnlyDictionary<string, object?> inputs, JsonObject? p)
    {
        switch (name)
        {
            case "text":
                {
                    var t = ToText(Single(inputs));
                    return string.IsNullOrEmpty(t) ? null : t;
                }
            case "boolean":
                {
                    var v = Single(inputs);
                    return v switch
                    {
                        bool b => b,
                        string s when bool.TryParse(s, out var pb) => pb,
                        _ => ToNumber(v) is { } n ? n != 0 : (bool?)null,
                    };
                }
            case "integer":
                {
                    var n = ToNumber(Single(inputs));
                    return n is null ? null : (long)Math.Round(n.Value, MidpointRounding.AwayFromZero);
                }
            case "number":
                {
                    var n = ToNumber(Single(inputs));
                    return n is null ? null : Clean(n.Value);
                }
            case "scale":
                {
                    var n = ToNumber(Single(inputs));
                    if (n is null) return null;
                    var factor = p?["factor"]?.GetValue<double>() ?? 1;
                    var offset = p?["offset"]?.GetValue<double>() ?? 0;
                    return Clean(n.Value * factor + offset);
                }
            case "structSum":
                {
                    var fields = p?["fields"] is JsonArray fa ? fa.Select(x => x!.GetValue<string>()).ToList() : null;
                    var parts = new List<double>();
                    foreach (var v in inputs.Values)
                    {
                        if (v is IDictionary<string, object?> o)
                        {
                            foreach (var f in fields ?? o.Keys.ToList())
                                if (o.TryGetValue(f, out var fv) && ToNumber(fv) is { } n) parts.Add(n);
                        }
                        else if (v is IEnumerable<object?> list)
                        {
                            foreach (var x in list) if (ToNumber(x) is { } n) parts.Add(n);
                        }
                        else if (ToNumber(v) is { } n) parts.Add(n);
                    }
                    return parts.Count == 0 ? null : Clean(parts.Sum());
                }
            case "enumObject":
                {
                    var res = new Dictionary<string, object?>();
                    var fields = p?["fields"]?.AsObject() ?? throw new InvalidDataException("enumObject needs params.fields");
                    foreach (var (member, spec) in fields)
                    {
                        var input = spec!["input"]!.GetValue<string>();
                        inputs.TryGetValue(input, out var raw);
                        var n = ToNumber(raw);
                        if (n is null) return null;
                        var key = ((long)n.Value).ToString(CultureInfo.InvariantCulture);
                        var map = spec["map"]!.AsObject();
                        if (!map.TryGetPropertyValue(key, out var nm) || nm is null) return null;
                        res[member] = nm.GetValue<string>();
                    }
                    return res;
                }
            case "enumMap":
                {
                    var n = ToNumber(Single(inputs));
                    if (n is null) return null;
                    var map = p?["map"]?.AsObject();
                    return map is not null && map.TryGetPropertyValue(((long)n.Value).ToString(CultureInfo.InvariantCulture), out var nm) ? nm?.GetValue<string>() : null;
                }
            default:
                throw new InvalidDataException($"unknown transform {name}");
        }
    }
}
