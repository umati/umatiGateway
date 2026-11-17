using System.Text.Json.Nodes;
using UmatiGateway.Mapping;
using Xunit;

namespace umatiGateway.Tests;

public class TransformTests
{
    private static IReadOnlyDictionary<string, object?> In(object? v) => new Dictionary<string, object?> { ["v"] = v };
    private static JsonObject P(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact] public void Text_EmptyIsUnavailable() => Assert.Null(Transforms.Apply("text", In(""), null));
    [Fact] public void Text_PassesString() => Assert.Equal("abc", Transforms.Apply("text", In("abc"), null));

    [Fact] public void Integer_Rounds() => Assert.Equal(2026L, Transforms.Apply("integer", In(2025.6), null));
    [Fact] public void Integer_NullStaysNull() => Assert.Null(Transforms.Apply("integer", In(null), null));

    [Fact] public void Number_RemovesNoise() => Assert.Equal(0.3, Transforms.Apply("number", In(0.1 + 0.2), null));
    [Fact] public void Number_NaNIsUnavailable() => Assert.Null(Transforms.Apply("number", In(double.NaN), null));

    [Fact] public void Scale_PercentToRatio() => Assert.Equal(0.95, Transforms.Apply("scale", In(95L), P("{\"factor\":0.01}")));
    [Fact] public void Scale_MillisecondsToSeconds() => Assert.Equal(12.345, Transforms.Apply("scale", In(12345L), P("{\"factor\":0.001}")));
    [Fact] public void Scale_KelvinToCelsius() => Assert.Equal(19.85, Transforms.Apply("scale", In(293.0), P("{\"offset\":-273.15}")));

    [Fact]
    public void StructSum_SumsPhases()
    {
        var s = new Dictionary<string, object?> { ["L1"] = 1000.0, ["L2"] = 1500L, ["L3"] = 2000.5 };
        Assert.Equal(4500.5, Transforms.Apply("structSum", In(s), P("{}")));
    }

    [Fact] public void Boolean_Variants()
    {
        Assert.Equal(true, Transforms.Apply("boolean", In(true), null));
        Assert.Equal(false, Transforms.Apply("boolean", In(0L), null));
        Assert.Null(Transforms.Apply("boolean", In(null), null));
    }

    private const string StatusParams = """
    {"fields":{
      "MachineryItemState":{"input":"item","map":{"0":"NotAvailable","1":"OutOfService","2":"NotExecuting","3":"Executing"}},
      "MachineryOperationMode":{"input":"mode","map":{"0":"None","1":"Maintenance","2":"Setup","3":"Processing"}}}}
    """;

    [Fact]
    public void EnumObject_BuildsSnapshot()
    {
        var inputs = new Dictionary<string, object?> { ["item"] = 3L, ["mode"] = 3L };
        var v = Assert.IsType<Dictionary<string, object?>>(Transforms.Apply("enumObject", inputs, P(StatusParams)));
        Assert.Equal("Executing", v["MachineryItemState"]);
        Assert.Equal("Processing", v["MachineryOperationMode"]);
    }

    [Fact]
    public void EnumObject_UnknownMemberMakesWholeValueUnavailable()
    {
        var inputs = new Dictionary<string, object?> { ["item"] = 3L, ["mode"] = 99L };
        Assert.Null(Transforms.Apply("enumObject", inputs, P(StatusParams)));
    }

    [Fact] public void EnumMap_Maps() => Assert.Equal("Interrupted", Transforms.Apply("enumMap", In(1L), P("{\"map\":{\"0\":\"Active\",\"1\":\"Interrupted\",\"2\":\"Reset\"}}")));
    [Fact] public void EnumMap_UnknownIsNull() => Assert.Null(Transforms.Apply("enumMap", In(7L), P("{\"map\":{\"0\":\"Active\"}}")));
}
