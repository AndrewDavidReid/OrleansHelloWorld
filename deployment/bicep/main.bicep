param appName string = 'hello-orleans'
param location string = resourceGroup().location

resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2021-03-15' = {
  name: '${appName}-db'
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    enableFreeTier: true
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
      maxStalenessPrefix: 1
      maxIntervalInSeconds: 5
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
      }
    ]
    databaseAccountOfferType: 'Standard'
    enableAutomaticFailover: true
    capabilities: [
      {
        name: 'EnableTable'
      }
    ]
  }
}

var key = listKeys(cosmosDbAccount.name, cosmosDbAccount.apiVersion).primaryMasterKey
var protocol = 'DefaultEndpointsProtocol=https'
var accountBits = 'AccountName=${cosmosDbAccount.name};AccountKey=${key}'
var endpointSuffix = 'TableEndpoint=https://${cosmosDbAccount.name}.table.cosmos.azure.com:443/;'

var cosmosConnectionString = '${protocol};${accountBits};${endpointSuffix}'

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: '${appName}-op-insights'
  location: location
  properties: {
    retentionInDays: 30
    features: {
      searchVersion: 1
    }
    sku: {
      name: 'PerGB2018'
    }
  }
}

var appInsightsInstrumentationKey = appInsights.properties.InstrumentationKey
var appInsightsConnectionString = appInsights.properties.ConnectionString

resource vnet 'Microsoft.Network/virtualNetworks@2021-05-01' = {
  name: '${appName}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '172.17.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '172.17.0.0/24'
          delegations: [
            {
              name: 'delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
    ]
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: '${appName}-app-service-plan'
  location: location
  kind: 'app'
  sku: {
    name: 'S1'
    capacity: 2
  }
}

resource orleansSilo 'Microsoft.Web/sites@2021-01-15' = {
  name: '${appName}-silo'
  location: location
  kind: 'app'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    virtualNetworkSubnetId: vnet.properties.subnets[0].id
    siteConfig: {
      vnetPrivatePortsCount: 2
      webSocketsEnabled: true
      netFrameworkVersion: 'v6.0'
      alwaysOn: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'RUN_ON_AZURE_APP_SERVICE'
          value: 'true'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'AZURE_STORAGE_CONNECTION_STRING'
          value: cosmosConnectionString
        }
      ]
    }
  }
}

resource orleansSiloConfig 'Microsoft.Web/sites/config@2021-03-01' = {
  name: '${orleansSilo.name}/metadata'
  properties: {
    CURRENT_STACK: 'dotnet'
  }
}

resource webApi 'Microsoft.Web/sites@2021-01-15' = {
  name: '${appName}-api'
  location: location
  kind: 'app'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    virtualNetworkSubnetId: vnet.properties.subnets[0].id
    siteConfig: {
      webSocketsEnabled: true
      netFrameworkVersion: 'v6.0'
      alwaysOn: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'RUN_ON_AZURE_APP_SERVICE'
          value: 'true'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'AZURE_STORAGE_CONNECTION_STRING'
          value: cosmosConnectionString
        }
      ]
    }
  }
}

resource webApiConfig 'Microsoft.Web/sites/config@2021-03-01' = {
  name: '${webApi.name}/metadata'
  properties: {
    CURRENT_STACK: 'dotnet'
  }
}
