// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using umatiGateway.Core.Configuration;

namespace umatiGateway.Core.PubSub
{
    public interface IActionMqttTransport
    {
        Task StartAsync(PubSubProviderConfig config, string topicFilter, Func<string, string, Task> messageHandler);
        Task PublishAsync(string topic, string payload, bool retain = false);
        Task StopAsync();
    }
}
