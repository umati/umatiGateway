
using umatiGateway.Core.OPC;

public class ConsoleService : BackgroundService
{
    private readonly ClientFactory clientFactory;

    public ConsoleService(ClientFactory clientFactory)
    {
        this.clientFactory = clientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Press Ctrl+C for exit.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5000, stoppingToken);
        }

        Console.WriteLine("Exiting...");
    }
}
