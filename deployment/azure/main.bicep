@description('Name of the storage account (must be globally unique)')
param storageAccountName string = 'sdcbchatsstorage1069'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Name of the file share')
param fileShareName string = 'sdcbchatsfileshare'

@description('Name for the Container Apps Environment')
param envName string = 'ca-env'

@description('Name for the Container App')
param containerAppName string = 'sdcbchats-app'

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
  }
}

// Storage Share
resource fileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2022-09-01' = {
  name: '${storageAccount.name}/default/${fileShareName}'
  properties: {
    shareQuota: 100 // in GB
    accessTier: 'TransactionOptimized'
  }
}

// Log Analytics for Container Apps Environment
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${envName}-log'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Container Apps Environment
resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: envName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// Get Storage Key for mounting File Share
var storageAccountKey = storageAccount.listKeys().keys[0].value

resource meStorage 'Microsoft.App/managedEnvironments/storages@2023-05-01' = {
  parent: containerAppEnv
  name: '${storageAccountName}fileshare'
  properties: {
    azureFile: {
      accountName: storageAccountName
      shareName: fileShareName
      accountKey: storageAccountKey
      accessMode: 'ReadWrite'
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      secrets: [
        {
          name: 'storage-account-key'
          value: storageAccountKey
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'sdcbchats-app'
          image: 'docker.io/sdcb/chats:latest'
          resources: {
            cpu: 2
            memory: '4Gi'
          }
          env: [
            {
              name: 'DBType'
              value: 'sqlite'
            }
            {
              name: 'ConnectionStrings__ChatsDB'
              value: 'Data Source=./AppData/chats.db'
            }
            // {
            //   name: 'JwtSecretKey'
            //   value: 'your_jwt_secret_key_here'
            // }
          ]
          volumeMounts: [
            {
              volumeName: 'appdata'
              mountPath: '/app/AppData'
            }
          ]
        }
      ]
      volumes: [
        {
          name: 'appdata'
          storageType: 'AzureFile'
          storageName: meStorage.name
          mountOptions: 'nobrl'
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

output storageAccountName string = storageAccount.name
output fileShareName string = fileShare.name
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
