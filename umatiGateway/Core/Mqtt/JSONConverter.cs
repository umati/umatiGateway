// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Newtonsoft.Json.Linq;
using NLog.LayoutRenderers;
using Opc.Ua;
using Org.BouncyCastle.Utilities;
using System.Xml;

namespace umatiGateway.Core.Mqtt
{
    /// <summary>
    /// This class is used to convert OPC UA DataValues to JSON Tokens. Depending on the DataValue
    /// the converted JSON is either an JArray, JObject or JValue. It also holds the Value that should be used
    /// if the DataValue is null.
    /// </summary>
    public class JSONConverter
    {
        //private readonly JToken defaultNullValue = JValue.CreateNull();
        private bool upperCaseRange = false;
        public JSONConverter(bool upperCaseRange)
        {
            this.upperCaseRange = upperCaseRange;
        }
        // Conversion of BaseDataTypes

        public JToken Convert(bool? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }

        public JToken Convert(byte? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(byte[]? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(DateTime? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(DiagnosticInfo? value)
        {
            if (value == null) return GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("namespaceUri", Convert(value.NamespaceUri));
            jObject.Add("symbolicId", Convert(value.SymbolicId));
            jObject.Add("locale", Convert(value.Locale));
            jObject.Add("localizedText", Convert(value.LocalizedText));
            jObject.Add("additionalInfo", Convert(value.AdditionalInfo));
            jObject.Add("innerStatusCode", Convert(value.InnerStatusCode));
            jObject.Add("innerDiagnosticInfo", Convert(value.InnerDiagnosticInfo));
            return jObject;
        }
        public JToken Convert(double? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(ExpandedNodeId? value)
        {
            if (value == null) return GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("serverIndex", Convert(value.ServerIndex));
            jObject.Add("namespaceUri", Convert(value.NamespaceUri));
            jObject.Add("namespaceIndex", Convert(value.NamespaceIndex));
            jObject.Add("identifierType", Convert(value.IdType));
            JToken identifierValue = JValue.CreateNull();
            object identifier = value.Identifier;
            if (identifier != null)
            {
                switch (identifier)
                {
                    case string stringIdentifier: identifierValue = Convert(stringIdentifier); break;
                    case Guid guidIdentifier: identifierValue = Convert(guidIdentifier); break;
                    case uint uintIdentifier: identifierValue = Convert(uintIdentifier); break;
                    case byte[] opaqueIdentifier: identifierValue = Convert(opaqueIdentifier); break;
                    default: identifierValue = new JValue("Error: Can not determine Identifier"); break;
                }
            }
            jObject.Add("identifier", identifierValue);
            return jObject;
        }
        public JToken Convert(float? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(Guid? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(IdType? value)
        {
            if (value == null) return GetDefaultNullValue();
            return new JValue(value.ToString());
        }
        public JToken Convert(short? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(int? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(long? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(LocalizedText? value)
        {
            if (value == null) return GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("locale", Convert(value.Locale));
            jObject.Add("text", Convert(value.Text));
            return jObject;
        }
        public JToken Convert(NodeId? value)
        {
            return value != null ? new JValue(value.ToString()) : GetDefaultNullValue();
        }
        public JToken Convert(QualifiedName? value)
        {
            if (value == null) return GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("namespaceIndex", Convert(value.NamespaceIndex));
            jObject.Add("name", Convert(value.Name));
            return jObject;
        }
        public JToken Convert(sbyte? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }

        public JToken Convert(StatusCode? value)
        {
            if (value == null) return GetDefaultNullValue();
            return new JValue((uint)value);
        }
        public JToken Convert(string? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(ushort? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(uint? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(UInt32Collection? value)
        {
            if (value == null) return GetDefaultNullValue();
            JArray jArray = new JArray();
            foreach (uint item in value)
            {
                jArray.Add(Convert(item));
            }
            return jArray;
        }
        public JToken Convert(ulong? value)
        {
            return value != null ? new JValue(value) : GetDefaultNullValue();
        }
        public JToken Convert(XmlElement? value)
        {
            return value != null ? new JValue(value.ToString()) : GetDefaultNullValue();
        }

        // Conversion of Structures

        public JToken Convert(Argument? argument)
        {
            if (argument == null) return GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("Name", Convert(argument.Name));
            jObject.Add("DataType", Convert(argument.DataType));
            jObject.Add("ValueRank", Convert(argument.ValueRank));
            jObject.Add("ArrayDimensions", Convert(argument.ArrayDimensions));
            jObject.Add("Description", Convert(argument.Description));
            return jObject;
        }
        public JToken Convert(EUInformation? euInformation)
        {
            if (euInformation == null) return GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("NamespaceUri", Convert(euInformation.NamespaceUri));
            jObject.Add("UnitId", Convert(euInformation.UnitId));
            jObject.Add("DisplayName", Convert(euInformation.DisplayName));
            jObject.Add("Description", Convert(euInformation.Description));
            return jObject;
        }
        public JToken Convert(Opc.Ua.Range? range)
        {
            if (range == null) return GetDefaultNullValue();
            JObject jObject = new JObject();
            if (upperCaseRange)
            {
                jObject.Add("Low", range.Low);
                jObject.Add("High", range.High);
            }
            else
            {
                jObject.Add("low", range.Low);
                jObject.Add("high", range.High);
            }
            return jObject;
        }
        public JToken Convert(EnumValueType enumValueType)
        {
            if (enumValueType == null) return GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("Value", enumValueType.Value);
            jObject.Add("DisplayName", enumValueType.DisplayName.ToString());
            jObject.Add("Description", enumValueType.Description.ToString());
            return jObject;
        }
        public JToken Convert(DataValue dataValue, object value)
        {
            if (dataValue == null) return GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("StatusCode", this.Convert(dataValue.StatusCode));
            if (dataValue.ServerTimestamp != DateTime.MinValue)
            {
                jObject.Add("SourceTimeStamp", this.Convert(dataValue.SourceTimestamp));
            }
            if (dataValue.SourceTimestamp != DateTime.MinValue)
            {
                jObject.Add("ServerTimeStamp", this.Convert(dataValue.ServerTimestamp));
            }
            if (dataValue.ServerPicoseconds != 0)
            {
                jObject.Add("ServerPicoseconds", this.Convert(dataValue.ServerPicoseconds));
            }
            if (dataValue.SourcePicoseconds != 0)
            {
                jObject.Add("SourcePicoseconds", this.Convert(dataValue.SourcePicoseconds));
            }
            if (value is JToken valueToken)
            {
                jObject.Add("Value", valueToken);
            }
            return jObject;
        }
        /// <summary>
        /// Returns the JToken that should be used if the DataValue is null.
        /// </summary>
        /// <returns>The JToken that is to be used if the DataValue is null.</returns>
        public JToken GetDefaultNullValue()
        {
            return JValue.CreateNull();
        }
    }
}
