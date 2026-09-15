// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using Opc.Ua.Client.ComplexTypes;

namespace umatiGateway.Core.Mqtt
{
    /// <summary>Converts SDK-decoded structures using the gateway's JSON representation.</summary>
    public sealed class StructuredValueConverter
    {
        private readonly JSONConverter scalarConverter;
        private readonly Func<ExtensionObject, JObject> decodeBinary;

        public StructuredValueConverter(JSONConverter scalarConverter, Func<ExtensionObject, JObject> decodeBinary)
        {
            this.scalarConverter = scalarConverter;
            this.decodeBinary = decodeBinary;
        }

        public JToken Convert(object? value) => Convert(value, 0);

        private JToken Convert(object? value, int depth)
        {
            if (depth > 100) throw new InvalidOperationException("Structured value nesting exceeds 100 levels.");
            switch (value)
            {
                case null: return scalarConverter.GetDefaultNullValue();
                case Variant variant: return Convert(variant.Value, depth + 1);
                case ExtensionObject extension:
                    return extension.Body is IEncodeable
                        ? Convert(extension.Body, depth + 1)
                        : extension.Body == null ? scalarConverter.GetDefaultNullValue() : decodeBinary(extension);
                case BaseComplexType structure:
                    var result = new JObject();
                    foreach (var field in structure.GetPropertyEnumerator())
                    {
                        if (structure is OptionalFieldsComplexType optional && field.IsOptional &&
                            (optional.EncodingMask & field.OptionalFieldMask) == 0) continue;
                        // The SDK indexer honours the selector for union fields.
                        var fieldValue = structure[field.Name];
                        if (structure is UnionComplexType && fieldValue == null) continue;
                        result.Add(field.Name, Convert(fieldValue, depth + 1));
                    }
                    return result;
                case Argument argument: return scalarConverter.Convert(argument);
                case EUInformation units: return scalarConverter.Convert(units);
                case Opc.Ua.Range range: return scalarConverter.Convert(range);
                case EnumValueType enumeration: return scalarConverter.Convert(enumeration);
                case IEncodeable encodeable:
                    // SDK built-in structures (e.g. RelativePath) expose their wire fields as DataMembers.
                    var fields = encodeable.GetType().GetProperties()
                        .Where(property => property.GetCustomAttribute<DataMemberAttribute>() != null)
                        .OrderBy(property => property.GetCustomAttribute<DataMemberAttribute>()!.Order).ToArray();
                    if (fields.Length == 0)
                        throw new NotSupportedException($"No data fields for encodeable type {encodeable.GetType()}.");
                    var encoded = new JObject();
                    foreach (var field in fields) encoded.Add(field.Name, Convert(field.GetValue(encodeable), depth + 1));
                    return encoded;
                case byte[] bytes: return scalarConverter.Convert(bytes);
                case string text: return scalarConverter.Convert(text);
                case bool scalar: return scalarConverter.Convert(scalar);
                case byte scalar: return scalarConverter.Convert(scalar);
                case sbyte scalar: return scalarConverter.Convert(scalar);
                case short scalar: return scalarConverter.Convert(scalar);
                case ushort scalar: return scalarConverter.Convert(scalar);
                case int scalar: return scalarConverter.Convert(scalar);
                case uint scalar: return scalarConverter.Convert(scalar);
                case long scalar: return scalarConverter.Convert(scalar);
                case ulong scalar: return scalarConverter.Convert(scalar);
                case float scalar: return scalarConverter.Convert(scalar);
                case double scalar: return scalarConverter.Convert(scalar);
                case DateTime scalar: return scalarConverter.Convert(scalar);
                case Guid scalar: return scalarConverter.Convert(scalar);
                case Uuid scalar: return scalarConverter.Convert((Guid)scalar);
                case NodeId scalar: return scalarConverter.Convert(scalar);
                case ExpandedNodeId scalar: return scalarConverter.Convert(scalar);
                case QualifiedName scalar: return scalarConverter.Convert(scalar);
                case LocalizedText scalar: return scalarConverter.Convert(scalar);
                case StatusCode scalar: return scalarConverter.Convert(scalar);
                case System.Xml.XmlElement scalar: return scalarConverter.Convert(scalar);
                case DiagnosticInfo scalar: return scalarConverter.Convert(scalar);
                case DataValue scalar: return scalarConverter.Convert(scalar, Convert(scalar.Value, depth + 1));
                case Enum scalar: return new JValue(System.Convert.ToInt64(scalar));
                case IEnumerable array:
                    var items = new JArray();
                    foreach (var item in array) items.Add(Convert(item, depth + 1));
                    return items;
                default: throw new NotSupportedException($"Unsupported OPC UA value type {value.GetType()}.");
            }
        }

        public JObject DecodeBinary(ExtensionObject extension, Type systemType, IServiceMessageContext context)
        {
            if (extension.Body is not byte[] body)
                throw new NotSupportedException($"Expected binary body for {extension.TypeId}, got {extension.Encoding}.");
            using var decoder = new BinaryDecoder(body, context);
            var value = decoder.ReadEncodeable(null, systemType, extension.TypeId);
            if (decoder.Position != body.Length)
                throw new InvalidOperationException($"Incomplete decoding of {extension.TypeId}: {decoder.Position}/{body.Length} bytes consumed.");
            return (JObject)Convert(value);
        }
    }
}
