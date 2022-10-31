using HelloWorld.Abstractions.extensions;
using Orleans;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrleans(siloBuilder =>
{
  siloBuilder.ConfigureCluster(builder.Configuration);
  siloBuilder.UseDashboard();
});
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/healthz");
app.Run();
