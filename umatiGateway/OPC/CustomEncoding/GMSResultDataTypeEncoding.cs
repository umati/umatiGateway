// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Newtonsoft.Json.Linq;
using Opc.Ua;
using Org.BouncyCastle.Asn1.X509.Qualified;

namespace UmatiGateway.OPC.CustomEncoding
{
    public class GMSResultDataTypeEncoding : ICustomEncoding
    {
        private static ExpandedNodeId GMSResultEncodingId = new ExpandedNodeId(new NodeId(5008), "http://opcfoundation.org/UA/Machinery/Result/");
        public string FullFieldName { get => "GMSResultDataTypeEncoding"; }
        public ExpandedNodeId NodeId { get => GMSResultEncodingId; }
        public GMSResultDataTypeEncoding()
        {

        }
        public JToken? decode(byte Binary)
        {
            return null;
        }
        public void encode()
        {
        }

        public JObject? decode(ExtensionObject eto)
        {
            ExtensionObjectEncoding encoding = eto.Encoding;
            switch (encoding)
            {
                case ExtensionObjectEncoding.Binary: return this.decodeBinary(eto);
                case ExtensionObjectEncoding.Xml: throw new CustomEncodingException($"Unable to decode ResultDataType. Xml decoding not implemented.");
                case ExtensionObjectEncoding.Json: throw new CustomEncodingException($"Unable to decode ResultDataType. Json decoding not implemented.");
                case ExtensionObjectEncoding.None: throw new CustomEncodingException($"Unable to decode ResultDataType. Encoding set to NONE.");
                case ExtensionObjectEncoding.EncodeableObject: throw new CustomEncodingException($"Unable to decode ResultDataType. EncodeableObject not implemented.");
                default: throw new CustomEncodingException($"Unable to decode ResultDataType. No Encoding given.");
            }
        }

        public ExtensionObject? encode(JObject jObject, ExtensionObjectEncoding encoding)
        {
            switch (encoding)
            {
                case ExtensionObjectEncoding.Binary: throw new CustomEncodingException($"Unable to encode ResultDataType. Binary encoding not implemented.");
                case ExtensionObjectEncoding.Xml: throw new CustomEncodingException($"Unable to encode ResultDataType. Xml encoding not implemented.");
                case ExtensionObjectEncoding.Json: throw new CustomEncodingException($"Unable to encode ResultDataType. Json encoding not implemented.");
                case ExtensionObjectEncoding.None: throw new CustomEncodingException($"Unable to encode ResultDataType. Encoding set to None.");
                case ExtensionObjectEncoding.EncodeableObject: throw new CustomEncodingException($"Unable to encode ResultDataType. EncodeableObjecy encoding not implemented.");
                default: throw new CustomEncodingException($"Unable to encode ResultDataType. Mo encoding given.");
            }
        }
        private JObject? decodeBinary(ExtensionObject eto)
        {
            JObject resultData = new JObject();
            JObject resultMetaData = new JObject();
            JObject resultContent = new JObject();
            resultData.Add("ResultMetaData", resultMetaData);
            try
            {
                BinaryDecoder binaryDecoder = new BinaryDecoder((byte[])eto.Body, ServiceMessageContext.GlobalContext);
                Int32 resultDataEncodingMask = binaryDecoder.ReadInt32("EncodingMask");
                bool hasResultContent = ((resultDataEncodingMask >> 0) & 1) == 1;
                Int32 encodingMask = binaryDecoder.ReadInt32("EncodingMask");
                bool hasHasTransferableDataOnFile = ((encodingMask >> 0) & 1) == 1;
                bool hasIsPartial = ((encodingMask >> 1) & 1) == 1;
                bool hasIsSimulated = ((encodingMask >> 2) & 1) == 1;
                bool hasResultState = ((encodingMask >> 3) & 1) == 1;
                bool hasStepId = ((encodingMask >> 4) & 1) == 1;
                bool hasPartId = ((encodingMask >> 5) & 1) == 1;
                bool hasExternalRecipeId = ((encodingMask >> 6) & 1) == 1;
                bool hasInternalRecipeId = ((encodingMask >> 7) & 1) == 1;
                bool hasProductId = ((encodingMask >> 8) & 1) == 1;
                bool hasExternalConfigurationId = ((encodingMask >> 9) & 1) == 1;
                bool hasInternalConfigurationId = ((encodingMask >> 10) & 1) == 1;
                bool hasJobId = ((encodingMask >> 11) & 1) == 1;
                bool hasCreationTime = ((encodingMask >> 12) & 1) == 1;
                bool hasProcessingTimes = ((encodingMask >> 13) & 1) == 1;
                bool hasResultUri = ((encodingMask >> 14) & 1) == 1;
                bool hasResultEvaluation = ((encodingMask >> 15) & 1) == 1;
                bool hasResultEvaluationCode = ((encodingMask >> 16) & 1) == 1;
                bool hasResultEvaluationDetails = ((encodingMask >> 17) & 1) == 1;
                bool hasFileFormat = ((encodingMask >> 18) & 1) == 1;
                resultMetaData.Add("ResultId", binaryDecoder.ReadString("ResultId"));
                if (hasHasTransferableDataOnFile) resultMetaData.Add("HasTransferableDataOnFile", binaryDecoder.ReadBoolean("HasTransferableDataOnFile"));
                if (hasIsPartial) resultMetaData.Add("IsPartial", binaryDecoder.ReadBoolean("IsPartial"));
                if (hasIsSimulated) resultMetaData.Add("IsSimulated", binaryDecoder.ReadBoolean("IsSimulated"));
                if (hasResultState) resultMetaData.Add("ResultState", binaryDecoder.ReadInt32("ResultState"));
                if (hasStepId) resultMetaData.Add("StepId", binaryDecoder.ReadString("StepId"));
                if (hasPartId) resultMetaData.Add("PartId", binaryDecoder.ReadString("PartId"));
                if (hasExternalRecipeId) resultMetaData.Add("ExternalRecipeId", binaryDecoder.ReadString("ExternalRecipeId"));
                if (hasInternalRecipeId) resultMetaData.Add("InternalRecipeId", binaryDecoder.ReadString("InternalRecipeId"));
                if (hasProductId) resultMetaData.Add("ProductId", binaryDecoder.ReadString("ProductId"));
                if (hasExternalConfigurationId) resultMetaData.Add("ExternalConfigurationId", binaryDecoder.ReadString("ExternalConfigurationId"));
                if (hasInternalConfigurationId) resultMetaData.Add("InternalConfigurationId", binaryDecoder.ReadString("InternalConfigurationId"));
                if (hasJobId) resultMetaData.Add("JobId", binaryDecoder.ReadString("JobId"));
                if (hasCreationTime) resultMetaData.Add("CreationTime", binaryDecoder.ReadDateTime("CreationTime"));
                if (hasProcessingTimes)
                { // TBD Look if this is an extension DataType
                }
                if (hasResultUri)
                {
                    Int32 length = binaryDecoder.ReadInt32("length");
                    JArray resultUri = new JArray(length);
                    for (int i = 0; i < length; i++)
                    {
                        resultUri[i] = binaryDecoder.ReadString("ResultUri");
                    }
                    resultMetaData.Add("ResultUri", resultUri);
                }
                if (hasResultEvaluation) resultMetaData.Add("ResultEvaluation", binaryDecoder.ReadInt32("ResultEvaluation"));
                if (hasResultEvaluationCode) resultMetaData.Add("ResultEvaluationCode", binaryDecoder.ReadInt64("ResultEvaluationCode"));
                if (hasResultEvaluationDetails) resultMetaData.Add("ResultEvaluationDetails", binaryDecoder.ReadLocalizedText("ResultEvaluationDetails").ToString());
                if (hasFileFormat)
                {
                    Int32 length = binaryDecoder.ReadInt32("length");
                    JArray fileFormat = new JArray(length);
                    for (int i = 0; i < length; i++)
                    {
                        fileFormat[i] = binaryDecoder.ReadString("FileFormat");
                    }
                    resultMetaData.Add("FileFormat", fileFormat);
                }
                if (hasResultContent)
                {
                    int length = binaryDecoder.ReadInt32("length");
                    for (int i = 0; i < length; i++)
                    {
                        //resultUri[i] = binaryDecoder.ReadString("ResultUri"); TBD Redirect to decoing here
                    }
                    resultData.Add("ResultContent", resultContent);
                }
                return resultData;
            }
            catch (Exception ex)
            {
                throw new CustomEncodingException($"Exception on decoding ResultData. Partial decoded object {resultData}", ex);
            }
        }
    }
}
