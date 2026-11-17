using UmatiGateway.Api;
using UmatiGateway.Gateway;
using UmatiGateway.Output;

var builder = WebApplication.CreateBuilder(args);

// listen on all interfaces, port 8080, unless ASPNETCORE_URLS / --urls says otherwise
if (string.IsNullOrEmpty(builder.Configuration["urls"]) && string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
    builder.WebHost.UseUrls("http://0.0.0.0:8080");

builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Services.AddSingleton<MessageHub>();
builder.Services.AddSingleton<GatewayService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GatewayService>());
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DictionaryKeyPolicy = null; // keep data-model paths untouched
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGatewayApi();
app.Run();
