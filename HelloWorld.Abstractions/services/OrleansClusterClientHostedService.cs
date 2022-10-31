using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;

namespace HelloWorld.Abstractions.services;

public class OrleansClusterClientHostedService : IHostedService
{
  private readonly ILogger<OrleansClusterClientHostedService> _logger;
  private readonly IConfiguration _configuration;
  private int _retries = 10;
  public IClusterClient Client { get; set; }

  public OrleansClusterClientHostedService(
    ILogger<OrleansClusterClientHostedService> logger,
    IConfiguration configuration)
  {
    _logger = logger;
    _configuration = configuration;

    var clientBuilder = new ClientBuilder();
    clientBuilder.Configure<ClusterOptions>(clusterOptions =>
    {
      clusterOptions.ClusterId = ConfigurationConstants.ClusterId;
      clusterOptions.ServiceId = ConfigurationConstants.ServiceId;
    });

    var storageProvider = StorageProviderUtils.GetStorageProvider(configuration);

    switch (storageProvider.Id)
    {
      case StorageProviders.MongoDb:
        clientBuilder.UseMongoDBClient(storageProvider.ConnectionString);
        clientBuilder.UseMongoDBClustering(options =>
        {
          options.DatabaseName = ConfigurationConstants.MongoClusteringDatabase;
          options.Strategy = MongoDBMembershipStrategy.SingleDocument;
        });
        break;
      case StorageProviders.AzureTableStorage:
        clientBuilder.UseAzureStorageClustering(options => options.ConfigureTableServiceClient(storageProvider.ConnectionString));
        break;
      default:
        clientBuilder.UseLocalhostClustering();
        break;
    }

    Client = clientBuilder.Build();
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Connecting...");
    if (Client.IsInitialized) return;
    await Client.Connect(async error =>
    {
      if (--_retries < 0)
      {
        _logger.LogError("Could not connect Orleans Client to the cluster: {@Message}", error.Message);
        return false;
      }
      else
      {
        _logger.LogWarning(error, "Error Connecting Orleans Client: {@Message}", error.Message);
      }

      try
      {
        await Task.Delay(1000, cancellationToken);
      }
      catch (OperationCanceledException)
      {
        return false;
      }

      return true;
    });

    _logger.LogInformation("Orleans Client Connected {Initialized}", Client.IsInitialized);
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    var cancellation = new TaskCompletionSource<bool>();
    cancellationToken.Register(() => cancellation.TrySetCanceled(cancellationToken));
    return Task.WhenAny(Client.Close(), cancellation.Task);
  }
}
