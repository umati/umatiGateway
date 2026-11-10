// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using umatiGateway.Core.OPC;
using umatiGateway.Core.PubSub;
using Xunit;

public class ActionExecutorTests
{
    [Fact]
    public void StructuredInputOmitsOptionalNullPropertiesRecursively()
    {
        JObject input = JObject.Parse("""
            {
              "JobOrderID": "test2",
              "StartTime": null,
              "EndTime": null,
              "Nested": { "Optional": null, "Value": 1 },
              "Items": [null, { "Optional": null, "Value": 2 }]
            }
            """);

        JToken normalized = ActionValueConverter.OmitNullProperties(input);

        Assert.Null(normalized["StartTime"]);
        Assert.Null(normalized["EndTime"]);
        Assert.Null(normalized["Nested"]?["Optional"]);
        Assert.Equal(1, (int?)normalized["Nested"]?["Value"]);
        Assert.Equal(JTokenType.Null, normalized["Items"]?[0]?.Type);
        Assert.Null(normalized["Items"]?[1]?["Optional"]);
        Assert.Equal(2, (int?)normalized["Items"]?[1]?["Value"]);
        Assert.NotNull(input["StartTime"]);
    }

    [Fact]
    public void RegistryAssignsStableGlobalIds()
    {
        var later = Target("nsu=urn:test;i=20", "nsu=urn:test;i=2", "Later");
        var earlier = Target("nsu=urn:test;i=10", "nsu=urn:test;i=3", "Earlier");
        var registry = new ActionRegistry();

        registry.Rebuild(new[]
        {
            new ActionMetadata { ActionTargets = new() { later } },
            new ActionMetadata { ActionTargets = new() { earlier } }
        });

        Assert.Equal(1, earlier.ActionTargetId);
        Assert.Equal(2, later.ActionTargetId);
        Assert.True(registry.TryGet(1, out ActionTargetMetadata? registered));
        Assert.Same(earlier, registered);
    }

    [Fact]
    public async Task ExecutorOrdersArgumentsByMetadataBeforeCallingServer()
    {
        ActionTargetMetadata target = Target("nsu=urn:test;i=10", "nsu=urn:test;i=20", "Run");
        target.InputArguments.Add(new ActionArgumentMetadata { Name = "First", DataType = "String", ValueRank = -1 });
        target.InputArguments.Add(new ActionArgumentMetadata { Name = "Second", DataType = "Int32", ValueRank = -1 });
        target.InputArguments.Add(new ActionArgumentMetadata { Name = "Comment", DataType = "LocalizedText", ValueRank = 1 });
        var registry = Registry(target);
        var client = DispatchProxy.Create<IOpcUaClient, ActionClientProxy>();
        var proxy = (ActionClientProxy)client;
        var executor = new OpcUaActionExecutor(client, registry);

        ActionExecutionResult result = await executor.ExecuteAsync(new ActionRequest
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActionTargetId = target.ActionTargetId,
            ObjectId = target.ObjectId,
            MethodId = target.MethodId,
            InputArguments = new()
            {
                new ActionArgumentValue { Name = "Second", DataType = "Int32", ValueRank = -1, Value = 42 },
                new ActionArgumentValue { Name = "First", DataType = "String", ValueRank = -1, Value = "hello" },
                new ActionArgumentValue { Name = "Comment", DataType = "LocalizedText", ValueRank = 1, Value = new JArray() }
            }
        });

        Assert.Equal(StatusCodes.Good, result.StatusCode);
        Assert.Equal("hello", proxy.InputArguments![0].Value);
        Assert.Equal(42, proxy.InputArguments[1].Value);
        Assert.Empty(Assert.IsType<LocalizedText[]>(proxy.InputArguments[2].Value));
        Assert.Equal(new NodeId(10, 1), proxy.ObjectId);
        Assert.Equal(new NodeId(20, 1), proxy.MethodId);
    }

    [Fact]
    public async Task ExecutorRejectsMismatchedTargetWithoutCallingServer()
    {
        ActionTargetMetadata target = Target("nsu=urn:test;i=10", "nsu=urn:test;i=20", "Run");
        var registry = Registry(target);
        var client = DispatchProxy.Create<IOpcUaClient, ActionClientProxy>();
        var proxy = (ActionClientProxy)client;
        var executor = new OpcUaActionExecutor(client, registry);

        ActionExecutionResult result = await executor.ExecuteAsync(new ActionRequest
        {
            ActionTargetId = target.ActionTargetId,
            ObjectId = target.ObjectId,
            MethodId = "nsu=urn:test;i=999"
        });

        Assert.Equal(StatusCodes.BadMethodInvalid, result.StatusCode);
        Assert.Null(proxy.InputArguments);
    }

    private static ActionRegistry Registry(ActionTargetMetadata target)
    {
        var registry = new ActionRegistry();
        registry.Rebuild(new[] { new ActionMetadata { ActionTargets = new() { target } } });
        return registry;
    }

    private static ActionTargetMetadata Target(string objectId, string methodId, string name) => new()
    {
        Name = name,
        ObjectId = objectId,
        MethodId = methodId
    };

    public class ActionClientProxy : DispatchProxy
    {
        public NodeId? ObjectId { get; private set; }
        public NodeId? MethodId { get; private set; }
        public IList<Variant>? InputArguments { get; private set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            return method!.Name switch
            {
                nameof(IOpcUaClient.GetNamespaceTable) => NamespaceTable(),
                nameof(IOpcUaClient.CallMethod) => Call(args!),
                _ => throw new InvalidOperationException($"Unexpected server access: {method.Name}")
            };
        }

        private OpcUaMethodCallResult Call(object?[] args)
        {
            ObjectId = (NodeId)args[0]!;
            MethodId = (NodeId)args[1]!;
            InputArguments = (IList<Variant>)args[2]!;
            return new OpcUaMethodCallResult { StatusCode = StatusCodes.Good };
        }

        private static NamespaceTable NamespaceTable()
        {
            var table = new NamespaceTable();
            table.Append("urn:test");
            return table;
        }
    }
}
