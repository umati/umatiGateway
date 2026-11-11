// SPDX-License-Identifier: Apache-2.0
using System.Runtime.CompilerServices;
using Opc.Ua;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.Mqtt;
using umatiGateway.Core.OPC;
using Xunit;

public class MqttIdentifierDecodingTests
{
    private static MqttProvider Provider()
    {
        // Exercise the BSD decoder without starting application logging or client threads.
        var app = (UmatiGatewayApp)RuntimeHelpers.GetUninitializedObject(typeof(UmatiGatewayApp));
        app.ActiveConfiguration = new UmatiConfiguration();
        return new MqttProvider(app);
    }

    public static IEnumerable<object[]> NodeIds()
    {
        yield return new object[] { new NodeId(47u) };
        yield return new object[] { new NodeId(500u, 2) };
        yield return new object[] { new NodeId(100000u, 300) };
        yield return new object[] { new NodeId("reference", 2) };
        yield return new object[] { new NodeId(Guid.Parse("12345678-1234-1234-1234-123456789abc"), 2) };
        yield return new object[] { new NodeId(new byte[] { 1, 2, 3 }, 2) };
    }

    [Theory]
    [MemberData(nameof(NodeIds))]
    public void RelativePathElementConsumesAllNodeIdEncodings(NodeId reference)
    {
        var context = new ServiceMessageContext();
        var element = new RelativePathElement
        {
            ReferenceTypeId = reference,
            IsInverse = true,
            IncludeSubtypes = false,
            TargetName = new QualifiedName("Running", 2)
        };
        using var encoder = new BinaryEncoder(context);
        element.Encode(encoder);
        encoder.WriteUInt32("Sentinel", 123456);
        var bytes = encoder.CloseAndReturnBuffer();
        using var decoder = new BinaryDecoder(bytes, context);
        var structure = new GeneratedStructure
        {
            Name = "RelativePathElement",
            fields = new()
            {
                new() { Name = "ReferenceTypeId", TypeName = "ua:NodeId" },
                new() { Name = "IsInverse", TypeName = "opc:Boolean" },
                new() { Name = "IncludeSubtypes", TypeName = "opc:Boolean" },
                new() { Name = "TargetName", TypeName = "ua:QualifiedName" }
            }
        };

        var result = Provider().decode(decoder, structure);

        Assert.Equal(4, result.Count);
        Assert.Equal(reference.ToString(), (string?)result["ReferenceTypeId"]);
        Assert.True((bool?)result["IsInverse"]);
        Assert.False((bool?)result["IncludeSubtypes"]);
        Assert.Equal("Running", (string?)result["TargetName"]?["name"]);
        Assert.Equal(2, (int?)result["TargetName"]?["namespaceIndex"]);
        Assert.Equal(123456u, decoder.ReadUInt32("Sentinel"));
        Assert.Equal(bytes.Length, decoder.Position);
    }

    [Theory]
    [InlineData("ua:NodeId", 2)]
    [InlineData("ua:ExpandedNodeId", 2)]
    [InlineData("ua:QualifiedName", 2)]
    [InlineData("ua:NodeId", 0)]
    [InlineData("ua:NodeId", -1)]
    public void IdentifierArraysPreserveFollowingFields(string type, int count)
    {
        var context = new ServiceMessageContext();
        using var encoder = new BinaryEncoder(context);
        encoder.WriteInt32("NoOfValues", count);
        for (int i = 0; i < count; i++)
        {
            if (type == "ua:NodeId") encoder.WriteNodeId(null, new NodeId((uint)(47 + i)));
            else if (type == "ua:ExpandedNodeId") encoder.WriteExpandedNodeId(null, new ExpandedNodeId((uint)(47 + i), "urn:test", 3));
            else encoder.WriteQualifiedName(null, new QualifiedName($"Name{i}", 2));
        }
        encoder.WriteBoolean("Following", true);
        var bytes = encoder.CloseAndReturnBuffer();
        using var decoder = new BinaryDecoder(bytes, context);
        var structure = new GeneratedStructure
        {
            Name = "Identifiers",
            fields = new()
            {
                new() { Name = "NoOfValues", TypeName = "opc:Int32" },
                new() { Name = "Values", TypeName = type, IsLengthField = true },
                new() { Name = "Following", TypeName = "opc:Boolean" }
            }
        };

        var result = Provider().decode(decoder, structure);

        Assert.Equal(Math.Max(0, count), result["Values"]!.Count());
        if (count > 0)
        {
            if (type == "ua:QualifiedName") Assert.Equal("Name1", (string?)result["Values"]?[1]?["name"]);
            else if (type == "ua:NodeId") Assert.Equal("i=48", (string?)result["Values"]?[1]);
            else
            {
                Assert.Equal(3, (int?)result["Values"]?[1]?["serverIndex"]);
                Assert.Equal("urn:test", (string?)result["Values"]?[1]?["namespaceUri"]);
                Assert.Equal(48, (int?)result["Values"]?[1]?["identifier"]);
            }
        }
        Assert.True((bool?)result["Following"]);
        Assert.Equal(bytes.Length, decoder.Position);
    }
}
