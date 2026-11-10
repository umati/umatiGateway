// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using Newtonsoft.Json.Linq;

namespace umatiGateway.Core.PubSub
{
    public sealed class ActionRequest
    {
        public string CorrelationId { get; set; } = "";
        public ushort ActionTargetId { get; set; }
        public string ObjectId { get; set; } = "";
        public string MethodId { get; set; } = "";
        public List<ActionArgumentValue> InputArguments { get; set; } = new();
    }

    public sealed class ActionResponse
    {
        public string CorrelationId { get; set; } = "";
        public ushort ActionTargetId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public uint StatusCode { get; set; }
        public uint ActionState { get; set; }
        public List<ActionArgumentValue> OutputArguments { get; set; } = new();
    }

    public sealed class ActionMetadata
    {
        public List<ActionTargetMetadata> ActionTargets { get; set; } = new();
    }

    public sealed class ActionTargetMetadata
    {
        public ushort ActionTargetId { get; set; }
        public string Name { get; set; } = "";
        public string ObjectId { get; set; } = "";
        public string MethodId { get; set; } = "";
        public List<ActionArgumentMetadata> InputArguments { get; set; } = new();
        public List<ActionArgumentMetadata> OutputArguments { get; set; } = new();
    }

    public sealed class ActionArgumentMetadata
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "";
        public int ValueRank { get; set; }
    }

    public sealed class ActionArgumentValue
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "";
        public int ValueRank { get; set; }
        public JToken? Value { get; set; }
    }

    public sealed class ActionExecutionResult
    {
        public uint StatusCode { get; set; }
        public List<ActionArgumentValue> OutputArguments { get; set; } = new();
    }
}
