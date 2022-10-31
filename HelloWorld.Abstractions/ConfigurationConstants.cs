namespace HelloWorld.Abstractions;

public static class ConfigurationConstants
{
  public const string StorageName = "HelloOrleansGrainStore";
  public const string ClusterId = "hello-orleans-cluster";
  public const string ServiceId = "hello-orleans-service";

  // mongo specific
  public const string MongoClusteringDatabase = "HelloOrleansSiloClusteringDb";
  public const string MongoGrainStorageDatabase = "HelloOrleansSiloGrainStorageDb";
}
