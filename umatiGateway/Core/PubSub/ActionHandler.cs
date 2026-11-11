// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using Opc.Ua;
using umatiGateway.Core.Configuration;
using UaStatusCodes = Opc.Ua.StatusCodes;

namespace umatiGateway.Core.PubSub
{
    public sealed class ActionHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string RequestDirection = "/action-request/";
        private const string ResponseDirection = "/action-response/";
        private readonly IActionMqttTransport transport;
        private readonly IActionExecutor executor;
        private readonly Func<DateTimeOffset> utcNow;

        public ActionHandler(IActionMqttTransport transport, IActionExecutor executor, Func<DateTimeOffset>? utcNow = null)
        {
            this.transport = transport;
            this.executor = executor;
            this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public static string CreateRequestTopicFilter(PubSubProviderConfig config)
        {
            string prefix = config.Prefix.Trim('/');
            (string responder, string writerGroup) = GetResponderAndWriterGroup(config.ClientId);
            return $"opcua/{prefix}/json/action-request/{responder}/{writerGroup}";
        }

        public static string CreateMetadataTopic(PubSubProviderConfig config, string browsePath)
        {
            string prefix = config.Prefix.Trim('/');
            string clientId = config.ClientId.Trim('/');
            string path = browsePath.Trim('/');
            return $"opcua/{prefix}/json/action-metadata/{clientId}/{path}";
        }

        public Task StartAsync(PubSubProviderConfig config)
        {
            return transport.StartAsync(config, CreateRequestTopicFilter(config), ProcessAsync);
        }

        public Task StopAsync()
        {
            return transport.StopAsync();
        }

        public async Task PublishMetadataAsync(string topic, ActionMetadata metadata)
        {
            string payload = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            await transport.PublishAsync(topic, payload, retain: true).ConfigureAwait(false);
            Logger.Info("Published MQTT action metadata on {Topic}: {Payload}", topic, payload);
        }

        public async Task ProcessAsync(string topic, string payload)
        {
            Logger.Info("Received MQTT action request on {Topic}: {Payload}", topic, payload);
            Console.WriteLine($"Received MQTT action request on {topic}: {payload}");

            string? responseTopic = CreateResponseTopic(topic);
            if (responseTopic == null)
            {
                Logger.Warn("Ignoring MQTT action request with unexpected topic {Topic}.", topic);
                return;
            }

            if (!TryReadCorrelation(payload, out JObject? json, out string? correlationId))
            {
                Logger.Warn("Ignoring MQTT action request without a valid CorrelationId on {Topic}.", topic);
                return;
            }

            ushort actionTargetId = ReadActionTargetId(json!);
            ActionExecutionResult executionResult;
            ActionRequest? request = null;
            if (!TryDeserializeRequest(json!, out request, out string error))
            {
                Logger.Warn("Rejecting invalid MQTT action request on {Topic}: {Error}", topic, error);
                executionResult = new ActionExecutionResult { StatusCode = UaStatusCodes.BadInvalidArgument };
            }
            else
            {
                executionResult = await executor.ExecuteAsync(request!).ConfigureAwait(false);
            }

            var response = new ActionResponse
            {
                CorrelationId = correlationId!,
                ActionTargetId = actionTargetId,
                Timestamp = utcNow(),
                StatusCode = executionResult.StatusCode,
                ActionState = 2,
                OutputArguments = executionResult.OutputArguments
            };

            string responsePayload = JsonConvert.SerializeObject(response, Formatting.Indented);
            await transport.PublishAsync(responseTopic, responsePayload, retain: false).ConfigureAwait(false);
            Logger.Info("Published default MQTT action response on {Topic}: {Payload}", responseTopic, responsePayload);
        }

        public static string? CreateResponseTopic(string requestTopic)
        {
            int directionIndex = requestTopic.IndexOf(RequestDirection, StringComparison.Ordinal);
            if (directionIndex < 0)
            {
                return null;
            }

            return string.Concat(
                requestTopic.AsSpan(0, directionIndex),
                ResponseDirection,
                requestTopic.AsSpan(directionIndex + RequestDirection.Length));
        }

        private static bool TryReadCorrelation(string payload, out JObject? json, out string? correlationId)
        {
            json = null;
            correlationId = null;
            try
            {
                json = JObject.Parse(payload);
                correlationId = json.Value<string>(nameof(ActionRequest.CorrelationId));
                return Guid.TryParse(correlationId, out _);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static ushort ReadActionTargetId(JObject json)
        {
            long? value = json.Value<long?>(nameof(ActionRequest.ActionTargetId));
            return value is >= ushort.MinValue and <= ushort.MaxValue ? (ushort)value.Value : (ushort)0;
        }

        private static bool TryDeserializeRequest(JObject json, out ActionRequest? request, out string error)
        {
            request = null;
            try
            {
                if (json[nameof(ActionRequest.ActionTargetId)]?.Type != JTokenType.Integer)
                {
                    error = "ActionTargetId is missing or is not a UInt16.";
                    return false;
                }

                long actionTargetId = json.Value<long>(nameof(ActionRequest.ActionTargetId));
                if (actionTargetId < ushort.MinValue || actionTargetId > ushort.MaxValue)
                {
                    error = "ActionTargetId is outside the UInt16 range.";
                    return false;
                }

                if (json[nameof(ActionRequest.ObjectId)]?.Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace(json.Value<string>(nameof(ActionRequest.ObjectId))))
                {
                    error = "ObjectId is missing or empty.";
                    return false;
                }

                if (json[nameof(ActionRequest.MethodId)]?.Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace(json.Value<string>(nameof(ActionRequest.MethodId))))
                {
                    error = "MethodId is missing or empty.";
                    return false;
                }

                if (json[nameof(ActionRequest.InputArguments)]?.Type != JTokenType.Array)
                {
                    error = "InputArguments is missing or is not an array.";
                    return false;
                }

                request = json.ToObject<ActionRequest>();
                if (request == null)
                {
                    error = "Request payload could not be deserialized.";
                    return false;
                }

                error = "";
                return true;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (OverflowException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static (string Responder, string WriterGroup) GetResponderAndWriterGroup(string clientId)
        {
            string[] segments = clientId.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length < 2)
            {
                throw new ArgumentException("The PubSub clientId must contain responder and writer group separated by '/'.", nameof(clientId));
            }

            return (segments[0], segments[1]);
        }
    }
}
