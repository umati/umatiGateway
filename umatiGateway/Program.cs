using UmatiGateway;
using UmatiGateway.OPC;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
var ClientFactory = new ClientFactory();
builder.Services.AddSingleton<ClientFactory>(ClientFactory);
SSEController sseController = new SSEController(ClientFactory);
builder.Services.AddSingleton<UmatiGateway.SSEController>(sseController);
builder.Services.AddSession();
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseSession();

app.MapControllers();
app.MapRazorPages();

app.Run();
