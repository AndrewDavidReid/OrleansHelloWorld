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
      options.ClusterId = ConfigurationConstants.ClusterId;
      options.ServiceId = ConfigurationConstants.ServiceId;
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

  private static ISiloBuilder ConfigureClusteringAndGrainStorage(this ISiloBuilder siloBuilder, IConfiguration configuration)
  {
    var storageProvider = StorageProviderUtils.GetStorageProvider(configuration);

    switch (storageProvider.Id)
    {
      case StorageProviders.AzureTableStorage:
        return ConfigureAzureClusteringAndGrainStorage(siloBuilder, storageProvider.ConnectionString);
      case StorageProviders.MongoDb:
        return ConfigureMongoClusteringAndStorage(siloBuilder, storageProvider.ConnectionString);
      default:
        return ConfigureLocalhostInMemoryClusteringAndStorage(siloBuilder);
    }
  }

  private static ISiloBuilder ConfigureAzureClusteringAndGrainStorage(this ISiloBuilder siloBuilder,
    string connectionString)
  {
    siloBuilder
      .UseAzureStorageClustering(storageOptions => storageOptions.ConfigureTableServiceClient(connectionString))
      .AddAzureTableGrainStorage(ConfigurationConstants.StorageName, tableStorageOptions =>
      {
        tableStorageOptions.ConfigureTableServiceClient(connectionString);
        tableStorageOptions.UseJson = true;
      });

    return siloBuilder;
  }

  private static ISiloBuilder ConfigureMongoClusteringAndStorage(this ISiloBuilder siloBuilder, string connectionString)
  {
    siloBuilder.UseMongoDBClient(connectionString);
    siloBuilder.UseMongoDBClustering(x =>
    {
      x.Strategy = MongoDBMembershipStrategy.SingleDocument;
      x.DatabaseName = ConfigurationConstants.MongoClusteringDatabase;
      x.CreateShardKeyForCosmos = false;
    });
    siloBuilder.AddMongoDBGrainStorage(ConfigurationConstants.StorageName,
      x => { x.DatabaseName = ConfigurationConstants.MongoGrainStorageDatabase; });
    return siloBuilder;
  }

  private static ISiloBuilder ConfigureLocalhostInMemoryClusteringAndStorage(this ISiloBuilder siloBuilder)
  {
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorage(ConfigurationConstants.StorageName);
    return siloBuilder;
  }
}
