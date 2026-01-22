# Chats 开发指南

[English](../en-US/build.md) | **简体中文**

欢迎使用 Chats！这个指南将帮助您快速上手开发，了解如何在开发阶段使用和配置 Chats 项目。Chats 在开发阶段采用前后端分离的模式，但在生产环境中前后端会合并为一个发布包。

## 📑 目录

- [技术栈](#技术栈)
- [环境需求](#环境需求)
- [获取代码](#获取代码)
- [前后端共同开发](#前后端共同开发)
  - [后端开发指南](#后端开发指南)
  - [前端开发指南](#前端开发指南)
- [仅后端开发](#仅后端开发)
- [常见问题](#常见问题)

## 技术栈

- **后端**：C# / ASP.NET Core 10
- **前端**：Next.js / React / TypeScript
- **样式**：Tailwind CSS
- **数据库**：SQLite（默认）/ SQL Server / PostgreSQL
- **存储**：本地文件 / AWS S3 / Minio / Aliyun OSS / Azure Blob Storage

## 环境需求

- **Git**：用于代码版本管理
- **[.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)**：后端开发必需
- **[Node.js](https://nodejs.org/) >= 20**：前端开发必需
- **Visual Studio Code**：轻量级代码编辑器
- **Visual Studio 2026**（可选但推荐）：完整的 IDE，调试体验更好

## 获取代码

首先，克隆 Chats 的代码仓库：

```bash
git clone https://github.com/sdcb/chats.git
```

## 前后端共同开发

### 后端开发指南

#### 1. 使用 Visual Studio 打开解决方案

在根目录下找到 `Chats.sln` 解决方案文件并打开。在 Visual Studio 中，您将看到一个名为 `Chats.BE` 的网站项目。

#### 2. 运行项目

- **方式一（Visual Studio）**：按 `F5` 或点击"启动调试"按钮运行项目
- **方式二（命令行）**：在项目目录下执行 `dotnet run`

**运行说明**：

- 默认配置会检查 SQLite 数据库文件 `chats.db` 是否存在，如果不存在，会自动创建在 `./AppData` 目录并初始化数据库
- 服务将在 `http://localhost:5146` 上运行，并提供 API 服务
- 在开发模式下（`ASPNETCORE_ENVIRONMENT=Development`），Swagger UI 将在 `http://localhost:5146/swagger` 上可用，方便测试 API

#### 3. 配置文件说明

默认配置在 `appsettings.json` 中，但 **强烈建议使用 `userSecrets.json` 管理敏感信息**，避免在代码库中泄露敏感的开发配置。

**默认配置结构：**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "FE_URL": "http://localhost:3001",
  "ENCRYPTION_PASSWORD": "this is used for encrypt auto increment int id, please set as a random string.",
  "DBType": "sqlite",
  "ConnectionStrings": {
    "ChatsDB": "Data Source=./AppData/chats.db"
  }
}
```

**配置选项详解：**

| 配置项                      | 说明                         | 默认值                                 |
| --------------------------- | ---------------------------- | -------------------------------------- |
| `Logging`                   | 日志级别配置                 | Information                            |
| `AllowedHosts`              | 允许访问的主机名             | `*`（接受所有）                        |
| `FE_URL`                    | 前端 URL，用于 CORS 跨域配置 | `http://localhost:3001`                |
| `DBType`                    | 数据库类型                   | `sqlite`（支持 `mssql`、`postgresql`） |
| `ConnectionStrings:ChatsDB` | 数据库 ADO.NET 连接字符串    | `Data Source=./AppData/chats.db`       |
| `ENCRYPTION_PASSWORD`       | 用于加密自增 ID 的密钥       | 建议设置为随机字符串                   |

> 更详细的配置说明，请见[配置说明文档](./configuration.md)。

> **💡 为什么使用整数 + 加密而非 GUID？**
> 
> 在 Chats 项目初期，我们确实使用的是 GUID，但经过慎重考虑，改为了自增整数 ID：
> - ✅ GUID 字段较大，占用更多存储空间
> - ✅ GUID 作为聚集索引会导致索引碎片，影响性能
> - ✅ 整数 ID 更高效，通过加密可以避免直接暴露 ID


#### 4. 管理敏感配置（推荐）

⚠️ **不建议在 `appsettings.json` 中直接修改敏感配置项**，应使用 `userSecrets.json` 来管理。

**使用 Visual Studio：**

1. 右键点击 `Chats.BE` 项目
2. 选择"管理用户机密"
3. 在打开的 `secrets.json` 文件中添加配置

**使用命令行：**

```bash
# 初始化用户机密
dotnet user-secrets init --project src/BE

# 设置配置项
dotnet user-secrets set "ENCRYPTION_PASSWORD" "your-random-string" --project src/BE
dotnet user-secrets set "ConnectionStrings:ChatsDB" "your-connection-string" --project src/BE

# 查看所有配置
dotnet user-secrets list --project src/BE
```

这样可以避免在提交代码时不小心将敏感信息上传到代码仓库。

#### 5. 命令行运行方式

如果不使用 Visual Studio，可以通过命令行运行：

```bash
# 进入后端目录
cd ./src/BE

# 运行项目
dotnet run

# 或指定监听地址
dotnet run --urls "http://localhost:5146"
```

### 前端开发指南

#### 1. 进入前端目录

```bash
cd ./src/FE
```

#### 2. 创建环境配置文件

创建 `.env.local` 文件并指定后端 API 地址：

**Linux/macOS：**

```bash
echo "API_URL=http://localhost:5146" > .env.local
```

**Windows PowerShell：**

```powershell
"API_URL=http://localhost:5146" | Out-File -FilePath .env.local -Encoding utf8
```

#### 3. 安装依赖并运行

```bash
# 安装依赖
npm install

# 或使用 pnpm（推荐，速度更快）
# pnpm install

# 启动开发服务器
npm run dev
```

**运行说明**：

- 前端服务将监听 `http://localhost:3000`
- 后端已配置 CORS 支持，无需额外配置
- 修改代码后会自动热重载，无需手动刷新浏览器

## 仅后端开发

对于专注于后端开发的场景，可以使用预构建的前端静态文件，无需本地编译前端。

### 快速开始

#### 1. 克隆仓库并进入后端目录

```bash
git clone https://github.com/sdcb/chats.git
cd chats/src/BE
```

#### 2. 下载并部署前端静态文件

**Linux/macOS：**

```bash
# 下载前端文件
curl -L -O https://github.com/sdcb/chats/releases/latest/download/chats-fe.zip

# 解压到 wwwroot 目录
unzip -o chats-fe.zip
cp -r chats-fe/* wwwroot/
rm -rf chats-fe chats-fe.zip
```

**Windows PowerShell：**

```powershell
# 下载前端文件
Invoke-WebRequest -Uri "https://github.com/sdcb/chats/releases/latest/download/chats-fe.zip" -OutFile "chats-fe.zip"

# 解压到 wwwroot 目录
Expand-Archive -Path "chats-fe.zip" -DestinationPath "." -Force
Copy-Item -Path ".\chats-fe\*" -Destination ".\wwwroot" -Recurse -Force
Remove-Item -Path "chats-fe" -Recurse -Force
Remove-Item -Path "chats-fe.zip"
```

**国内镜像地址（推荐）：**

如果从 GitHub 下载速度较慢，可以使用国内镜像：

```bash
# Linux/macOS
curl -L -O https://chats.sdcb.pub/release/latest/chats-fe.zip

# Windows PowerShell
Invoke-WebRequest -Uri "https://chats.sdcb.pub/release/latest/chats-fe.zip" -OutFile "chats-fe.zip"
```

> **📌 注意事项**：
> 
> 1. `chats-fe.zip` 由 GitHub Actions 在代码合入 `main` 分支时自动生成
> 2. 合入 `dev` 分支时不会触发更新
> 3. 如需最新的开发版本，请使用前端开发模式

#### 3. 运行后端

**使用命令行：**

```bash
dotnet run
```

**使用 Visual Studio：**

1. 打开 `Chats.sln` 解决方案
2. 选择 `Chats.BE` 项目
3. 按 `F5` 启动调试

#### 4. 访问应用

运行成功后，访问 `http://localhost:5146/login` 即可进入 Chats 的登录界面，实现前后端不分离的部署模式。

---

## 常见问题

### 1. 如何切换数据库类型？

修改 `appsettings.json` 或使用用户机密设置：

```json
{
  "DBType": "mssql",  // 或 "postgresql", "sqlite"
  "ConnectionStrings": {
    "ChatsDB": "Server=localhost;Database=ChatsDB;User Id=sa;Password=YourPassword;"
  }
}
```

### 2. 前端请求后端 API 失败？

检查以下配置：

- 确保后端已启动并监听在 `http://localhost:5146`
- 检查前端 `.env.local` 文件中的 `API_URL` 配置
- 检查后端的 `FE_URL` 配置是否正确
- 查看浏览器控制台是否有 CORS 错误

### 3. 如何重置数据库？

删除数据库文件并重新运行即可：

```bash
# SQLite
rm ./AppData/chats.db

# 然后重新运行项目
dotnet run
```

### 4. Visual Studio 无法识别 .NET 10？

确保已安装 Visual Studio 2026 或更高版本，并安装了 .NET 10 SDK。

---

## 相关资源

- **GitHub 仓库**：[https://github.com/sdcb/chats](https://github.com/sdcb/chats)
- **问题反馈**：[创建 Issue](https://github.com/sdcb/chats/issues)
- **QQ 群**：498452653
- **版本更新日志**：[Release Notes](./release-notes/README.md)

---

希望此指南可以帮助您顺利开展 Chats 项目的开发工作。如有任何问题，欢迎通过 GitHub Issues 或 QQ 群获取支持！