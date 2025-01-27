// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UmatiGateway.OPC;

namespace UmatiGateway
{
    [Route("api/SSE")]
    [ApiController]
    public class SSEController : ControllerBase, UmatiGatewayAppListener
    {
        private readonly BlockingCollection<string> UpdateQueue = new BlockingCollection<string>();
        public SSEController(ClientFactory ClientFactory)
        {
            UmatiGatewayApp app = this.getClient(ClientFactory);
            app.AddUmatiGatewayAppListener(this);
        }
        private UmatiGatewayApp getClient(ClientFactory clientFactory)
        {
            UmatiGatewayApp client = clientFactory.getClient(Guid.NewGuid().ToString());
            return client;
        }

        [HttpGet("events")]
        public async Task<IActionResult> GetEvents()
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            try
            {
                // Loop indefinitely, checking for updates
                foreach (var update in UpdateQueue.GetConsumingEnumerable())
                {
                    if (Response.HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        break; // Exit if the client disconnects
                    }

                    var message = $"data: {update}\n\n";
                    await Response.WriteAsync(message);
                    await Response.Body.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SSE connection: {ex.Message}");
            }

            return new EmptyResult();
        }
        public void blockingTransitionChanged(BlockingTransition blockingTransition)
        {
            var bt = new
            {
                transition = blockingTransition.Transition,
                message = blockingTransition.Message,
                detail = blockingTransition.Detail,
                isBlocking = blockingTransition.isBlocking
            };
            string jsonData = System.Text.Json.JsonSerializer.Serialize(bt);
            UpdateQueue.Add(jsonData); // Add update to the queue
        }
    }
}
