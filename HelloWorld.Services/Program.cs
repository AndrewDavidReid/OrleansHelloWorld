using Bogus;
using HelloWorld.Abstractions;
using HelloWorld.Abstractions.extensions;
using Orleans;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddOrleansClusterClient(builder.Configuration);

var app = builder.Build();

app.UseRouting();
app.MapGet("/", async (IClusterClient orleansClient) =>
{
  var faker = new Faker("en");
  var grain = orleansClient.GetGrain<IHelloWorld>(faker.Name.FirstName());
  return await grain.SayHelloWorld();
});
app.MapHealthChecks("/healthz");

app.Run();
