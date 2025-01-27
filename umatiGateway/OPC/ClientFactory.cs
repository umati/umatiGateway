// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Configuration;

namespace UmatiGateway.OPC
{
    public class ClientFactory
    {
        public Dictionary<string, UmatiGatewayApp> clients = new Dictionary<string, UmatiGatewayApp>();
        UmatiGatewayApp client;
        public ClientFactory()
        {
            Console.WriteLine("Create ClientFactory");
            client = this.createClientAsync().Result;
            client.StartUp();
        }
        public UmatiGatewayApp getClient(String sessionId)
        {
            return this.client;
            /*Client? client;
            if(this.clients.TryGetValue(sessionId, out client))
            { 
                return client;
            } else
            {
                client = createClientAsync().Result;
                clients.Add(sessionId, client);
                return client;
            }*/
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
            var config = await application.LoadApplicationConfiguration("./OPC/Gateway.Config.xml", silent: false);
            await application.CheckApplicationInstanceCertificate(silent: false, minimumKeySize: 0);
            UmatiGatewayApp client = new UmatiGatewayApp(config, Console.Out, ClientBase.ValidateResponse)
            {
                AutoAccept = true
            };
            return client;
        }
    }
}