# 快速开始

[English](../en-US/quick-start.md) | **简体中文**

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
mkdir -p ./AppData
chmod 755 ./AppData
docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:/app/AppData -v /var/run/docker.sock:/var/run/docker.sock --user 0:0 -p 8080:8080 sdcb/chats:latest
```

> **说明**：
> - SQLite 需要映射 `./AppData` 文件夹用于存储数据库文件和上传文件（如图床服务使用本地文件提供商时）。
> - `-v /var/run/docker.sock:/var/run/docker.sock` 和 `--user 0:0` 是为了支持基于 Docker 沙箱的 Code Interpreter 功能，如果不需要该功能可以删除这两个参数。

### PostgreSQL 快速启动

```bash
docker run --restart unless-stopped --name sdcb-chats -e DBType=postgresql -e ConnectionStrings__ChatsDB="Host=host.docker.internal;Port=5432;Username=postgres;Password=mysecretpassword;Database=postgres" -v /var/run/docker.sock:/var/run/docker.sock --user 0:0 -p 8080:8080 sdcb/chats:latest
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

> 💾 **Docker 镜像列表**：详细的 Docker 版本说明和使用示例，请参考[下载地址页面](./downloads.md)。

## 可执行文件部署指南

对于不便使用 Docker 部署的环境，Chats 提供了 8 种操作系统或架构的原生可执行文件，无需安装任何运行时环境即可直接运行。

> 💾 **下载地址**：完整的下载地址列表（包括 GitHub 和国内镜像），请参考[下载地址页面](./downloads.md)。

### 运行说明

解压可执行文件后的目录结构如下：

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

**基础启动**：直接运行 `Chats.BE.exe` 即可启动应用（该文件名虽指"后端"，但实际同时包含前端和后端组件）。应用默认使用 SQLite 数据库，并在当前目录创建 `AppData` 文件夹存储数据。

**Windows + Docker Desktop 部署**：如果你在 Windows 系统上安装了 Docker Desktop，并希望使用 Code Interpreter 功能，需要指定 Docker 端点：

```pwsh
.\Chats.BE.exe --urls http://+:5000 --CodePod:DockerEndpoint npipe://./pipe/docker_engine
```

该配置将应用绑定到 5000 端口，并连接到 Docker Desktop 的命名管道，以支持基于 Docker 沙箱的代码执行环境。

**更多配置选项**：

如需自定义端口、数据库类型或连接字符串，可通过命令行参数进行配置，例如：

```pwsh
.\Chats.BE.exe --urls http://+:5000 --CodePod:DockerEndpoint npipe://./pipe/docker_engine --DBType=mssql --ConnectionStrings:ChatsDB="Data Source=(localdb)\mssqllocaldb; Initial Catalog=ChatsDB; Integrated Security=True"
```

**参数说明**：
- `--urls`：指定应用监听的地址和端口
- `--CodePod:DockerEndpoint`：指定 Docker 服务端点。Windows 上使用 `npipe://./pipe/docker_engine` 连接 Docker Desktop；Linux/macOS 使用默认的 `unix:///var/run/docker.sock`
- `--DBType`：数据库类型，可选 `sqlite`（默认）、`mssql` 或 `pgsql`
- `--ConnectionStrings:ChatsDB`：数据库的 ADO.NET 连接字符串

> 💡 更多高级配置选项，请参考[配置说明文档](./configuration.md)。

### 依赖 .NET 运行时的版本说明

对于下载的 `chats.zip`，需要安装 .NET 10 运行时。安装后，使用以下命令启动：

```bash
dotnet Chats.BE.dll
```

下载 .NET 运行时：[https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
