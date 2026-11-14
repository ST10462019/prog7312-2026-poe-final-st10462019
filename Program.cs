using SmartX.PoE.Api.Endpoints;
using SmartX.PoE.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<TopologyService>();

builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Configuration.GetValue("SmartX:SeedDemoData", true))
{
    DemoDataSeeder.Seed(
        app.Services.GetRequiredService<TopologyService>(),
        app.Services.GetRequiredService<TelemetryStore>());
}

app.UseCors("frontend");
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapTopologyEndpoints();
app.MapTelemetryEndpoints();

app.Run();

public partial class Program;
