// SPDX-License-Identifier: Apache-2.0
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using Opc.Ua.Client.ComplexTypes;
using umatiGateway.Core.Mqtt;
using Xunit;

public class StructuredValueConverterTests
{
    private static StructuredValueConverter Converter() => new(new JSONConverter(false),
        extension => throw new InvalidOperationException($"Unexpected unresolved binary type: {extension.TypeId}"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BinaryJobOrderPreservesNestedStateAndOptionalFields(bool withPriority)
    {
        var context = new ServiceMessageContext();
        context.NamespaceUris.Append("urn:test:jobcontrol");
        using var encoder = new BinaryEncoder(context);
        // Write the wire layout independently of the decoder's generated property metadata.
        encoder.WriteUInt32("EncodingMask", withPriority ? 1u : 0u);
        encoder.WriteString("JobOrderID", "order-42");
        if (withPriority) encoder.WriteInt16("Priority", 7);
        encoder.WriteInt32("NoOfState", 1);
        encoder.WriteInt32("NoOfElements", 1);
        encoder.WriteNodeId("ReferenceTypeId", ReferenceTypeIds.HasComponent);
        encoder.WriteBoolean("IsInverse", false);
        encoder.WriteBoolean("IncludeSubtypes", true);
        encoder.WriteQualifiedName("TargetName", new QualifiedName("Running", 1));
        encoder.WriteLocalizedText("StateText", new LocalizedText("en", "Running"));
        encoder.WriteUInt32("StateNumber", 2);
        var extension = new ExtensionObject(new ExpandedNodeId(5032u, "urn:test:jobcontrol"), encoder.CloseAndReturnBuffer());

        var result = Converter().DecodeBinary(extension, typeof(JobOrderAndState), context);

        Assert.Equal("order-42", (string?)result["JobOrder"]?["JobOrderID"]);
        Assert.Equal(withPriority, ((JObject)result["JobOrder"]!).ContainsKey("Priority"));
        if (withPriority) Assert.Equal(7, (int?)result["JobOrder"]?["Priority"]);
        Assert.Equal(2, (int?)result["State"]?[0]?["StateNumber"]);
        Assert.Equal("Running", (string?)result["State"]?[0]?["StateText"]?["text"]);
        Assert.Equal("Running", (string?)result["State"]?[0]?["BrowsePath"]?["Elements"]?[0]?["TargetName"]?["name"]);
    }

    [Fact]
    public void AlreadyDecodedExtensionObjectsAndVariantArraysArePreserved()
    {
        var job = new JobOrder { JobOrderID = "order-1" };
        var result = Converter().Convert(new[] { new ExtensionObject(job) });
        Assert.Equal("order-1", (string?)result[0]?["JobOrderID"]);
        Assert.False(((JObject)result[0]!).ContainsKey("Priority"));
        Assert.Equal(new JArray("a", "b").ToString(), Converter().Convert(new Variant(new[] { "a", "b" })).ToString());
        Assert.Empty((JArray)Converter().Convert(Array.Empty<string>()));
        Assert.IsType<JValue>(Converter().Convert(new byte[] { 1, 2 }));
    }

    [Fact]
    public void TruncatedOrTrailingBinaryDataIsRejected()
    {
        var context = new ServiceMessageContext();
        using var encoder = new BinaryEncoder(context);
        encoder.WriteUInt32("EncodingMask", 0);
        encoder.WriteString("JobOrderID", "order-1");
        var bytes = encoder.CloseAndReturnBuffer();
        var converter = Converter();
        Assert.ThrowsAny<Exception>(() => converter.DecodeBinary(
            new ExtensionObject(new ExpandedNodeId(1), bytes[..^1]), typeof(JobOrder), context));
        Assert.Throws<InvalidOperationException>(() => converter.DecodeBinary(
            new ExtensionObject(new ExpandedNodeId(1), bytes.Concat(new byte[] { 0 }).ToArray()), typeof(JobOrder), context));
    }

    [DataContract(Namespace = "urn:test:jobcontrol")]
    public class JobOrderAndState : BaseComplexType
    {
        [DataMember(Order = 1), StructureField(BuiltInType = (int)BuiltInType.Null)]
        public JobOrder JobOrder { get; set; } = new();
        [DataMember(Order = 2), StructureField(BuiltInType = (int)BuiltInType.Null, ValueRank = ValueRanks.OneDimension)]
        public State[] State { get; set; } = Array.Empty<State>();
    }

    [DataContract(Namespace = "urn:test:jobcontrol")]
    public class JobOrder : OptionalFieldsComplexType
    {
        [DataMember(Order = 1), StructureField(BuiltInType = (int)BuiltInType.String)]
        public string JobOrderID { get; set; } = "";
        [DataMember(Order = 2), StructureField(BuiltInType = (int)BuiltInType.Int16, IsOptional = true)]
        public short Priority { get; set; }
    }

    [DataContract(Namespace = "urn:test:jobcontrol")]
    public class State : BaseComplexType
    {
        [DataMember(Order = 1), StructureField(BuiltInType = (int)BuiltInType.Null)]
        public RelativePath BrowsePath { get; set; } = new();
        [DataMember(Order = 2), StructureField(BuiltInType = (int)BuiltInType.LocalizedText)]
        public LocalizedText StateText { get; set; } = LocalizedText.Null;
        [DataMember(Order = 3), StructureField(BuiltInType = (int)BuiltInType.UInt32)]
        public uint StateNumber { get; set; }
    }
}
