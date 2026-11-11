// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json.Linq;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.PubSub;
using Xunit;

public class ActionHandlerTests
{
    [Fact]
    public async Task ValidRequestPublishesCorrelatedDefaultResponse()
    {
        var transport = new FakeActionTransport();
        var executor = new FakeActionExecutor();
        var timestamp = new DateTimeOffset(2026, 11, 9, 12, 30, 0, TimeSpan.Zero);
        var handler = new ActionHandler(transport, executor, () => timestamp);
        string requestTopic = "opcua/umati/v3/json/action-request/company/client";
        string requestId = "3c6f5c8e-2b5a-4c49-98dd-0df6d9e1a0a1";

        await handler.ProcessAsync(requestTopic, $$"""
            {
              "CorrelationId": "{{requestId}}",
              "ActionTargetId": 1,
              "ObjectId": "nsu=http://example.com/UA/;i=1001",
              "MethodId": "nsu=http://example.com/UA/;i=7001",
              "InputArguments": []
            }
            """);

        Assert.Single(transport.PublishedMessages);
        var published = transport.PublishedMessages[0];
        Assert.False(published.Retain);
        Assert.Equal("opcua/umati/v3/json/action-response/company/client", published.Topic);
        JObject response = JObject.Parse(published.Payload);
        Assert.Equal(1, (int?)response["ActionTargetId"]);
        Assert.Equal(requestId, (string?)response["CorrelationId"]);
        Assert.Equal(timestamp, (DateTimeOffset?)response["Timestamp"]);
        Assert.Equal(0u, (uint?)response["StatusCode"]);
        Assert.Equal(2u, (uint?)response["ActionState"]);
        Assert.Empty((JArray)response["OutputArguments"]!);
        Assert.NotNull(executor.Request);
        Assert.Equal("nsu=http://example.com/UA/;i=7001", executor.Request!.MethodId);
    }

    [Fact]
    public void CreatesConfiguredRequestFilter()
    {
        var config = new PubSubProviderConfig { Prefix = "/umati/v3/", ClientId = "/company/client/ChocoCuttingTable/" };

        Assert.Equal("opcua/umati/v3/json/action-request/company/client", ActionHandler.CreateRequestTopicFilter(config));
    }

    [Fact]
    public void CreatesMetadataTopicForOwningObject()
    {
        var config = new PubSubProviderConfig { Prefix = "/umati/v3/", ClientId = "/gateway-1/" };

        string topic = ActionHandler.CreateMetadataTopic(
            config,
            "/Enterprise:Site:Area:Line:Cell/machines/Press/Control/");

        Assert.Equal(
            "opcua/umati/v3/json/action-metadata/gateway-1/Enterprise:Site:Area:Line:Cell/machines/Press/Control",
            topic);
    }

    [Fact]
    public async Task PublishesActionMetadataWithMethodSignature()
    {
        var transport = new FakeActionTransport();
        var handler = new ActionHandler(transport, new FakeActionExecutor());
        var metadata = new ActionMetadata
        {
            ActionTargets = new()
            {
                new ActionTargetMetadata
                {
                    ActionTargetId = 1,
                    Name = "Reset",
                    ObjectId = "nsu=http://example.com/UA/;i=1001",
                    MethodId = "nsu=http://example.com/UA/;i=7001",
                    InputArguments = new()
                    {
                        new ActionArgumentMetadata { Name = "Command", DataType = "String", ValueRank = -1 }
                    }
                },
                new ActionTargetMetadata
                {
                    ActionTargetId = 2,
                    Name = "Start",
                    ObjectId = "nsu=http://example.com/UA/;i=1001",
                    MethodId = "nsu=http://example.com/UA/;i=7002"
                }
            }
        };

        await handler.PublishMetadataAsync("opcua/umati/v3/json/action-metadata/gateway-1/machines/Press", metadata);

        Assert.Single(transport.PublishedMessages);
        Assert.True(transport.PublishedMessages[0].Retain);
        JObject payload = JObject.Parse(transport.PublishedMessages[0].Payload);
        Assert.Equal(2, payload["ActionTargets"]?.Count());
        Assert.Equal(1, (int?)payload["ActionTargets"]?[0]?["ActionTargetId"]);
        Assert.Equal("Reset", (string?)payload["ActionTargets"]?[0]?["Name"]);
        Assert.Equal(metadata.ActionTargets[0].ObjectId, (string?)payload["ActionTargets"]?[0]?["ObjectId"]);
        Assert.Equal(metadata.ActionTargets[0].MethodId, (string?)payload["ActionTargets"]?[0]?["MethodId"]);
        Assert.Equal("Command", (string?)payload["ActionTargets"]?[0]?["InputArguments"]?[0]?["Name"]);
        Assert.Equal("String", (string?)payload["ActionTargets"]?[0]?["InputArguments"]?[0]?["DataType"]);
        Assert.Equal(-1, (int?)payload["ActionTargets"]?[0]?["InputArguments"]?[0]?["ValueRank"]);
        Assert.Empty((JArray)payload["ActionTargets"]?[0]?["OutputArguments"]!);
        Assert.Equal("Start", (string?)payload["ActionTargets"]?[1]?["Name"]);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"ActionTargetId\":\"target\"}")]
    [InlineData("{\"ActionTargetId\":1,\"CorrelationId\":\"not-a-guid\",\"ObjectId\":\"nsu=urn:test;i=1\",\"MethodId\":\"nsu=urn:test;i=2\",\"InputArguments\":[]}")]
    public async Task InvalidRequestDoesNotPublish(string payload)
    {
        var transport = new FakeActionTransport();
        var handler = new ActionHandler(transport, new FakeActionExecutor());

        await handler.ProcessAsync("opcua/umati/v3/json/action-request/company/client", payload);

        Assert.Empty(transport.PublishedMessages);
    }

    [Fact]
    public async Task CorrelatableInvalidRequestPublishesBadInvalidArgument()
    {
        var transport = new FakeActionTransport();
        var executor = new FakeActionExecutor();
        var handler = new ActionHandler(transport, executor);

        await handler.ProcessAsync("opcua/umati/v3/json/action-request/company/client", """
            {
              "CorrelationId": "60a943fe-0c33-407b-bf5f-8ae057396f1c",
              "ActionTargetId": 4
            }
            """);

        Assert.Single(transport.PublishedMessages);
        JObject response = JObject.Parse(transport.PublishedMessages[0].Payload);
        Assert.Equal(Opc.Ua.StatusCodes.BadInvalidArgument, (uint?)response["StatusCode"]);
        Assert.Null(executor.Request);
    }

    [Fact]
    public async Task CurrentStructuredJobOrderRequestIsForwardedToExecutor()
    {
        var transport = new FakeActionTransport();
        var executor = new FakeActionExecutor();
        var handler = new ActionHandler(transport, executor);

        await handler.ProcessAsync("opcua/umati/v3/json/action-request/company/client", """
            {
              "CorrelationId": "c93c97c3-8ad3-465d-a1a2-24746d8e7b53",
              "ActionTargetId": 4,
              "ObjectId": "nsu=urn:de.interop4X.opcua.choco_cutting_table;i=1027",
              "MethodId": "nsu=urn:de.interop4X.opcua.choco_cutting_table;i=1046",
              "InputArguments": [
                {
                  "Name": "JobOrder",
                  "DataType": "nsu=http://opcfoundation.org/UA/ISA95-JOBCONTROL_V2/;i=3008",
                  "ValueRank": -1,
                  "Value": { "JobOrderID": "asdf", "Description": [], "StartTime": null }
                },
                {
                  "Name": "Comment",
                  "DataType": "LocalizedText",
                  "ValueRank": 1,
                  "Value": []
                }
              ]
            }
            """);

        Assert.NotNull(executor.Request);
        Assert.Equal(2, executor.Request!.InputArguments.Count);
        Assert.Equal("asdf", (string?)executor.Request.InputArguments[0].Value?["JobOrderID"]);
        Assert.Empty((JArray)executor.Request.InputArguments[1].Value!);
        Assert.Single(transport.PublishedMessages);
    }

    [Fact]
    public async Task RepeatedStartAndStopAreDelegatedWithoutDuplicateHandlers()
    {
        var transport = new FakeActionTransport();
        var handler = new ActionHandler(transport, new FakeActionExecutor());
        var config = new PubSubProviderConfig { Prefix = "umati/v3", ClientId = "company/client/ChocoCuttingTable" };

        await handler.StartAsync(config);
        await handler.StopAsync();
        await handler.StartAsync(config);

        Assert.Equal(2, transport.StartCount);
        Assert.Equal(1, transport.StopCount);
        Assert.Equal("opcua/umati/v3/json/action-request/company/client", transport.TopicFilter);
    }

    private sealed class FakeActionTransport : IActionMqttTransport
    {
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public string? TopicFilter { get; private set; }
        public List<(string Topic, string Payload, bool Retain)> PublishedMessages { get; } = new();

        public Task StartAsync(PubSubProviderConfig config, string topicFilter, Func<string, string, Task> messageHandler)
        {
            StartCount++;
            TopicFilter = topicFilter;
            return Task.CompletedTask;
        }

        public Task PublishAsync(string topic, string payload, bool retain = false)
        {
            PublishedMessages.Add((topic, payload, retain));
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeActionExecutor : IActionExecutor
    {
        public ActionRequest? Request { get; private set; }

        public Task<ActionExecutionResult> ExecuteAsync(ActionRequest request)
        {
            Request = request;
            return Task.FromResult(new ActionExecutionResult { StatusCode = Opc.Ua.StatusCodes.Good });
        }
    }
}
