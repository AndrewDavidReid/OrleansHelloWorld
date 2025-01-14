﻿using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using System.Collections.Generic;

return await Pulumi.Deployment.RunAsync(() =>
{
  var resourceGroup = new ResourceGroup("hello-orleans-pulumi");

  // Create an Azure resource (Storage Account)
  var storageAccount = new StorageAccount("sa", new StorageAccountArgs
  {
    ResourceGroupName = resourceGroup.Name,
    Sku = new SkuArgs
    {
      Name = SkuName.Standard_LRS
    },
    Kind = Kind.StorageV2
  });

  var storageAccountKeys = ListStorageAccountKeys.Invoke(new ListStorageAccountKeysInvokeArgs
  {
    ResourceGroupName = resourceGroup.Name,
    AccountName = storageAccount.Name
  });

  var primaryStorageKey = storageAccountKeys.Apply(accountKeys =>
  {
    var firstKey = accountKeys.Keys[0].Value;
    return Output.CreateSecret(firstKey);
  });

  // Export the primary key of the Storage Account
  return new Dictionary<string, object?>
  {
    ["primaryStorageKey"] = primaryStorageKey,
  };
});
