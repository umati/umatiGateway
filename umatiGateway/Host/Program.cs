// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using NLog;
using System;
using System.Net;
using umatiGateway.Core.OPC;
using umatiGateway.Hub;
using UmatiGateway;

Logger Logger = LogManager.GetCurrentClassLogger();
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    var ex = e.ExceptionObject as Exception;
    Logger.Error(ex, $"[Unhandled Exception] {ex?.Message}");
};

TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    Logger.Error(e.Exception, $"[Unobserved Task Exception] {e.Exception?.Message}");
    e.SetObserved();
};

ClientFactory clientFactory = new ClientFactory();
UmatiGatewayApp umatiGateway = clientFactory.getClient("UmatiAppClient");
if (umatiGateway.ActiveConfiguration.StartConfiguration.StartWebUI == true)
{
    var builder = WebApplication.CreateBuilder(args);
    string url = umatiGateway.ActiveConfiguration.WebUI.URL;
    if (url != "")
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
            && uriResult != null)
        {
            builder.WebHost.UseUrls(uriResult.ToString());
        }
        else
        {
            Logger.Error("Invalid formatted Uri specified: {Url}", url);
        }
    }
    // Add services to the container
    builder.Services.AddRazorPages();
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<SignalHubBridge>();
    builder.Services.AddControllers();
    builder.Services.AddSingleton<ClientFactory>(clientFactory);
    builder.Services.AddSession();
    builder.Services.AddMemoryCache();

    var app = builder.Build();
    app.Services.GetRequiredService<SignalHubBridge>();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    //app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthorization();
    app.UseSession();

    app.MapControllers();
    app.MapRazorPages();
    app.MapHub<SignalHub>("/signalHub");

    app.Run();
}
else
{
    using IHost host = Host.CreateDefaultBuilder(args)
        .ConfigureServices(services =>
        {
            services.AddSingleton(clientFactory);
            services.AddHostedService<ConsoleService>();
        })
        .Build();

    await host.RunAsync();
}
