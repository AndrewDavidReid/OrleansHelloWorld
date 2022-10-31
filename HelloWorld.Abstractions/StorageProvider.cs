using Microsoft.Extensions.Configuration;

namespace HelloWorld.Abstractions;

public record StorageProvider(StorageProviders Id, string ConnectionString);

public enum StorageProviders
{
  InMemory,
  AzureTableStorage,
  MongoDb
}

public static class StorageProviderUtils
{
  public static StorageProvider GetStorageProvider(IConfiguration configuration)
  {
    var azureStorageConnectionString = configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING");
    var useAzureStorage = !string.IsNullOrEmpty(azureStorageConnectionString);
    var mongoConnectionString = configuration.GetValue<string>("MONGO_CONNECTION_STRING");
    var useMongoDb = !string.IsNullOrEmpty(mongoConnectionString);

    if (useAzureStorage)
    {
      return new StorageProvider(StorageProviders.AzureTableStorage, azureStorageConnectionString);
    }

    if (useMongoDb)
    {
      return new StorageProvider(StorageProviders.MongoDb, mongoConnectionString);
    }

    return new StorageProvider(StorageProviders.InMemory, "");
  }
}
