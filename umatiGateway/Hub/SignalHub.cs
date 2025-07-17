namespace umatiGateway.Hub
{
    using Microsoft.AspNetCore.SignalR;
    using System;
    using System.Threading.Tasks;

    public class SignalHub : Hub
    {
        public async Task Echo(string message)
        {
            await Clients.All.SendAsync("WertAktualisiert", $"Echo vom Server: {message}");
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("WertAktualisiert", "Verbunden mit dem SignalHub");
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            // Optional: Logging oder Bereinigung
            return base.OnDisconnectedAsync(exception);
        }
    }
}
