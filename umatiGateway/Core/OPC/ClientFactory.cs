// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Opc.Ua;
using Opc.Ua.Configuration;

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
            UmatiGatewayApp client = new UmatiGatewayApp(config, Console.Out, ClientBase.ValidateResponse)
            {
                AutoAccept = true
            };
            return client;
        }
    }
}