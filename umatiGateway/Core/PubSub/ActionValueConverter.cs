// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;
using umatiGateway.Core.OPC;
using UaStatusCodes = Opc.Ua.StatusCodes;

namespace umatiGateway.Core.PubSub
{
    public sealed class ActionValueConverter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IOpcUaClient client;
        private readonly object typeSystemLock = new();
        private Session? typeSystemSession;
        private ComplexTypeSystem? typeSystem;

        public ActionValueConverter(IOpcUaClient client)
        {
            this.client = client;
        }

        public Variant ConvertInput(ActionArgumentValue argument, ActionArgumentMetadata definition)
        {
            if (!string.Equals(argument.DataType, definition.DataType, StringComparison.Ordinal) ||
                argument.ValueRank != definition.ValueRank)
            {
                throw new ActionExecutionException(UaStatusCodes.BadTypeMismatch, $"Argument '{definition.Name}' does not match its advertised type.");
            }

            object? value = definition.ValueRank >= 0
                ? ConvertArray(argument.Value, definition.DataType)
                : ConvertScalar(argument.Value, definition.DataType);
            return new Variant(value);
        }

        public ActionArgumentValue ConvertOutput(Variant variant, ActionArgumentMetadata definition)
        {
            return new ActionArgumentValue
            {
                Name = definition.Name,
                DataType = definition.DataType,
                ValueRank = definition.ValueRank,
                Value = ToJsonToken(variant.Value)
            };
        }

        private object ConvertArray(JToken? token, string dataType)
        {
            if (token is not JArray array)
            {
                throw new ActionExecutionException(UaStatusCodes.BadTypeMismatch, $"Value for array argument of type '{dataType}' is not an array.");
            }

            Type elementType = GetClrType(dataType, wrapStructure: true);
            Array result = Array.CreateInstance(elementType, array.Count);
            for (int index = 0; index < array.Count; index++)
            {
                result.SetValue(ConvertScalar(array[index], dataType), index);
            }

            return result;
        }

        private object? ConvertScalar(JToken? token, string dataType)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (Enum.TryParse(dataType, ignoreCase: true, out BuiltInType builtInType))
            {
                return builtInType switch
                {
                    BuiltInType.Boolean => token.ToObject<bool>(),
                    BuiltInType.SByte => token.ToObject<sbyte>(),
                    BuiltInType.Byte => token.ToObject<byte>(),
                    BuiltInType.Int16 => token.ToObject<short>(),
                    BuiltInType.UInt16 => token.ToObject<ushort>(),
                    BuiltInType.Int32 => token.ToObject<int>(),
                    BuiltInType.UInt32 => token.ToObject<uint>(),
                    BuiltInType.Int64 => token.ToObject<long>(),
                    BuiltInType.UInt64 => token.ToObject<ulong>(),
                    BuiltInType.Float => token.ToObject<float>(),
                    BuiltInType.Double => token.ToObject<double>(),
                    BuiltInType.String => token.ToObject<string>(),
                    BuiltInType.DateTime => token.ToObject<DateTime>(),
                    BuiltInType.Guid => new Uuid(token.ToObject<Guid>()),
                    BuiltInType.ByteString => Convert.FromBase64String(token.ToObject<string>() ?? ""),
                    BuiltInType.NodeId => NodeId.Parse(token.ToObject<string>()),
                    BuiltInType.ExpandedNodeId => ExpandedNodeId.Parse(token.ToObject<string>()),
                    BuiltInType.StatusCode => new StatusCode(token.ToObject<uint>()),
                    BuiltInType.QualifiedName => token.ToObject<QualifiedName>(),
                    BuiltInType.LocalizedText => token.ToObject<LocalizedText>(),
                    BuiltInType.XmlElement => throw new ActionExecutionException(UaStatusCodes.BadNotSupported, "XmlElement action arguments are not supported."),
                    _ => token.ToObject<object>()
                };
            }

            ExpandedNodeId expandedDataType = ExpandedNodeId.Parse(dataType);
            Type systemType = LoadSystemType(expandedDataType);
            JToken normalizedToken = OmitNullProperties(token);
            Logger.Info(
                "Converting structured action value: DataType={DataType}, CLRType={ClrType}, JsonValue={JsonValue}, NormalizedJsonValue={NormalizedJsonValue}",
                dataType,
                systemType.FullName,
                token.ToString(Formatting.None),
                normalizedToken.ToString(Formatting.None));
            object? encodeable = normalizedToken.ToObject(systemType, JsonSerializer.CreateDefault());
            if (encodeable == null)
            {
                throw new ActionExecutionException(UaStatusCodes.BadTypeMismatch, $"Unable to create structured value '{dataType}'.");
            }

            var extensionObject = new ExtensionObject(encodeable);
            Logger.Info("Created structured action ExtensionObject: DataType={DataType}, TypeId={TypeId}, Encoding={Encoding}, BodyType={BodyType}", dataType, extensionObject.TypeId, extensionObject.Encoding, extensionObject.Body?.GetType().FullName);
            return extensionObject;
        }

        public static JToken OmitNullProperties(JToken token)
        {
            JToken clone = token.DeepClone();
            RemoveNullProperties(clone);
            return clone;
        }

        private static void RemoveNullProperties(JToken token)
        {
            if (token is JObject jsonObject)
            {
                foreach (JProperty property in jsonObject.Properties().ToList())
                {
                    if (property.Value.Type == JTokenType.Null)
                    {
                        property.Remove();
                    }
                    else
                    {
                        RemoveNullProperties(property.Value);
                    }
                }
            }
            else if (token is JArray jsonArray)
            {
                foreach (JToken item in jsonArray)
                {
                    RemoveNullProperties(item);
                }
            }
        }

        private Type GetClrType(string dataType, bool wrapStructure)
        {
            if (Enum.TryParse(dataType, ignoreCase: true, out BuiltInType builtInType))
            {
                return builtInType switch
                {
                    BuiltInType.Boolean => typeof(bool),
                    BuiltInType.SByte => typeof(sbyte),
                    BuiltInType.Byte => typeof(byte),
                    BuiltInType.Int16 => typeof(short),
                    BuiltInType.UInt16 => typeof(ushort),
                    BuiltInType.Int32 => typeof(int),
                    BuiltInType.UInt32 => typeof(uint),
                    BuiltInType.Int64 => typeof(long),
                    BuiltInType.UInt64 => typeof(ulong),
                    BuiltInType.Float => typeof(float),
                    BuiltInType.Double => typeof(double),
                    BuiltInType.String => typeof(string),
                    BuiltInType.DateTime => typeof(DateTime),
                    BuiltInType.Guid => typeof(Uuid),
                    BuiltInType.ByteString => typeof(byte[]),
                    BuiltInType.NodeId => typeof(NodeId),
                    BuiltInType.ExpandedNodeId => typeof(ExpandedNodeId),
                    BuiltInType.StatusCode => typeof(StatusCode),
                    BuiltInType.QualifiedName => typeof(QualifiedName),
                    BuiltInType.LocalizedText => typeof(LocalizedText),
                    _ => typeof(object)
                };
            }

            return wrapStructure ? typeof(ExtensionObject) : LoadSystemType(ExpandedNodeId.Parse(dataType));
        }

        private Type LoadSystemType(ExpandedNodeId dataTypeId)
        {
            Session session = client.CheckSession();
            lock (typeSystemLock)
            {
                if (!ReferenceEquals(typeSystemSession, session))
                {
                    typeSystemSession = session;
                    typeSystem = new ComplexTypeSystem(session);
                }

                Type? systemType = session.Factory.GetSystemType(dataTypeId) ??
                    typeSystem!.LoadTypeAsync(dataTypeId, throwOnError: true).ConfigureAwait(false).GetAwaiter().GetResult();
                return systemType ?? throw new ActionExecutionException(UaStatusCodes.BadTypeMismatch, $"No OPC UA type definition is available for '{dataTypeId}'.");
            }
        }

        private static JToken ToJsonToken(object? value)
        {
            if (value == null)
            {
                return JValue.CreateNull();
            }

            if (value is ExtensionObject extensionObject)
            {
                if (extensionObject.Body is byte[] bytes)
                {
                    return new JValue(Convert.ToBase64String(bytes));
                }

                return extensionObject.Body == null ? JValue.CreateNull() : JToken.FromObject(extensionObject.Body);
            }

            if (value is NodeId or ExpandedNodeId or StatusCode or QualifiedName)
            {
                return new JValue(value.ToString());
            }

            if (value is byte[] byteString)
            {
                return new JValue(Convert.ToBase64String(byteString));
            }

            return JToken.FromObject(value);
        }
    }

    public sealed class ActionExecutionException : Exception
    {
        public uint StatusCode { get; }

        public ActionExecutionException(uint statusCode, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}
