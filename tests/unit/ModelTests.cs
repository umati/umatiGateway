using System.Text.Json;
using UmatiGateway.Mapping;
using UmatiGateway.Model;
using Xunit;

namespace umatiGateway.Tests;

public class ModelTests
{
    [Fact]
    public void PathBecomesTopic() =>
        Assert.Equal("m1/Consumption/Electricity/Main/AcActivePowerTotal",
            new TopicBuilder(null, "m1").Value("Consumption.Electricity.Main.AcActivePowerTotal"));

    [Fact]
    public void PrefixAndManifestTopic()
    {
        var t = new TopicBuilder("/umati/", "m1");
        Assert.Equal("umati/m1/State/Status", t.Value("State.Status"));
        Assert.Equal("umati/m1/_manifest", t.Manifest);
        Assert.Equal("umati/m1", t.Root);
    }

    [Fact]
    public void MachineIdIsPercentEncoded()
    {
        var id = TopicBuilder.EncodeMachineId("http://vdw.de/SampleMachineTool#123456");
        Assert.Equal("http%3A%2F%2Fvdw.de%2FSampleMachineTool%23123456", id);
        Assert.DoesNotContain("/", id);
        Assert.DoesNotContain(":", id);
    }

    [Fact]
    public void EnvelopeCarriesUnitOnlyOnNumbers()
    {
        var ts = new DateTime(2026, 11, 17, 9, 15, 0, 123, DateTimeKind.Utc);
        var num = JsonDocument.Parse(Json.Serialize(Envelope.Create(1.5, ts, null, "one"))).RootElement;
        Assert.Equal("one", num.GetProperty("unit").GetString());
        Assert.Equal("2026-11-17T09:15:00.123Z", num.GetProperty("timestamp").GetString());

        var str = JsonDocument.Parse(Json.Serialize(Envelope.Create("x", ts, null, "one"))).RootElement;
        Assert.False(str.TryGetProperty("unit", out _));
        Assert.False(str.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public void UnavailableValueIsSerializedAsNull()
    {
        var e = JsonDocument.Parse(Json.Serialize(Envelope.Create(null, DateTime.UtcNow))).RootElement;
        Assert.Equal(JsonValueKind.Null, e.GetProperty("value").ValueKind);
    }

    [Fact]
    public void CorrelationIdIsKept()
    {
        var e = JsonDocument.Parse(Json.Serialize(Envelope.Create("Active", DateTime.UtcNow, "exec-1"))).RootElement;
        Assert.Equal("exec-1", e.GetProperty("correlationId").GetString());
    }
}

public class MappingTests
{
    private static string DefaultMapping => Path.Combine(AppContext.BaseDirectory, "mappings", "machinetool-default.json");

    [Fact]
    public void DefaultMappingLoadsAndIsValid()
    {
        var m = MappingDefinition.Load(DefaultMapping);
        Assert.Equal("0.13.1", m.ModelVersion);
        Assert.Contains("Core", m.Profiles);
        Assert.Contains(m.Elements, e => e.Path == "State.Status");
        Assert.Contains(m.Elements, e => e.Path == "Production.ProgramExecution" && e.Special == "programExecution");
        Assert.Contains(m.Elements, e => e.Path == "Channel.<n>.ChannelState" && e.Instances is not null);
    }

    [Fact]
    public void DuplicatePathsAreRejected()
    {
        var m = new MappingDefinition
        {
            ModelVersion = "0.13.1", Profiles = { "Core" },
            Elements = { new MappingElement { Path = "A.B", Constant = System.Text.Json.Nodes.JsonValue.Create(1) }, new MappingElement { Path = "A.B", Constant = System.Text.Json.Nodes.JsonValue.Create(2) } },
        };
        Assert.Throws<InvalidDataException>(m.Validate);
    }

    [Fact]
    public void UnknownTransformIsRejected()
    {
        var m = new MappingDefinition
        {
            ModelVersion = "0.13.1", Profiles = { "Core" },
            Elements = { new MappingElement { Path = "A.B", Transform = "nope", Inputs = new() { ["v"] = "X" } } },
        };
        Assert.Throws<InvalidDataException>(m.Validate);
    }
}
