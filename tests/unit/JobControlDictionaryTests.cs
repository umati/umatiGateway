// SPDX-License-Identifier: Apache-2.0
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.Mqtt;
using umatiGateway.Core.OPC;
using Xunit;

public class JobControlDictionaryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BundledDictionaryDecodesJobOrderWithoutServerDefinitionsOrExtraLibs(bool description, bool priority)
    {
        var app = (UmatiGatewayApp)RuntimeHelpers.GetUninitializedObject(typeof(UmatiGatewayApp));
        app.ActiveConfiguration = new UmatiConfiguration();
        var client = DispatchProxy.Create<IOpcUaClient, DictionaryOnlyClient>();
        app.OpcUaClient = client;
        var dictionaries = new TypeDictionaries(app) { ReadExtraLibs = false };
        ((DictionaryOnlyClient)client).Dictionaries = dictionaries;
        dictionaries.ReadOpcBinary();
        var provider = new MqttProvider(app);
        var structure = provider.GetGeneratedDataClass(
            "http://opcfoundation.org/UA/ISA95-JOBCONTROL_V2/", "ISA95JobOrderAndStateDataType");
        Assert.NotNull(structure);

        var context = new ServiceMessageContext();
        using var encoder = new BinaryEncoder(context);
        // Encode the actual ISA95 layout independently of the BSD reader.
        encoder.WriteUInt32("EncodingMask", (description ? 1u : 0u) | (priority ? 1u << 4 : 0u));
        encoder.WriteString("JobOrderID", "synerJob150926_4796");
        if (description)
        {
            encoder.WriteInt32("NoOfDescription", 1);
            encoder.WriteLocalizedText("Description", new LocalizedText("de", "Glasauftrag"));
        }
        if (priority) encoder.WriteInt16("Priority", 7);
        encoder.WriteInt32("NoOfState", 2);
        encoder.WriteInt32("NoOfElements", 0);
        encoder.WriteLocalizedText("StateText", new LocalizedText("en", "NotExecuting"));
        encoder.WriteUInt32("StateNumber", 2);
        encoder.WriteInt32("NoOfElements", 1);
        new RelativePathElement
        {
            ReferenceTypeId = ReferenceTypeIds.HasComponent,
            IsInverse = false,
            IncludeSubtypes = true,
            TargetName = new QualifiedName("Ready", 2)
        }.Encode(encoder);
        encoder.WriteLocalizedText("StateText", new LocalizedText("en", "Ready"));
        encoder.WriteUInt32("StateNumber", 3);
        var bytes = encoder.CloseAndReturnBuffer();
        using var decoder = new BinaryDecoder(bytes, context);

        var result = provider.decode(decoder, structure!);

        Assert.Equal(bytes.Length, decoder.Position);
        var job = Assert.IsType<JObject>(result["JobOrder"]);
        Assert.Equal("synerJob150926_4796", (string?)job["JobOrderID"]);
        Assert.Equal(description, job.ContainsKey("Description"));
        if (description) Assert.Equal("Glasauftrag", (string?)job["Description"]?[0]?["text"]);
        Assert.Equal(priority, job.ContainsKey("Priority"));
        if (priority) Assert.Equal(7, (int?)job["Priority"]);
        Assert.Equal(2, result["State"]!.Count());
        Assert.Equal("NotExecuting", (string?)result["State"]?[0]?["StateText"]?["text"]);
        Assert.Equal("Ready", (string?)result["State"]?[1]?["BrowsePath"]?["Elements"]?[0]?["TargetName"]?["name"]);
        Assert.Equal(3, (int?)result["State"]?[1]?["StateNumber"]);
        Assert.DoesNotContain("Exception", result.ToString());
    }

    public class DictionaryOnlyClient : DispatchProxy
    {
        public TypeDictionaries Dictionaries { get; set; } = null!;

        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IOpcUaClient.GetTypeDictionaries) => Dictionaries,
            nameof(IOpcUaClient.BrowseLocalNodeIdsWithTypeDefinition) => new List<NodeId>(),
            nameof(IOpcUaClient.BrowseLocalNodeIds) => new List<NodeId>(),
            nameof(IOpcUaClient.ReadNode) => null,
            _ => throw new InvalidOperationException($"Unexpected server access: {method.Name}")
        };
    }
}
