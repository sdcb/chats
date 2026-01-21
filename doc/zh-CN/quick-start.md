# 快速开始

## 系统要求

- **Docker 部署**：任何支持 Docker 的系统（Linux/Windows/macOS）
- **可执行文件部署**：
  - Windows: Windows 10 或更高版本
  - Linux: glibc 2.17+ 或 musl libc
  - macOS: macOS 10.15 或更高版本
- **数据库**：SQLite（默认，无需安装）/ SQL Server / PostgreSQL

## Docker 部署

对于大多数用户而言，Docker 提供了最简单快速的部署方式。

### SQLite 快速启动

```bash
mkdir -p ./AppData && chmod 755 ./AppData && docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:/app/AppData -p 8080:8080 sdcb/chats:latest
```

> **说明**：SQLite 需要映射 `./AppData` 文件夹用于存储数据库文件和上传文件（如图床服务使用本地文件提供商时）。

### PostgreSQL 快速启动

```bash
docker run --restart unless-stopped --name sdcb-chats -e DBType=postgresql -e ConnectionStrings__ChatsDB="Host=host.docker.internal;Port=5432;Username=postgres;Password=mysecretpassword;Database=postgres" -p 8080:8080 sdcb/chats:latest
```

> **说明**：PostgreSQL 不依赖 `./AppData` 文件夹存储数据库，但如果使用本地文件提供商作为图床服务，仍需映射该文件夹：`-v ./AppData:/app/AppData`（用户可在管理界面配置其他文件存储方式）。

### 配置说明

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

### Docker 镜像列表

Chats 提供了以下几个镜像：

| 描述                          | Docker 镜像                                          |
| ----------------------------- | ---------------------------------------------------- |
| Latest（推荐）                | `docker.io/sdcb/chats:latest`                        |
| 指定完整版本                  | `docker.io/sdcb/chats:{version}`                     |
| 指定主版本                    | `docker.io/sdcb/chats:{major}`                       |
| 指定次版本                    | `docker.io/sdcb/chats:{major.minor}`                 |
| Linux x64                     | `docker.io/sdcb/chats:{version}-linux-x64`           |
| Linux ARM64                   | `docker.io/sdcb/chats:{version}-linux-arm64`         |
| Windows Nano Server LTSC 2022 | `docker.io/sdcb/chats:{version}-nanoserver-ltsc2022` |
| Windows Nano Server LTSC 2025 | `docker.io/sdcb/chats:{version}-nanoserver-ltsc2025` |

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

## 可执行文件部署指南

对于不便使用 Docker 部署的环境，Chats 提供了 8 种操作系统或架构的原生可执行文件，无需安装任何运行时环境即可直接运行。

### 下载地址

| 平台                   | GitHub 下载（所有版本）                                                                                         | 国内镜像下载（最新稳定版）                                                                     |
| ---------------------- | --------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| Windows 64位           | [chats-win-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-win-x64.zip)                   | [chats-win-x64.zip](https://chats.sdcb.pub/release/latest/chats-win-x64.zip)                   |
| Linux 64位             | [chats-linux-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-x64.zip)               | [chats-linux-x64.zip](https://chats.sdcb.pub/release/latest/chats-linux-x64.zip)               |
| Linux ARM64            | [chats-linux-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-arm64.zip)           | [chats-linux-arm64.zip](https://chats.sdcb.pub/release/latest/chats-linux-arm64.zip)           |
| Linux musl x64         | [chats-linux-musl-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-x64.zip)     | [chats-linux-musl-x64.zip](https://chats.sdcb.pub/release/latest/chats-linux-musl-x64.zip)     |
| Linux musl ARM64       | [chats-linux-musl-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-arm64.zip) | [chats-linux-musl-arm64.zip](https://chats.sdcb.pub/release/latest/chats-linux-musl-arm64.zip) |
| macOS ARM64            | [chats-osx-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-arm64.zip)               | [chats-osx-arm64.zip](https://chats.sdcb.pub/release/latest/chats-osx-arm64.zip)               |
| macOS x64              | [chats-osx-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-x64.zip)                   | [chats-osx-x64.zip](https://chats.sdcb.pub/release/latest/chats-osx-x64.zip)                   |
| 通用包（需要 .NET 10） | [chats.zip](https://github.com/sdcb/chats/releases/latest/download/chats.zip)                                   | [chats.zip](https://chats.sdcb.pub/release/latest/chats.zip)                                   |
| 纯前端文件             | [chats-fe.zip](https://github.com/sdcb/chats/releases/latest/download/chats-fe.zip)                             | [chats-fe.zip](https://chats.sdcb.pub/release/latest/chats-fe.zip)                             |

> **💡 下载说明**：
> - **国内镜像下载**（基于 Cloudflare R2）：推荐国内用户使用，速度更快
> - **最新开发版下载**：如需体验最新功能，开发版提供以下文件
>   - 通用包：[chats.zip](https://chats.sdcb.pub/latest/chats.zip)（需要 .NET 10）
>   - 前端文件：[chats-fe.zip](https://chats.sdcb.pub/latest/chats-fe.zip)
>   - ⚠️ 注意：开发版会从 `dev`/`feature` 分支自动更新，可能不稳定
> - 除通用包外，所有平台都提供 AOT 编译的原生可执行文件，启动速度快，内存占用低

### 版本说明

- **最新版本**：访问 [Releases](https://github.com/sdcb/chats/releases) 页面查看最新版本和更新日志
- **替代下载**：在 GitHub 访问不便时，可使用以下格式的国内镜像地址：
  ```
  https://chats.sdcb.pub/release/latest/{artifact-id}.zip
  ```
  例如：`https://chats.sdcb.pub/release/latest/chats-win-x64.zip`

### 运行说明

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

- **启动应用**：运行 `Chats.BE.exe` 即可启动 Chats 应用，该文件名虽指"后端"，但实际同时包含前端和后端组件。
- **数据库配置**：默认情况下，应用将在当前目录创建名为 `AppData` 的目录，并以 SQLite 作为数据库。命令行参数可用于指定不同的数据库类型：
  ```pwsh
  .\Chats.BE.exe --urls http://+:5000 --DBType=mssql --ConnectionStrings:ChatsDB="Data Source=(localdb)\mssqllocaldb; Initial Catalog=ChatsDB; Integrated Security=True"
  ```
  - 参数 `--urls`：用于指定应用监听的地址和端口。
  - 参数 `DBType`：可选 `sqlite`、`mssql` 或 `pgsql`。
  - 参数 `--ConnectionStrings:ChatsDB`：用于指定数据库的ADO.NET连接字符串。

### 依赖 .NET 运行时的版本说明

对于下载的 `chats.zip`，需要安装 .NET 10 运行时。安装后，使用以下命令启动：

```bash
dotnet Chats.BE.dll
```

下载 .NET 运行时：[https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
