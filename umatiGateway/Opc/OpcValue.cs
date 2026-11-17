using System.Globalization;
using Opc.Ua;
using Opc.Ua.Client.ComplexTypes;

namespace UmatiGateway.Opc;

/// <summary>A converted sample of an OPC UA variable.</summary>
public sealed record Sample(object? Value, DateTime? Timestamp, bool Good);

/// <summary>Converts OPC UA values into plain .NET values (null, string, long, double, bool, Dictionary, List).</summary>
public static class OpcValue
{
    public static Sample FromDataValue(DataValue dv)
    {
        var ts = dv.SourceTimestamp != DateTime.MinValue ? dv.SourceTimestamp
               : dv.ServerTimestamp != DateTime.MinValue ? dv.ServerTimestamp : (DateTime?)null;
        if (StatusCode.IsNotGood(dv.StatusCode) || dv.Value is null) return new Sample(null, ts, false);
        return new Sample(Convert(dv.Value), ts, true);
    }

    public static object? Convert(object? v)
    {
        switch (v)
        {
            case null: return null;
            case Variant vv: return Convert(vv.Value);
            case bool or string: return v;
            case sbyte or byte or short or ushort or int or uint: return System.Convert.ToInt64(v, CultureInfo.InvariantCulture);
            case long l: return l;
            case ulong ul: return ul <= long.MaxValue ? (long)ul : (double)ul;
            case float f: return float.IsFinite(f) ? double.Parse(f.ToString("G7", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) : null; // 1.438f -> 1.438
            case double d: return double.IsFinite(d) ? d : null;
            case DateTime dt: return dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            case LocalizedText lt: return lt.Text;
            case QualifiedName qn: return qn.Name;
            case NodeId n: return n.ToString();
            case ExpandedNodeId en: return en.ToString();
            case Uuid u: return u.ToString();
            case StatusCode sc: return (long)sc.Code;
            case Enum e: return System.Convert.ToInt64(e, CultureInfo.InvariantCulture);
            case ExtensionObject eo: return Convert(eo.Body);
            case byte[] bytes: return System.Convert.ToBase64String(bytes);
            case BaseComplexType ct:
                {
                    var d = new Dictionary<string, object?>();
                    foreach (var p in ct.GetPropertyEnumerator()) d[p.Name] = Convert(ct[p.Name]);
                    return d;
                }
            case Array arr:
                return arr.Cast<object?>().Select(Convert).ToList();
            case System.Collections.IEnumerable en when v is not string:
                return en.Cast<object?>().Select(Convert).ToList();
            case IEncodeable enc:
                {
                    var d = new Dictionary<string, object?>();
                    foreach (var p in enc.GetType().GetProperties())
                    {
                        if (p.GetIndexParameters().Length > 0 || !p.CanRead) continue;
                        if (p.Name is "TypeId" or "BinaryEncodingId" or "XmlEncodingId" or "JsonEncodingId") continue;
                        try { d[p.Name] = Convert(p.GetValue(enc)); } catch { /* skip */ }
                    }
                    return d;
                }
            default: return v.ToString();
        }
    }
}
