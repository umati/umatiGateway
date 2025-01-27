// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Newtonsoft.Json.Linq;
using NLog.LayoutRenderers;
using Opc.Ua;
using Org.BouncyCastle.Utilities;
using System.Xml;

namespace UmatiGateway.OPC
{
    /// <summary>
    /// This class is used to convert OPC UA DataValues to JSON Tokens. Depending on the DataValue
    /// the converted JSON is either an JArray, JObject or JValue. It also holds the Value that should be used
    /// if the DataValue is null.
    /// </summary>
    public class JSONConverter
    {
        //private readonly JToken defaultNullValue = JValue.CreateNull();
        public JSONConverter()
        {
        }


        // Conversion of BaseDataTypes

        public JToken Convert(Boolean? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(Byte? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(byte[]? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(DateTime? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(DiagnosticInfo? value)
        {
            if (value == null) return this.GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("namespaceUri", this.Convert(value.NamespaceUri));
            jObject.Add("symbolicId", this.Convert(value.SymbolicId));
            jObject.Add("locale", this.Convert(value.Locale));
            jObject.Add("localizedText", this.Convert(value.LocalizedText));
            jObject.Add("additionalInfo", this.Convert(value.AdditionalInfo));
            jObject.Add("innerStatusCode", this.Convert(value.InnerStatusCode));
            jObject.Add("innerDiagnosticInfo", this.Convert(value.InnerDiagnosticInfo));
            return jObject;
        }
        public JToken Convert(Double? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(ExpandedNodeId? value)
        {
            if (value == null) return this.GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("serverIndex", this.Convert(value.ServerIndex));
            jObject.Add("namespaceUri", this.Convert(value.NamespaceUri));
            jObject.Add("namespaceIndex", this.Convert(value.NamespaceIndex));
            jObject.Add("identifierType", this.Convert(value.IdType));
            JToken identifierValue = JValue.CreateNull();
            object identifier = value.Identifier;
            if (identifier != null)
            {
                switch (identifier)
                {
                    case string stringIdentifier: identifierValue = this.Convert(stringIdentifier); break;
                    case Guid guidIdentifier: identifierValue = this.Convert(guidIdentifier); break;
                    case uint uintIdentifier: identifierValue = this.Convert(uintIdentifier); break;
                    case byte[] opaqueIdentifier: identifierValue = this.Convert(opaqueIdentifier); break;
                    default: identifierValue = new JValue("Error: Can not determine Identifier"); break;
                }
            }
            jObject.Add("identifier", identifierValue);
            return jObject;
        }
        public JToken Convert(float? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(Guid? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(IdType? value)
        {
            if (value == null) return this.GetDefaultNullValue();
            return new JValue(value.ToString());
        }
        public JToken Convert(Int16? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(Int32? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(Int64? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(LocalizedText? value)
        {
            if (value == null) return this.GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("locale", this.Convert(value.Locale));
            jObject.Add("text", this.Convert(value.Text));
            return jObject;
        }
        public JToken Convert(NodeId? value)
        {
            return value != null ? new JValue(value.ToString()) : this.GetDefaultNullValue();
        }
        public JToken Convert(QualifiedName? value)
        {
            if (value == null) return this.GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("namespaceIndex", this.Convert(value.NamespaceIndex));
            jObject.Add("name", this.Convert(value.Name));
            return jObject;
        }
        public JToken Convert(SByte? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }

        public JToken Convert(StatusCode? value)
        {
            if (value == null) return this.GetDefaultNullValue();
            return new JValue((UInt32)value);
        }
        public JToken Convert(String? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(UInt16? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(UInt32? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(UInt32Collection? value)
        {
            if (value == null) return this.GetDefaultNullValue();
            JArray jArray = new JArray();
            foreach (UInt32 item in value)
            {
                jArray.Add(this.Convert(item));
            }
            return jArray;
        }
        public JToken Convert(UInt64? value)
        {
            return value != null ? new JValue(value) : this.GetDefaultNullValue();
        }
        public JToken Convert(XmlElement? value)
        {
            return value != null ? new JValue(value.ToString()) : this.GetDefaultNullValue();
        }

        // Conversion of Structures

        public JToken Convert(Argument? argument)
        {
            if (argument == null) return this.GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("Name", this.Convert(argument.Name));
            jObject.Add("DataType", this.Convert(argument.DataType));
            jObject.Add("ValueRank", this.Convert(argument.ValueRank));
            jObject.Add("ArrayDimensions", this.Convert(argument.ArrayDimensions));
            jObject.Add("Description", this.Convert(argument.Description));
            return jObject;
        }
        public JToken Convert(EUInformation? euInformation)
        {
            if (euInformation == null) return this.GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("NamespaceUri", this.Convert(euInformation.NamespaceUri));
            jObject.Add("UnitId", this.Convert(euInformation.UnitId));
            jObject.Add("DisplayNmae", this.Convert(euInformation.DisplayName));
            jObject.Add("Description", this.Convert(euInformation.Description));
            return jObject;
        }
        public JToken Convert(Opc.Ua.Range? range)
        {
            if (range == null) return this.GetDefaultNullValue();
            JObject jObject = new JObject();
            jObject.Add("low", range.Low);
            jObject.Add("high", range.High);
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
