# 在 Azure 容器应用上快速部署

**中文** | [English](../en-US/azure-bicep.md)

本指南提供了使用 Bicep 模板在 Azure 容器应用上快速部署 SDCB Chats 应用的方法。它自动化了部署流程，包括创建存储账户和容器应用等必要资源。

注意：如需手动部署，请参考[手动部署指南](https://edi.wang/post/2025/7/25/deploy-sdcb-chats-on-azure-container-apps)。

前置条件：
- 已安装并登录 Azure CLI
- 已安装 Azure Bicep CLI

## 在终端中运行以下命令：

例如，将应用部署到 `japaneast` 区域的资源组 `sdcbchats-rg`。

```powershell
az group create --name sdcbchats-rg --location japaneast
az deployment group create --resource-group sdcbchats-rg --template-file main.bicep --parameters storageAccountName=sdcbchatsstorage2996
```

## 访问已部署的应用

部署完成后，可在 Azure 门户的容器应用资源下找到应用的访问 URL。
