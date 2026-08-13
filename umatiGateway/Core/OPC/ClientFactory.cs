// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
// Copyright (c) 2026 Verein Deutscher Werkzeugmaschinenfabriken e.V. . All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Opc.Ua;
using Opc.Ua.Configuration;

#pragma warning disable CS0618

namespace umatiGateway.Core.OPC
{
    public class ClientFactory
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public Dictionary<string, UmatiGatewayApp> clients = new Dictionary<string, UmatiGatewayApp>();
        UmatiGatewayApp client;
        public ClientFactory()
        {
            Logger.Info("Create ClientFactory");
            client = createClientAsync().Result;
            client.StartUp();
        }
        public UmatiGatewayApp getClient(string sessionId)
        {
            return client;
        }
        public UmatiGatewayApp getClient()
        {
            return client;
        }

        private async Task<UmatiGatewayApp> createClientAsync()
        {
            var configSectionName = "GateWay";
            CertificatePasswordProvider PasswordProvider = new CertificatePasswordProvider(null);
            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "GatewayClient",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = configSectionName,
                CertificatePasswordProvider = PasswordProvider
            };
            var config = await application.LoadApplicationConfiguration("./Core/OPC/Gateway.Config.xml", silent: false);
            await application.CheckApplicationInstanceCertificates(silent: false);
            UmatiGatewayApp client = new UmatiGatewayApp(config, Console.Out, ValidateOpcUaResponse);
            return client;
        }
        private static void ValidateOpcUaResponse(IList responses, IList requests)
        {
            ClientBase.ValidateResponse<object, object>(
                responses == null ? null! : responses.Cast<object>().ToArray(),
                requests == null ? null! : requests.Cast<object>().ToArray()
            );
        }
    }
}

#pragma warning enable CS0618