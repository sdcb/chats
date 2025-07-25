# Quick Deploy on Azure Container Apps

[中文](../zh-CN/azure-bicep.md) | **English**

This guide provides a quick way to deploy the SDCB Chats application on Azure Container Apps using Bicep templates. It automates the deployment process, including creating necessary resources like storage accounts and container apps.

Note: For manual deployment, refer to the [manual deployment guide](https://edi.wang/post/2025/7/25/deploy-sdcb-chats-on-azure-container-apps).

Prerequisites:
- Azure CLI installed and logged in
- Azure Bicep CLI installed

## Run the following commands in your terminal:

e.g. Deploy to a resource group named `sdcbchats-rg` in the `japaneast` region.

```powershell
az group create --name sdcbchats-rg --location japaneast
az deployment group create --resource-group sdcbchats-rg --template-file main.bicep --parameters storageAccountName=sdcbchatsstorage2996
```

## Access the deployed application

After the deployment is complete, you can access the application using the URL provided in the Azure Portal under the Container Apps resource.
