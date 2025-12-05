# Sdcb Chats [![docker pulls](https://img.shields.io/docker/pulls/sdcb/chats)](https://hub.docker.com/r/sdcb/chats) [![QQ](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=social&logo=tencent-qq&logoColor=000&logoWidth=20)](https://qm.qq.com/q/AM8tY9cAsS) [![License](https://img.shields.io/github/license/sdcb/chats)](LICENSE) [![问DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sdcb/chats) [![更新日志](https://img.shields.io/static/v1?label=%F0%9F%93%9D%20&message=更新日志&color=8A2BE2)](./doc/zh-CN/release-notes/README.md)

**[English](README_EN.md)** | **简体中文** 

Sdcb Chats 是一个强大且灵活的大语言模型前端，支持 20+ 主流 AI 模型服务商。无论您是希望统一管理多种模型接口，还是需要一个简单易用的部署方案，Sdcb Chats 都能满足您的需求。

## ✨ 为什么选择 Sdcb Chats

- 🚀 **一站式管理**：统一管理 20+ 主流 AI 模型服务商
- 🎯 **开箱即用**：支持 Docker 一键部署，也提供 8 种平台的原生可执行文件
- 💾 **灵活存储**：支持 SQLite/SQL Server/PostgreSQL，支持本地文件/S3/OSS/Azure Blob 等多种存储
- 🔐 **企业级安全**：完善的用户权限管理和账户余额控制，支持 Keycloak SSO
- 🌐 **标准协议**：支持 OpenAI/Anthropic 等主流 API 协议，包括对话、图像生成等功能，兼容 Claude Code
- 🎨 **现代界面**：美观易用的前端界面，支持视觉模型交互

## 功能特性

- **多模型支持**：动态管理多种大语言模型接口
- **视觉模型支持**：集成视觉模型，增强用户交互体验
- **用户权限管理**：提供精细的用户权限设置，确保安全性
- **账户余额管理**：实时跟踪和管理用户账户余额
- **模型管理**：轻松添加、删除和配置模型
- **API 网关功能**：基于 OpenAI 协议透明地转发用户的聊天请求
- **简单部署**：支持 4 种操作系统/平台架构的 Docker 镜像，此外提供 8 种不同操作系统的可执行文件，方便不使用 Docker 的用户一键部署
- **多数据库支持**：兼容 SQLite、SQL Server 和 PostgreSQL 数据库，除了数据库外，不依赖其他组件
- **多文件服务支持**：兼容本地文件、AWS S3、Minio、Aliyun OSS、Azure Blob Storage 等文件服务，可运行时配置修改
- **多种登录方式支持**：支持 Keycloak SSO，支持手机短信验证码登录

<img alt="chats" src="https://github.com/user-attachments/assets/64a8f9ac-3ac0-4e3e-8903-2a2cf0b111a5" />

## 快速开始

### 系统要求

- **Docker 部署**：任何支持 Docker 的系统（Linux/Windows/macOS）
- **可执行文件部署**：
  - Windows: Windows 10 或更高版本
  - Linux: glibc 2.17+ 或 musl libc
  - macOS: macOS 10.15 或更高版本
- **数据库**：SQLite（默认，无需安装）/ SQL Server / PostgreSQL

### Docker 部署

对于大多数用户而言，Docker 提供了最简单快速的部署方式。

#### SQLite 快速启动

```bash
mkdir -p ./AppData && chmod 755 ./AppData && docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:/app/AppData -p 8080:8080 sdcb/chats:latest
```

> **说明**：SQLite 需要映射 `./AppData` 文件夹用于存储数据库文件和上传文件（如图床服务使用本地文件提供商时）。

#### PostgreSQL 快速启动

```bash
docker run --restart unless-stopped --name sdcb-chats -e DBType=postgresql -e ConnectionStrings__ChatsDB="Host=host.docker.internal;Port=5432;Username=postgres;Password=mysecretpassword;Database=postgres" -p 8080:8080 sdcb/chats:latest
```

> **说明**：PostgreSQL 不依赖 `./AppData` 文件夹存储数据库，但如果使用本地文件提供商作为图床服务，仍需映射该文件夹：`-v ./AppData:/app/AppData`（用户可在管理界面配置其他文件存储方式）。

#### 配置说明

- **数据库存储位置**：默认情况下，Chats 的 SQLite 数据库会在 `./AppData` 目录下创建。为了避免每次重新启动 Docker 容器时数据库被意外清空，我们首先创建一个 `AppData` 文件夹并将其权限设置为可写（`chmod 755`，安全起见不建议使用 777）
  
- **端口映射**：该命令将容器的 8080 端口映射到主机的 8080 端口，使得您可以通过 `http://localhost:8080` 访问应用

- **数据库类型配置**：`DBType` 环境变量指定数据库类型，默认值为 `sqlite`。除了 SQLite，该应用还支持使用 `mssql`（或 `sqlserver`）和 `postgresql`（或 `pgsql`）作为数据库选项

- **连接字符串**：`ConnectionStrings__ChatsDB` 的默认值为 `Data Source=./AppData/chats.db`，它是连接数据库的 ADO.NET 连接字符串

- **非首次运行**：如果您的 `AppData` 目录已经创建并且 Docker 用户对其有写入权限，可以简化启动命令如下：

    ```bash
    docker run --restart unless-stopped --name sdcb-chats -v ./AppData:/app/AppData -p 8080:8080 sdcb/chats:latest
    ```

- **数据库初始化**：容器启动后，如果数据库文件不存在，将自动创建并插入初始数据
  - 初始管理员用户名：`chats`
  - 初始默认密码：`RESET!!!`
  - ⚠️ **重要**：请在首次登录后立即前往左下角的用户管理界面修改密码，以确保系统安全

通过以上步骤，您将能顺利使用 Docker 部署和运行应用。如果在部署过程中遇到任何问题，请通过 [Issues](https://github.com/sdcb/chats/issues) 或 [QQ 群](https://qm.qq.com/q/AM8tY9cAsS) 联系我们。

#### Docker 镜像列表

Chats 提供了以下几个镜像：

| 描述                          | Docker 镜像                                              |
| ----------------------------- | ------------------------------------------------------- |
| Latest（推荐）                 | `docker.io/sdcb/chats:latest`                           |
| 指定完整版本                   | `docker.io/sdcb/chats:{version}`                        |
| 指定主版本                     | `docker.io/sdcb/chats:{major}`                          |
| 指定次版本                     | `docker.io/sdcb/chats:{major.minor}`                    |
| Linux x64                     | `docker.io/sdcb/chats:{version}-linux-x64`              |
| Linux ARM64                   | `docker.io/sdcb/chats:{version}-linux-arm64`            |
| Windows Nano Server LTSC 2022 | `docker.io/sdcb/chats:{version}-nanoserver-ltsc2022`    |
| Windows Nano Server LTSC 2025 | `docker.io/sdcb/chats:{version}-nanoserver-ltsc2025`    |

**版本说明：**

- **版本号格式**：采用语义化版本号，如 `1.8.1`
  - `{major}`: 主版本号，如 `1`
  - `{major.minor}`: 主版本号.次版本号，如 `1.8`
  - `{version}`: 完整版本号，如 `1.8.1`

- **多平台支持**：`latest` 和版本号标签（如 `1.8.1`、`1.8`、`1`）都是多平台镜像，包含：
  - Linux x64
  - Linux ARM64
  - Windows Nano Server LTSC 2022（适用于 Windows Server 2022）
  - Windows Nano Server LTSC 2025（适用于 Windows Server 2025）

- **自动选择平台**：使用 `docker pull` 时，无需指定具体的操作系统版本，Docker 会通过 manifest 自动选择适合您系统的正确版本

**示例：**

```bash
# 使用最新版本（推荐）
docker pull sdcb/chats:latest

# 使用指定版本
docker pull sdcb/chats:1.8.1

# 使用主版本号（自动获取 1.x.x 的最新版本）
docker pull sdcb/chats:1

# 使用次版本号（自动获取 1.8.x 的最新版本）
docker pull sdcb/chats:1.8

# 指定特定平台（通常不需要）
docker pull sdcb/chats:1.8.1-linux-x64
```

### 可执行文件部署指南

对于不便使用 Docker 部署的环境，Chats 提供了 8 种操作系统或架构的原生可执行文件，无需安装任何运行时环境即可直接运行。

#### 下载地址

| 平台                    | GitHub 下载（所有版本）                                                                                | 国内镜像下载（最新稳定版）                                                      |
| ----------------------- | ------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------- |
| Windows 64位            | [chats-win-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-win-x64.zip)         | [chats-win-x64.zip](https://chats.sdcb.pub/release/latest/chats-win-x64.zip)         |
| Linux 64位              | [chats-linux-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-x64.zip)     | [chats-linux-x64.zip](https://chats.sdcb.pub/release/latest/chats-linux-x64.zip)     |
| Linux ARM64             | [chats-linux-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-arm64.zip) | [chats-linux-arm64.zip](https://chats.sdcb.pub/release/latest/chats-linux-arm64.zip) |
| Linux musl x64          | [chats-linux-musl-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-x64.zip) | [chats-linux-musl-x64.zip](https://chats.sdcb.pub/release/latest/chats-linux-musl-x64.zip) |
| Linux musl ARM64        | [chats-linux-musl-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-arm64.zip) | [chats-linux-musl-arm64.zip](https://chats.sdcb.pub/release/latest/chats-linux-musl-arm64.zip) |
| macOS ARM64             | [chats-osx-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-arm64.zip)     | [chats-osx-arm64.zip](https://chats.sdcb.pub/release/latest/chats-osx-arm64.zip)     |
| macOS x64               | [chats-osx-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-x64.zip)         | [chats-osx-x64.zip](https://chats.sdcb.pub/release/latest/chats-osx-x64.zip)         |
| 通用包（需要 .NET 10） | [chats.zip](https://github.com/sdcb/chats/releases/latest/download/chats.zip)                         | [chats.zip](https://chats.sdcb.pub/release/latest/chats.zip)                         |
| 纯前端文件              | [chats-fe.zip](https://github.com/sdcb/chats/releases/latest/download/chats-fe.zip)                   | [chats-fe.zip](https://chats.sdcb.pub/release/latest/chats-fe.zip)                   |

> **💡 下载说明**：
> - **国内镜像下载**（基于 Cloudflare R2）：推荐国内用户使用，速度更快
> - **最新开发版下载**：如需体验最新功能，开发版提供以下文件
>   - 通用包：[chats.zip](https://chats.sdcb.pub/latest/chats.zip)（需要 .NET 10）
>   - 前端文件：[chats-fe.zip](https://chats.sdcb.pub/latest/chats-fe.zip)
>   - ⚠️ 注意：开发版会从 `dev`/`feature` 分支自动更新，可能不稳定
> - 除通用包外，所有平台都提供 AOT 编译的原生可执行文件，启动速度快，内存占用低

#### 版本说明

- **最新版本**：访问 [Releases](https://github.com/sdcb/chats/releases) 页面查看最新版本和更新日志
- **替代下载**：在 GitHub 访问不便时，可使用以下格式的国内镜像地址：
  ```
  https://chats.sdcb.pub/release/latest/{artifact-id}.zip
  ```
  例如：`https://chats.sdcb.pub/release/latest/chats-win-x64.zip`

#### 运行说明

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

#### 依赖 .NET 运行时的版本说明

对于下载的 `chats.zip`，需要安装 .NET 10 运行时。安装后，使用以下命令启动：

```bash
dotnet Chats.BE.dll
```

下载 .NET 运行时：[https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

---

## 支持的大模型服务

| Id  | Name             | Host                                                     |
| --- | ---------------- | -------------------------------------------------------- |
| 0   | 测试             | null                                                     |
| 1   | Azure AI Foundry | https://<resource-name>.openai.azure.com/                |
| 2   | 腾讯混元         | https://api.hunyuan.cloud.tencent.com/v1                 |
| 3   | 零一万物         | https://api.lingyiwanwu.com/v1                           |
| 4   | 月之暗面         | https://api.moonshot.cn/v1                               |
| 5   | OpenAI           | https://api.openai.com/v1                                |
| 6   | 文心一言         | https://qianfan.baidubce.com/v2                          |
| 7   | 通义千问         | https://dashscope.aliyuncs.com/compatible-mode/v1        |
| 8   | 讯飞星火         | https://spark-api-open.xf-yun.com/v1                     |
| 9   | 智谱AI           | https://open.bigmodel.cn/api/paas/v4/                    |
| 10  | DeepSeek         | https://api.deepseek.com/v1                              |
| 11  | x.ai             | https://api.x.ai/v1                                      |
| 12  | Github Models    | https://models.inference.ai.azure.com                    |
| 13  | 谷歌AI           | https://generativelanguage.googleapis.com/v1beta/openai |
| 14  | Ollama           | http://localhost:11434/v1                                |
| 15  | MiniMax          | https://api.minimax.chat/v1                              |
| 16  | 火山方舟         | https://ark.cn-beijing.volces.com/api/v3                |
| 17  | 硅基流动         | https://api.siliconflow.cn/v1                            |
| 18  | OpenRouter       | https://openrouter.ai/api/v1                             |
| 19  | 小马算力         | https://api.tokenpony.cn/v1                              |
| 20  | Anthropic        | https://api.anthropic.com                                |

**注意事项：**

- ✅ 任何符合 OpenAI Chat Completion API 协议的模型提供商都可以通过 Chats 进行访问
- 🤖 OpenAI/Azure AI Foundry 的 o3/o4-mini/gpt-5 系列模型使用 Response API 协议（非 Chat Completion API），支持思考概要和思考过程功能
- 🌐 Google AI 的 Gemini 模型使用 Google Gemini 原生 API 协议

---

## 开发文档

Chats 使用 `C#`/`TypeScript` 开发，有关如何编译和开发 Chats，请查看：

- [🛠️ 开发文档](./doc/zh-CN/build.md)

---

## 常见问题

<details>
<summary><b>如何修改默认端口？</b></summary>

在启动时使用 `--urls` 参数指定端口：

```bash
# Docker
docker run -e ASPNETCORE_URLS="http://+:5000" -p 5000:5000 sdcb/chats:latest

# 可执行文件
./Chats.BE.exe --urls http://+:5000
```
</details>

<details>
<summary><b>如何切换到 SQL Server 或 PostgreSQL？</b></summary>

使用 `--DBType` 参数和 `--ConnectionStrings:ChatsDB` 参数：

```bash
# SQL Server
./Chats.BE.exe --DBType=mssql --ConnectionStrings:ChatsDB="Server=localhost;Database=ChatsDB;User Id=sa;Password=YourPassword"

# PostgreSQL
./Chats.BE.exe --DBType=pgsql --ConnectionStrings:ChatsDB="Host=localhost;Database=chatsdb;Username=postgres;Password=YourPassword"
```
</details>

<details>
<summary><b>如何配置文件存储服务？</b></summary>

Chats 支持多种文件存储服务，可在管理界面的系统设置中配置，支持：
- 本地文件系统
- AWS S3
- Minio
- Aliyun OSS
- Azure Blob Storage

配置后无需重启即可生效。
</details>

<details>
<summary><b>忘记管理员密码怎么办？</b></summary>

可以通过数据库直接重置密码,或删除数据库文件重新初始化（注意备份数据）。
</details>

<details>
<summary><b>如何在纯 Windows 环境使用 Docker 部署？</b></summary>

纯 Windows 环境下使用 SQLite 数据库：

```powershell
mkdir AppData
icacls .\AppData /grant "Users:(OI)(CI)(M)" /T
docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:C:/app/AppData -p 8080:8080 sdcb/chats:latest
```

纯 Windows 环境下使用 PostgreSQL 数据库：

```powershell
mkdir AppData
icacls .\AppData /grant "Users:(OI)(CI)(M)" /T
docker run --restart unless-stopped --name sdcb-chats -e DBType=postgresql -e ConnectionStrings__ChatsDB="Host=host.docker.internal;Port=5432;Username=postgres;Password=YourPassword;Database=postgres" -v ./AppData:C:/app/AppData -p 8080:8080 sdcb/chats:latest
```

**注意**：从容器访问宿主机服务时，使用 `host.docker.internal` 而不是 `localhost`。
</details>

---

## 贡献指南

我们欢迎各种形式的贡献，包括但不限于：

- 🐛 报告 Bug
- 💡 提出新功能建议
- 📝 改进文档
- 🔧 提交代码

请通过 [GitHub Issues](https://github.com/sdcb/chats/issues) 提交问题或建议。

---

## 联系方式

- **GitHub Issues**：[https://github.com/sdcb/chats/issues](https://github.com/sdcb/chats/issues)
- **QQ 群**：498452653 [![加入QQ群](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=flat&logo=tencent-qq)](https://qm.qq.com/q/AM8tY9cAsS)

---

## 许可证

本项目采用 [MIT License](LICENSE) 开源许可证。

---

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=sdcb/chats&type=Date)](https://star-history.com/#sdcb/chats&Date)

---

**如果这个项目对你有帮助，欢迎给个 ⭐ Star！**
