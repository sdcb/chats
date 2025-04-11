**[English](README_EN.md)** | **简体中文** [![docker pulls](https://img.shields.io/docker/pulls/sdcb/chats)](https://hub.docker.com/r/sdcb/chats) [![QQ](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=social&logo=tencent-qq&logoColor=000&logoWidth=20)](https://qm.qq.com/q/AM8tY9cAsS)


Sdcb Chats 是一个强大且灵活的大语言模型前端，支持多种功能和平台。无论您是希望管理多种模型接口，还是需要一个简单的部署流程，Sdcb Chats 都能满足您的需求。

![](https://github.com/user-attachments/assets/2d9d609e-142e-4dbf-bdba-87477d7753fb)

## 功能特性

- **多模型支持**：动态管理多种大语言模型接口。
- **视觉模型支持**：集成视觉模型，增强用户交互体验。
- **用户权限管理**：提供精细的用户权限设置，确保安全性。
- **账户余额管理**：实时跟踪和管理用户账户余额。
- **模型管理**：轻松添加、删除和配置模型。
- **API 网关功能**：基于 OpenAI 协议透明地转发用户的聊天请求。
- **简单部署**：支持 4 种操作系统/平台架构的 Docker 镜像。此外，提供 8 种不同操作系统的可执行文件，方便不使用 Docker 的用户一键部署。
- **多数据库支持**: 兼容 SQLite、SQL Server 和 PostgreSQL 数据库，除了数据库外，不依赖其他组件。
- **多文件服务支持**: 兼容本地文件，AWS S3、Minio、Aliyun OSS、Azure Blob Storage等文件服务，可运行时配置修改。
- **多种登录方式支持**: 支持Keycloak SSO，支持手机短信验证码登录。


## 快速开始

### 开发文档

Chats使用`C#`/`TypeScript`开发，有关如何编译Chats，请查看 [开发文档链接](./doc/zh-CN/build.md)

### Docker 部署

对于大多数用户而言，Docker 提供了最简单快速的部署方式。以下是一步到位的部署命令：

```bash
mkdir ./AppData && chmod 777 ./AppData && docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:/app/AppData -p 8080:8080 sdcb/chats:latest
```

#### 说明：

- **数据库存储位置**：默认情况下，Chats 的 SQLite 数据库会在 `./AppData` 目录下创建。为了避免每次重新启动 Docker 容器时数据库被意外清空，我们首先创建一个 `AppData` 文件夹并将其权限设置为可写（`chmod 777`）。
  
- **端口映射**：该命令将容器的 8080 端口映射到主机的 8080 端口，使得您可以通过 `http://localhost:8080` 访问应用。

- **数据库类型配置**：`DBType` 环境变量指定数据库类型，默认值为 `sqlite`。除了 SQLite，该应用还支持使用 `mssql`（或`sqlserver`）和 `postgresql`（或`pgsql`）作为数据库选项。

- **连接字符串**：`ConnectionStrings__ChatsDB` 的默认值为 `Data Source=./AppData/chats.db`，它是连接数据库的 ADO.NET 连接字符串。

- **非首次运行**：如果您的 `AppData` 目录已经创建并且 Docker 用户对其有写入权限，可以简化启动命令如下：

    ```bash
    docker run --restart unless-stopped --name sdcb-chats -v ./AppData:/app/AppData -p 8080:8080 sdcb/chats:latest
    ```

- **数据库初始化**：容器启动后，如果数据库文件不存在，将自动创建并插入初始数据。初始管理员用户名为 `chats`，默认密码为 `RESET!!!`。强烈建议您在首次登录后立即前往左下角的用户管理界面，设置一个新密码以确保安全。

通过以上步骤，您将能顺利使用 Docker 部署和运行应用。如果在部署过程中遇到任何问题，可以联系我们。

Chats提供了以下几个镜像：

| 描述                          | Docker 镜像                                         |
| ----------------------------- | --------------------------------------------------- |
| Latest                        | docker.io/sdcb/chats:latest                         |
| r{version}                    | docker.io/sdcb/chats:r{version}                     |
| Linux x64                     | docker.io/sdcb/chats:r{version}-linux-x64           |
| Linux ARM64                   | docker.io/sdcb/chats:r{version}-linux-arm64         |
| Windows Nano Server 1809      | docker.io/sdcb/chats:r{version}-nanoserver-1809     |
| Windows Nano Server LTSC 2022 | docker.io/sdcb/chats:r{version}-nanoserver-ltsc2022 |

**说明：**

- `Latest` 和 `r{version}` 镜像中已经包含了以下四个操作系统版本的支持：
  - Linux x64
  - Linux ARM64
  - Windows Nano Server 1809（适用于 Windows Server 2019）
  - Windows Nano Server LTSC 2022（适用于 Windows Server 2022）

因此，用户在使用 `docker pull` 时，无需指定具体的操作系统版本，Docker 会自动选择适合您系统的正确版本。这一功能是通过 Docker 的 manifest 创建实现的，确保了用户能够轻松获取与其环境兼容的镜像。

请注意，`r{version}` 中的 `{version}` 表示具体的版本号，例如 `r141`（在编写文档时的最新版本号）。

### 可执行文件部署指南

对于不便使用 Docker 部署的环境，Chats 提供了 8 种操作系统或架构的直接部署选项。可从以下链接获取相应的编译包：

| 平台             | Github下载链接                                                                                                  | 国内下载链接                                                                                     |
| ---------------- | --------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Windows 64位     | [chats-win-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-win-x64.zip)                   | [chats-win-x64.zip](https://io.starworks.cc:88/chats/latest/chats-win-x64.zip)                   |
| Linux 64位       | [chats-linux-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-x64.zip)               | [chats-linux-x64.zip](https://io.starworks.cc:88/chats/latest/chats-linux-x64.zip)               |
| Linux ARM64      | [chats-linux-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-arm64.zip)           | [chats-linux-arm64.zip](https://io.starworks.cc:88/chats/latest/chats-linux-arm64.zip)           |
| Linux musl x64   | [chats-linux-musl-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-x64.zip)     | [chats-linux-musl-x64.zip](https://io.starworks.cc:88/chats/latest/chats-linux-musl-x64.zip)     |
| Linux musl ARM64 | [chats-linux-musl-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-arm64.zip) | [chats-linux-musl-arm64.zip](https://io.starworks.cc:88/chats/latest/chats-linux-musl-arm64.zip) |
| macOS ARM64      | [chats-osx-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-arm64.zip)               | [chats-osx-arm64.zip](https://io.starworks.cc:88/chats/latest/chats-osx-arm64.zip)               |
| macOS x64        | [chats-osx-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-x64.zip)                   | [chats-osx-x64.zip](https://io.starworks.cc:88/chats/latest/chats-osx-x64.zip)                   |
| 依赖.NET的通用包 | [chats.zip](https://github.com/sdcb/chats/releases/latest/download/chats.zip)                                   | [chats.zip](https://io.starworks.cc:88/chats/latest/chats.zip)                                   |
| 纯前端文件       | [chats-fe.zip](https://github.com/sdcb/chats/releases/latest/download/chats-fe.zip)                             | [chats-fe.zip](https://io.starworks.cc:88/chats/latest/chats-fe.zip)                             |

### 版本和下载说明

1. **指定版本下载地址**：
   - 若需下载特定版本的 Chats，将链接中的`release/latest/download`替换为`releases/download/r-{version}`。例如，版本 `141` 的 Linux ARM64 文件链接为：
     ```
     https://github.com/sdcb/chats/releases/download/r-141/chats-linux-arm64.zip
     ```

2. **替代下载基础地址**：
   - 在访问不便时，可使用替代下载地址，将{version}调整为具体版本号或使用 `latest`：
     ```
     https://io.starworks.cc:88/chats/r{version}/{artifact-id}.zip
     ```
   - 例如，通过替代下载直接获取最新的 Windows 64-bit 版：
     ```
     https://io.starworks.cc:88/chats/latest/chats-win-x64.zip
     ```

### 执行文件目录结构和运行说明

解压AOT可执行文件后的目录结构如下：

```
C:\Users\ZhouJie\Downloads\chats-win-x64>dir
 2024/12/06  16:35    <DIR>          .
 2024/12/06  16:35    <DIR>          ..
 2024/12/06  16:35               119 appsettings.Development.json
 2024/12/06  16:35               417 appsettings.json
 2024/12/06  16:35           367,144 aspnetcorev2_inprocess.dll
 2024/12/06  16:35        84,012,075 Chats.BE.exe
 2024/12/06  16:35           200,296 Chats.BE.pdb
 2024/12/06  16:35         1,759,232 e_sqlite3.dll
 2024/12/06  16:35           504,872 Microsoft.Data.SqlClient.SNI.dll
 2024/12/06  16:35               465 web.config
 2024/12/06  16:35    <DIR>          wwwroot
```

- **启动应用**：运行 `Chats.BE.exe` 即可启动 Chats 应用，该文件名虽指“后端”，但实际同时包含前端和后端组件。
- **数据库配置**：默认情况下，应用将在当前目录创建名为 `AppData` 的目录，并以 SQLite 作为数据库。命令行参数可用于指定不同的数据库类型：
  ```pwsh
  .\Chats.BE.exe --urls http://+:5000 --DBType=mssql --ConnectionStrings:ChatsDB="Data Source=(localdb)\mssqllocaldb; Initial Catalog=ChatsDB; Integrated Security=True"
  ```
  - 参数 `--urls`：用于指定应用监听的地址和端口。
  - 参数 `DBType`：可选 `sqlite`、`mssql` 或 `pgsql`。
  - 参数 `--ConnectionStrings:ChatsDB`：用于指定数据库的ADO.NET连接字符串。

#### 特殊说明

- 对于下载的 `chats.zip`，将需要.NET SDK支持。安装.NET运行时后，使用 `dotnet Chats.BE.dll` 启动程序。

### 支持的大模型服务

| Id  | Name             | Host                                                     | Comment                               |
| --- | ---------------- | -------------------------------------------------------- | ------------------------------------- |
| 0   | Test             | null                                                     |                                       |
| 1   | Azure OpenAI     | https://<resource-name>.openai.azure.com/                |                                       |
| 2   | Tencent Hunyuan  | https://api.hunyuan.cloud.tencent.com/v1                 |                                       |
| 3   | 01.ai            | https://api.lingyiwanwu.com/v1                           |                                       |
| 4   | Moonshot         | https://api.moonshot.cn/v1                               |                                       |
| 5   | OpenAI           | https://api.openai.com/v1                                |                                       |
| 6   | Wenxin Qianfan   | https://qianfan.baidubce.com/v2                          |                                       |
| 7   | DashScope        | https://dashscope.aliyuncs.com/compatible-mode/v1        |                                       |
| 8   | Xunfei SparkDesk | https://spark-api-open.xf-yun.com/v1                     |                                       |
| 9   | Zhipu AI         | https://open.bigmodel.cn/api/paas/v4/                    |                                       |
| 10  | DeepSeek         | https://api.deepseek.com/v1                              |                                       |
| 11  | x.ai             | https://api.x.ai/v1                                      |                                       |
| 12  | Github Models    | https://models.inference.ai.azure.com                    |                                       |
| 13  | Google AI        | https://generativelanguage.googleapis.com/v1beta/openai/ |                                       |
| 14  | Ollama           | http://localhost:11434/v1                                | 任何支持OpenAI协议的API都可以用Ollama |
| 15  | MiniMax          | https://api.minimax.chat/v1                              |                                       |
| 16  | Doubao           | https://ark.cn-beijing.volces.com/api/v3/                |                                       |
| 17  | SiliconFlow      | https://api.siliconflow.cn/v1                            |                                       |
| 18  | OpenRouter       | https://openrouter.ai/api/v1                             |                                       |
