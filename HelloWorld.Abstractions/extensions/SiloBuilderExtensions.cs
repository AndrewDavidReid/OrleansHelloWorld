using System.Net;
using HelloWorld.Abstractions;
using Microsoft.Extensions.Configuration;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;

namespace HelloWorld.Abstractions.extensions;

public static class SiloBuilderExtensions
{
  public static ISiloBuilder ConfigureCluster(this ISiloBuilder siloBuilder, IConfiguration configuration)
  {
    siloBuilder.Configure<ClusterOptions>(options =>
    {
      options.ClusterId = "hello-orleans-cluster";
      options.ServiceId = "hello-orleans-service";
    });

    siloBuilder.Configure<SiloOptions>(options => options.SiloName = "hello-orleans-silo");
    siloBuilder.ConfigureClusterEndpoints(configuration);
    siloBuilder.ConfigureClusteringAndGrainStorage(configuration);

    return siloBuilder;
  }

  private static ISiloBuilder ConfigureClusterEndpoints(this ISiloBuilder siloBuilder, IConfiguration configuration)
  {
    var runtimeEnvironment = DetermineRunningEnvironment(configuration);

    switch (runtimeEnvironment)
    {
      case RuntimeEnvironments.AzureAppService:
        return ConfigureAzureAppServiceEndpoints(siloBuilder, configuration);
      case RuntimeEnvironments.AwsEcs:
        return ConfigureAwsEcsEndpoints(siloBuilder);
      default:
        return ConfigureLocalhostEndpoints(siloBuilder);
    }
  }

  private static ISiloBuilder ConfigureAzureAppServiceEndpoints(this ISiloBuilder siloBuilder,
    IConfiguration configuration)
  {
    var privateIp = configuration.GetValue<string>("WEBSITE_PRIVATE_IP");
    var privatePorts = configuration.GetValue<string>("WEBSITE_PRIVATE_PORTS");

    var endpointAddress = IPAddress.Parse(privateIp);
    var strPorts = privatePorts.Split(',');

    if (strPorts.Length < 2)
    {
      throw new Exception("Insufficient private ports configured.");
    }

    var siloPort = int.Parse(strPorts[0]);
    var gatewayPort = int.Parse(strPorts[1]);

    siloBuilder.ConfigureEndpoints(endpointAddress, siloPort, gatewayPort);

    return siloBuilder;
  }

  private static ISiloBuilder ConfigureAwsEcsEndpoints(this ISiloBuilder siloBuilder)
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

    return siloBuilder;
  }

  private static ISiloBuilder ConfigureLocalhostEndpoints(this ISiloBuilder siloBuilder)
  {
    siloBuilder.Configure<EndpointOptions>(options =>
    {
      options.AdvertisedIPAddress = IPAddress.Loopback;
      options.SiloPort = 11111;
      options.GatewayPort = 30000;
    });

    return siloBuilder;
  }

  private static RuntimeEnvironments DetermineRunningEnvironment(IConfiguration configuration)
  {
    var runOnAzureAppService = configuration.GetValue<bool>("RUN_ON_AZURE_APP_SERVICE");
    var runOnAwsEcs = configuration.GetValue<bool>("RUN_ON_AWS_ECS");

    if (runOnAzureAppService)
    {
      return RuntimeEnvironments.AzureAppService;
    }

    if (runOnAwsEcs)
    {
      return RuntimeEnvironments.AwsEcs;
    }

    return RuntimeEnvironments.Local;
  }

  private static StorageProviders DetermineStorageProvider(IConfiguration configuration)
  {
    var useAzureStorage = !string.IsNullOrEmpty(configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING"));
    var useMongoDb = !string.IsNullOrEmpty(configuration.GetValue<string>("MONGO_CONNECTION_STRING"));

    if (useAzureStorage)
    {
      return StorageProviders.AzureTableStorage;
    }

    if (useMongoDb)
    {
      return StorageProviders.MongoDb;
    }

    return StorageProviders.InMemory;
  }

  private static ISiloBuilder ConfigureClusteringAndGrainStorage(this ISiloBuilder siloBuilder, IConfiguration configuration)
  {
    var storageProvider = DetermineStorageProvider(configuration);

    switch (storageProvider)
    {
      case StorageProviders.AzureTableStorage:
        return ConfigureAzureClusteringAndGrainStorage(siloBuilder, configuration);
      case StorageProviders.MongoDb:
        return ConfigureMongoClusteringAndStorage(siloBuilder, configuration);
      default:
        return ConfigureLocalhostInMemoryClusteringAndStorage(siloBuilder);
    }
  }

  private static ISiloBuilder ConfigureAzureClusteringAndGrainStorage(this ISiloBuilder siloBuilder,
    IConfiguration configuration)
  {
    var connectionString = configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING");

    siloBuilder
      .UseAzureStorageClustering(storageOptions => storageOptions.ConfigureTableServiceClient(connectionString))
      .AddAzureTableGrainStorage(ConfigurationConstants.StorageName, tableStorageOptions =>
      {
        tableStorageOptions.ConfigureTableServiceClient(connectionString);
        tableStorageOptions.UseJson = true;
      });

    return siloBuilder;
  }

  private static ISiloBuilder ConfigureMongoClusteringAndStorage(this ISiloBuilder siloBuilder, IConfiguration configuration)
  {
    var mongoConnectionString = configuration.GetValue<string>("MONGO_CONNECTION_STRING");
    siloBuilder.UseMongoDBClient(mongoConnectionString);
    siloBuilder.UseMongoDBClustering(x =>
    {
      x.Strategy = MongoDBMembershipStrategy.SingleDocument;
      x.DatabaseName = "helloOrleansSiloClusteringDb";
      x.CreateShardKeyForCosmos = false;
    });
    siloBuilder.AddMongoDBGrainStorage("silo-grain-storage",
      x => { x.DatabaseName = "helloOrleansSiloGrainStorageDb"; });
    return siloBuilder;
  }

  private static ISiloBuilder ConfigureLocalhostInMemoryClusteringAndStorage(this ISiloBuilder siloBuilder)
  {
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorageAsDefault();
    return siloBuilder;
  }
}
