// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

using System.Buffers;
using System.Text;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using umatiGateway.Core.Configuration;

namespace umatiGateway.Core.PubSub
{
    public sealed class MqttActionTransport : IActionMqttTransport
    {
        private readonly MqttClientFactory mqttFactory = new MqttClientFactory();
        private IMqttClient? mqttClient;
        private Func<string, string, Task>? messageHandler;

        public async Task StartAsync(PubSubProviderConfig config, string topicFilter, Func<string, string, Task> messageHandler)
        {
            await StopAsync().ConfigureAwait(false);

            this.messageHandler = messageHandler;
            mqttClient = mqttFactory.CreateMqttClient();
            mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

            MqttClientOptions options = CreateOptions(config);
            await mqttClient.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
            await mqttClient.SubscribeAsync(topicFilter, MqttQualityOfServiceLevel.AtMostOnce, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task PublishAsync(string topic, string payload, bool retain = false)
        {
            if (mqttClient == null || !mqttClient.IsConnected)
            {
                throw new InvalidOperationException("The MQTT action transport is not connected.");
            }

            MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .WithRetainFlag(retain)
                .Build();

            await mqttClient.PublishAsync(message, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task StopAsync()
        {
            IMqttClient? client = mqttClient;
            mqttClient = null;
            messageHandler = null;

            if (client == null)
            {
                return;
            }

            client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
            if (client.IsConnected)
            {
                await client.DisconnectAsync().ConfigureAwait(false);
            }

            client.Dispose();
        }

        private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            Func<string, string, Task>? handler = messageHandler;
            if (handler == null)
            {
                return;
            }

            string payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload.ToArray());
            await handler(args.ApplicationMessage.Topic, payload).ConfigureAwait(false);
        }

        private static MqttClientOptions CreateOptions(PubSubProviderConfig config)
        {
            if (!Uri.TryCreate(config.ServerEndpoint, UriKind.Absolute, out Uri? endpoint))
            {
                throw new ArgumentException($"Invalid MQTT server endpoint '{config.ServerEndpoint}'.", nameof(config));
            }

            var builder = new MqttClientOptionsBuilder()
                .WithProtocolVersion(MqttProtocolVersion.V500);

            if (endpoint.Scheme is "ws" or "wss")
            {
                builder.WithWebSocketServer(options => options.WithUri(endpoint.ToString()));
            }
            else if (endpoint.Scheme is "mqtt" or "mqtts")
            {
                int port = endpoint.IsDefaultPort ? (endpoint.Scheme == "mqtts" ? 8883 : 1883) : endpoint.Port;
                builder.WithTcpServer(endpoint.Host, port);
            }
            else
            {
                throw new ArgumentException($"Unsupported MQTT endpoint scheme '{endpoint.Scheme}'.", nameof(config));
            }

            if (!string.IsNullOrEmpty(config.UserName))
            {
                builder.WithCredentials(config.UserName, config.Password);
            }

            if (endpoint.Scheme is "mqtts" or "wss")
            {
                builder.WithTlsOptions(options =>
                {
                    options.UseTls(true);
                    if (config.AllowUntrustedCertificates)
                    {
                        options.WithCertificateValidationHandler(_ => true);
                    }
                });
            }

            return builder.Build();
        }
    }
}
