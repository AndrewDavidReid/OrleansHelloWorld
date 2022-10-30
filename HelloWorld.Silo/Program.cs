using System.Net;
using HelloWorld.Abstractions;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrleans(siloBuilder =>
{
  var runOnAzure = builder.Configuration.GetValue<bool>("RUN_ON_AZURE");
  var runOnAws = builder.Configuration.GetValue<bool>("RUN_ON_AWS");
  var mongoDbConnectionString = builder.Configuration.GetValue<string>("MONGO_CONNECTION_STRING");
  var useMongo = !string.IsNullOrEmpty(mongoDbConnectionString);

  siloBuilder.Configure<ClusterOptions>(options =>
  {
    options.ClusterId = "hello-orleans-cluster";
    options.ServiceId = $"hello-orleans-service";
  });
  
  if (runOnAzure)
  {
    siloBuilder.HostSiloInAzure(builder.Configuration);
  }
  else if (useMongo)
  {
    if (runOnAws)
    {
      siloBuilder.Configure<EndpointOptions>(options =>
      {
        // since we are using awsvpc each container gets its own dns and ip
        var ip = Dns.GetHostAddressesAsync(Dns.GetHostName()).Result.First();
        options.AdvertisedIPAddress = ip;
        // These 2 ports will be used by a cluster
        // for silo to silo communications
        options.SiloPort = EndpointOptions.DEFAULT_SILO_PORT;
        // Port to use for the gateway (client to silo)
        options.GatewayPort = EndpointOptions.DEFAULT_GATEWAY_PORT;
        // Internal ports which you expose to docker
        options.GatewayListeningEndpoint = new IPEndPoint(IPAddress.Any, EndpointOptions.DEFAULT_GATEWAY_PORT);
        options.SiloListeningEndpoint = new IPEndPoint(IPAddress.Any, EndpointOptions.DEFAULT_SILO_PORT);
      });
    }
    else
    {
      siloBuilder.Configure<EndpointOptions>(options =>
      {
        options.AdvertisedIPAddress = IPAddress.Loopback;
        options.SiloPort = 11111;
        options.GatewayPort = 30000;
      });
    }
    
    siloBuilder.UseMongoDBClient(mongoDbConnectionString);
    siloBuilder.UseMongoDBClustering(x =>
    {
      x.Strategy = MongoDBMembershipStrategy.SingleDocument;
      x.DatabaseName = "helloOrleansSiloClusteringDb";
      x.CreateShardKeyForCosmos = false;
    });
    siloBuilder.AddMongoDBGrainStorage("silo-grain-storage", x =>
    {
      x.DatabaseName = "helloOrleansSiloGrainStorageDb";
    });
  }
  else
  {
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorageAsDefault();
  }

  siloBuilder.UseDashboard();
});
builder.Services.AddHealthChecks();


var app = builder.Build();

app.MapHealthChecks("/healthz");

app.Run();