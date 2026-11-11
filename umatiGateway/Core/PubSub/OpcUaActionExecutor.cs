// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using Newtonsoft.Json;
using NLog;
using Opc.Ua;
using umatiGateway.Core.OPC;
using UaStatusCodes = Opc.Ua.StatusCodes;

namespace umatiGateway.Core.PubSub
{
    public sealed class OpcUaActionExecutor : IActionExecutor
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IOpcUaClient client;
        private readonly ActionRegistry registry;
        private readonly ActionValueConverter converter;

        public OpcUaActionExecutor(IOpcUaClient client, ActionRegistry registry)
        {
            this.client = client;
            this.registry = registry;
            converter = new ActionValueConverter(client);
        }

        public Task<ActionExecutionResult> ExecuteAsync(ActionRequest request)
        {
            try
            {
                if (!registry.TryGet(request.ActionTargetId, out ActionTargetMetadata? target) || target == null ||
                    !string.Equals(target.ObjectId, request.ObjectId, StringComparison.Ordinal) ||
                    !string.Equals(target.MethodId, request.MethodId, StringComparison.Ordinal))
                {
                    return Task.FromResult(Failure(UaStatusCodes.BadMethodInvalid));
                }

                Dictionary<string, ActionArgumentValue> suppliedArguments;
                try
                {
                    suppliedArguments = request.InputArguments.ToDictionary(argument => argument.Name, StringComparer.Ordinal);
                }
                catch (ArgumentException)
                {
                    return Task.FromResult(Failure(UaStatusCodes.BadInvalidArgument));
                }

                if (suppliedArguments.Count != target.InputArguments.Count ||
                    target.InputArguments.Any(definition => !suppliedArguments.ContainsKey(definition.Name)))
                {
                    return Task.FromResult(Failure(UaStatusCodes.BadInvalidArgument));
                }

                var inputVariants = new List<Variant>();
                foreach (ActionArgumentMetadata definition in target.InputArguments)
                {
                    ActionArgumentValue suppliedArgument = suppliedArguments[definition.Name];
                    Logger.Info(
                        "Converting action argument: Target={ActionTargetId}, Name={Name}, ExpectedDataType={ExpectedDataType}, ExpectedValueRank={ExpectedValueRank}, SuppliedDataType={SuppliedDataType}, SuppliedValueRank={SuppliedValueRank}, JsonValue={JsonValue}",
                        request.ActionTargetId,
                        definition.Name,
                        definition.DataType,
                        definition.ValueRank,
                        suppliedArgument.DataType,
                        suppliedArgument.ValueRank,
                        suppliedArgument.Value?.ToString(Formatting.None) ?? "null");

                    Variant convertedArgument = converter.ConvertInput(suppliedArgument, definition);
                    Logger.Info(
                        "Converted action argument: Target={ActionTargetId}, Name={Name}, BuiltInType={BuiltInType}, CLRType={ClrType}, OPCValue={OPCValue}",
                        request.ActionTargetId,
                        definition.Name,
                        Opc.Ua.TypeInfo.Construct(convertedArgument.Value).BuiltInType,
                        convertedArgument.Value?.GetType().FullName ?? "null",
                        DescribeValue(convertedArgument.Value));
                    inputVariants.Add(convertedArgument);
                }

                NamespaceTable namespaceTable = client.GetNamespaceTable();
                NodeId? objectId = ExpandedNodeId.ToNodeId(ExpandedNodeId.Parse(target.ObjectId), namespaceTable);
                NodeId? methodId = ExpandedNodeId.ToNodeId(ExpandedNodeId.Parse(target.MethodId), namespaceTable);
                if (objectId == null || methodId == null)
                {
                    return Task.FromResult(Failure(UaStatusCodes.BadMethodInvalid));
                }

                Logger.Info(
                    "Calling OPC UA method: Target={ActionTargetId}, ObjectId={ObjectId}, MethodId={MethodId}, InputCount={InputCount}",
                    request.ActionTargetId,
                    objectId,
                    methodId,
                    inputVariants.Count);
                OpcUaMethodCallResult callResult = client.CallMethod(objectId, methodId, inputVariants);
                Logger.Info(
                    "OPC UA method call completed: Target={ActionTargetId}, ObjectId={ObjectId}, MethodId={MethodId}, StatusCode={StatusCode}, OutputCount={OutputCount}",
                    request.ActionTargetId,
                    objectId,
                    methodId,
                    callResult.StatusCode,
                    callResult.OutputArguments.Count);
                var result = new ActionExecutionResult { StatusCode = callResult.StatusCode.Code };
                if (StatusCode.IsGood(callResult.StatusCode))
                {
                    if (callResult.OutputArguments.Count != target.OutputArguments.Count)
                    {
                        return Task.FromResult(Failure(UaStatusCodes.BadUnexpectedError));
                    }

                    for (int index = 0; index < target.OutputArguments.Count; index++)
                    {
                        result.OutputArguments.Add(converter.ConvertOutput(callResult.OutputArguments[index], target.OutputArguments[index]));
                    }
                }

                return Task.FromResult(result);
            }
            catch (ActionExecutionException ex)
            {
                Logger.Warn(ex, "Action argument conversion failed for target {ActionTargetId}.", request.ActionTargetId);
                return Task.FromResult(Failure(ex.StatusCode));
            }
            catch (ServiceResultException ex)
            {
                Logger.Warn(ex, "OPC UA method call failed for target {ActionTargetId}.", request.ActionTargetId);
                return Task.FromResult(Failure(ex.StatusCode));
            }
            catch (OpcUaException)
            {
                Logger.Warn("OPC UA connection is unavailable for action target {ActionTargetId}.", request.ActionTargetId);
                return Task.FromResult(Failure(UaStatusCodes.BadNotConnected));
            }
            catch (JsonException ex)
            {
                Logger.Warn(ex, "Action JSON conversion failed for target {ActionTargetId}.", request.ActionTargetId);
                return Task.FromResult(Failure(UaStatusCodes.BadTypeMismatch));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected failure executing action target {ActionTargetId}.", request.ActionTargetId);
                return Task.FromResult(Failure(UaStatusCodes.BadUnexpectedError));
            }
        }

        private static ActionExecutionResult Failure(uint statusCode)
        {
            return new ActionExecutionResult { StatusCode = statusCode };
        }

        private static string DescribeValue(object? value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is ExtensionObject extensionObject)
            {
                return $"ExtensionObject(TypeId={extensionObject.TypeId}, Encoding={extensionObject.Encoding}, BodyType={extensionObject.Body?.GetType().FullName ?? "null"})";
            }

            if (value is Array array)
            {
                string elementType = value.GetType().GetElementType()?.FullName ?? "unknown";
                return $"Array(ElementType={elementType}, Length={array.Length})";
            }

            return $"Value(Type={value.GetType().FullName ?? "unknown"})";
        }
    }
}
