# 配置说明（appsettings.json）

[English](../en-US/configuration.md) | **简体中文**

本文档说明后端配置文件中的各项配置（基于 `src/BE/web/appsettings.json`），并按功能分类逐项解释。

> 注：本文刻意不介绍 ASP.NET Core 通用项（例如 `Logging`、`AllowedHosts`）。如需了解通用配置，请访问 [ASP.NET Core 官方配置文档](https://learn.microsoft.com/zh-cn/aspnet/core/fundamentals/configuration/)。

## 0. 配置来源与优先级

Chats 基于 .NET 配置系统读取配置，优先级从高到低为：

1. **命令行参数**（如 `--DBType=sqlite`、`--ConnectionStrings:ChatsDB=...`）
2. **环境变量**（如 `DBType`、`ConnectionStrings__ChatsDB`）
3. **appsettings.json**（默认配置文件）

> 提示：嵌套配置在环境变量中用双下划线 `__` 表示层级，例如 `ConnectionStrings__ChatsDB`、`CodeInterpreter__DefaultTimeoutSeconds`。

### 配置项汇总

| 配置名                                                                                         | 默认值                                  | 注意事项                    |
| ---------------------------------------------------------------------------------------------- | --------------------------------------- | --------------------------- |
| [`FE_URL`](#11-fe_url)                                                                         | `http://localhost:3001`                 | 必须配置，影响CORS策略      |
| [`ENCRYPTION_PASSWORD`](#21-encryption_password)                                               | 示例占位文本                            | 生产环境务必设置随机字符串  |
| [`DBType`](#31-dbtype)                                                                         | `sqlite`                                | 支持sqlite/mssql/postgresql |
| [`ConnectionStrings:ChatsDB`](#32-connectionstringschatsdb)                                    | `Data Source=./AppData/chats.db`        | 必须配置                    |
| [`CodePod:IsWindowsContainer`](#41-codepodiswindowscontainer)                                  | `false`                                 | 影响Docker端点和命令        |
| [`CodePod:DockerEndpoint`](#42-codepod​dockerendpoint)                                         | `null`                                  | null时自动选择默认端点      |
| [`CodePod:WorkDir`](#43-codepodworkdir)                                                        | `/app`                                  | 建议保持默认值              |
| [`CodePod:ArtifactsDir`](#44-codepodartifactsdir)                                              | `artifacts`                             | 相对于WorkDir的子目录       |
| [`CodePod:LabelPrefix`](#45-codepodlabelprefix)                                                | `codepod`                               | 用于容器命名和标签          |
| [`CodePod:OutputOptions:MaxOutputBytes`](#46-codepodoutputoptionsmaxoutputbytes)               | `6144`                                  | 6KB，超过会截断             |
| [`CodeInterpreter:DefaultImage`](#51-codeinterpreterdefaultimage)                              | `sdcb/code-interpreter:r-26`            | 新建会话默认镜像            |
| [`CodeInterpreter:DefaultImageDescription`](#52-codeinterpreterdefaultimagedescription)        | `Pre-installed with common packages...` | 用于丰富系统提示词          |
| [`CodeInterpreter:DefaultTimeoutSeconds`](#53-codeinterpreterdefaulttimeoutseconds)            | `300`                                   | null表示近似无限(24小时)    |
| [`CodeInterpreter:SessionIdleTimeoutSeconds`](#54-codeinterpretersessionidletimeoutseconds)    | `1800`                                  | 30分钟，会话空闲超时        |
| [`CodeInterpreter:DefaultNetworkMode`](#55-codeinterpreterdefaultnetworkmode)                  | `bridge`                                | none/bridge/host            |
| [`CodeInterpreter:MaxAllowedNetworkMode`](#56-codeinterpretermaxallowednetworkmode)            | `bridge`                                | 限制最大网络权限            |
| [`CodeInterpreter:DefaultResourceLimits`](#57-codeinterpreterdefaultresourcelimits)            | 见详情                                  | 内存2GB/CPU 2核/进程200     |
| [`CodeInterpreter:MaxResourceLimits`](#58-codeinterpretermaxresourcelimits)                    | 全部`null`                              | null表示不限制              |
| [`CodeInterpreter:MaxArtifactsFilesToUpload`](#59-codeinterpretermaxartifactsfilestoupload)    | `50`                                    | 每轮最多上传文件数          |
| [`CodeInterpreter:MaxSingleUploadBytes`](#510-codeinterpretermaxsingleuploadbytes)             | `157286400`                             | 150MB，单文件限制           |
| [`CodeInterpreter:MaxTotalUploadBytesPerTurn`](#511-codeinterpretermaxtotaluploadbytesperturn) | `314572800`                             | 300MB，单轮总限制           |
| [`JwtValidPeriod`](#61-jwtvalidperiod)                                                         | `1.00:00:00`                            | 1天，JWT有效期              |
| [`JwtSecretKey`](#62-jwtsecretkey)                                                             | `null`                                  | 生产环境建议设置稳定密钥    |
| [`Chat:Retry429Times`](#71-chatretry429times)                                                  | `5`                                     | HTTP 429重试次数            |

---

## 1. 前端地址与跨域

### 1.1 `FE_URL`

- **类型**：字符串（URL）
- **默认值**：`http://localhost:3001`
- **用途**：
  - 用于配置后端的 CORS 允许来源（允许前端跨域访问后端 API）。
  - 后端会将该值加入 `FrontendCORS` 策略的 `WithOrigins(...)` 白名单。
  - 代码位置：后端在启动时读取 `FE_URL`，缺失会抛异常（即必须配置）。

- **注意事项**：
  - 除了你配置的 `FE_URL`，后端还额外允许 `http://localhost:3000`（便于本地开发）。

- **环境变量写法**：`FE_URL=http://localhost:3001`
- **命令行写法**：`--FE_URL=http://localhost:3001`

---

## 2. 链接/ID 加密

### 2.1 `ENCRYPTION_PASSWORD`

- **类型**：字符串
- **默认值**：示例占位文本（你应当替换）
- **用途**：
  - 用于对系统中的自增整型 ID 做“URL 友好”的加密/混淆（例如分享链接、部分 API 路径参数等）。
  - 若未配置（为空/全空白），后端会记录警告并退化为 **不加密**（NoOp）。

- **风险与建议**：
  - 生产环境务必设置为**足够长且随机**的字符串，并且保持稳定；变更该值会导致旧的加密 ID/链接可能无法再被识别。

- **环境变量写法**：`ENCRYPTION_PASSWORD=...`
- **命令行写法**：`--ENCRYPTION_PASSWORD=...`

---

## 3. 数据库

### 3.1 `DBType`

- **类型**：字符串
- **默认值**：`sqlite`
- **用途**：指定使用的数据库类型。
- **支持值**：
  - `sqlite`（默认）
  - `mssql` / `sqlserver`
  - `postgresql` / `pgsql`
- **行为说明**：
  - 未配置时等价于 `sqlite`。
  - 配置为未知值会在启动时抛异常并终止启动。

- **环境变量写法**：`DBType=sqlite`
- **命令行写法**：`--DBType=sqlite`

### 3.2 `ConnectionStrings:ChatsDB`

- **类型**：字符串（ADO.NET 连接字符串）
- **默认值**：`Data Source=./AppData/chats.db`
- **用途**：指定数据库连接字符串。
- **行为说明**：
  - 该连接字符串**必须存在**；缺失会在启动时直接抛异常（`ConnectionStrings:ChatsDB not found`）。
  - 当 `DBType=sqlite` 且连接字符串为默认值 `Data Source=./AppData/chats.db` 时，如果当前目录不存在 `AppData`，后端会自动创建该文件夹，改善首次启动体验。

- **环境变量写法**：`ConnectionStrings__ChatsDB=...`
- **命令行写法**：`--ConnectionStrings:ChatsDB=...`

---

## 4. CodePod（Docker 容器底座）

`CodePod` 这组配置用于管理代码解释器沙箱所依赖的 Docker 容器创建、工作目录、输出截断等行为。

> 重要：当前版本的代码解释器工具链在多个地方**默认假设工作目录为 `/app`，Artifacts 目录为 `/app/artifacts`**。除非你知道自己在做什么，否则建议保持 `WorkDir=/app`、`ArtifactsDir=artifacts`。

### 4.1 `CodePod:IsWindowsContainer`

- **类型**：布尔值
- **默认值**：`false`（使用 Linux 容器）
- **用途**：
  - 指示是否使用 Windows 容器（默认使用 Linux 容器）。
  - 影响默认 Docker 端点、容器内部命令（如 keep-alive、mkdir、删除文件等）。

### 4.2 `CodePod:DockerEndpoint`

- **类型**：字符串或 `null`
- **默认值**：`null`（自动选择默认端点）
- **用途**：指定 Docker 服务端点地址。
- **行为说明**：
  - 未配置时会根据 `CodePod:IsWindowsContainer` 自动选择默认端点：
    - Windows 容器：`npipe://./pipe/docker_engine`
    - Linux/macOS 容器：`unix:///var/run/docker.sock`

### 4.3 `CodePod:WorkDir`

- **类型**：字符串（容器内路径）
- **默认值**：`/app`
- **用途**：容器的工作目录（Docker `WorkingDir`）。

### 4.4 `CodePod:ArtifactsDir`

- **类型**：字符串（相对于 `WorkDir` 的子目录名）
- **默认值**：`artifacts`
- **用途**：用于存放导出文件（供下载/上传回传）的目录。

### 4.5 `CodePod:LabelPrefix`

- **类型**：字符串
- **默认值**：`codepod`
- **用途**：
  - 生成容器名称前缀（例如 `codepod-xxxxxxxx`）。
  - 作为 Docker labels 的前缀，标识“由 Chats 管理”的容器，便于清理与筛选。

### 4.6 `CodePod:OutputOptions:MaxOutputBytes`

- **类型**：整数（字节）
- **默认值**：`6144`（6KB）
- **用途**：限制容器命令输出（stdout/stderr）的最大字节数，超过会触发截断（默认策略为保留首尾，并插入“输出已截断”的提示）。

- **环境变量示例**：`CodePod__OutputOptions__MaxOutputBytes=6144`

---

## 5. CodeInterpreter（代码解释器 / 沙箱策略）

`CodeInterpreter` 这组配置控制：默认使用的沙箱镜像、每次命令执行超时、会话空闲回收、网络隔离、资源限制，以及每轮可回传的 artifacts 上传配额。

### 5.1 `CodeInterpreter:DefaultImage`

- **类型**：字符串（Docker 镜像名）
- **默认值**：`sdcb/code-interpreter:r-26`
- **用途**：新建代码解释器会话时使用的默认镜像。

### 5.2 `CodeInterpreter:DefaultImageDescription`

- **类型**：字符串
- **默认值**：`Pre-installed with common packages, suitable for most daily tasks`
- **用途**：用于丰富系统提示词（告诉模型该镜像里有什么能力/工具）。

### 5.3 `CodeInterpreter:DefaultTimeoutSeconds`

- **类型**：整数秒或 `null`
- **默认值**：`300`
- **用途**：单次命令执行默认超时。
- **行为说明**：
  - `null` 表示“近似无限”（实现上会当作 24 小时）。
  - 最终会被夹在 `1..86400` 秒范围内。

### 5.4 `CodeInterpreter:SessionIdleTimeoutSeconds`

- **类型**：整数秒
- **默认值**：`1800`（30 分钟）
- **用途**：会话空闲多久算过期（用于设置会话的 `ExpiresAt`，并由后台清理服务回收容器）。

### 5.5 `CodeInterpreter:DefaultNetworkMode`

- **类型**：字符串
- **默认值**：`bridge`
- **用途**：新建会话默认网络模式。
- **可选值**：`none` | `bridge` | `host`

### 5.6 `CodeInterpreter:MaxAllowedNetworkMode`

- **类型**：字符串
- **默认值**：`bridge`
- **用途**：限制模型在调用工具时“可以请求的最大网络权限”。
- **规则说明**：允许的模式为：所有“等级不高于该值”的模式（`none < bridge < host`）。
  - 例如 `MaxAllowedNetworkMode=bridge` 时，允许 `none`、`bridge`，禁止 `host`。
  - 系统会在启动时校验：`DefaultNetworkMode` 不能高于 `MaxAllowedNetworkMode`。

### 5.7 `CodeInterpreter:DefaultResourceLimits`

- **类型**：对象
- **默认值**：
  - `MemoryBytes`: `2147483648`（2GB）
  - `CpuCores`: `2.0`
  - `MaxProcesses`: `200`
- **用途**：当创建会话时未显式指定资源限制，使用该默认值。
- **字段含义**：
  - `MemoryBytes`：内存上限（字节）
  - `CpuCores`：CPU 核数（可为小数）
  - `MaxProcesses`：进程数上限（Linux 容器生效；Windows 容器不支持该限制）

### 5.8 `CodeInterpreter:MaxResourceLimits`

- **类型**：对象
- **默认值**：全部为 `null`
- **用途**：作为“硬上限”，防止模型/工具请求超过你允许的资源。
- **行为说明**：
  - `null` 表示不限制（会被转换为 Docker 侧的“无限制/0”语义）。

### 5.9 `CodeInterpreter:MaxArtifactsFilesToUpload`

- **类型**：整数
- **默认值**：`50`
- **用途**：每轮最多允许从 `/app/artifacts` 上传/回传的文件数量。

### 5.10 `CodeInterpreter:MaxSingleUploadBytes`

- **类型**：整数（字节）或 `null`
- **默认值**：`157286400`（150MB）
- **用途**：限制单个 artifacts 文件的最大回传大小。
- **行为说明**：`null` 表示不限制。

### 5.11 `CodeInterpreter:MaxTotalUploadBytesPerTurn`

- **类型**：整数（字节）或 `null`
- **默认值**：`314572800`（300MB）
- **用途**：限制“单轮对话”中所有 artifacts 回传的总大小。
- **行为说明**：`null` 表示不限制。

---

## 6. 登录会话与 JWT

### 6.1 `JwtValidPeriod`

- **类型**：TimeSpan 字符串
- **默认值**：`1.00:00:00`（1 天）
- **用途**：控制用户登录后 JWT 的有效期。
- **行为说明**：
  - 若不配置该项，默认有效期为 8 小时。
  - 常见格式：`d.hh:mm:ss`，例如 `0.08:00:00` 表示 8 小时。

### 6.2 `JwtSecretKey`

- **类型**：字符串或 `null`
- **默认值**：`null`
- **用途**：JWT 签名密钥。
- **行为说明**：
  - 若为空，系统会在进程启动时生成一个随机值作为密钥；这会导致**服务重启后旧 JWT 全部失效**（用户需要重新登录）。
  - 生产环境建议配置一个稳定的随机字符串（并妥善保管）。

---

## 7. 聊天请求重试

### 7.1 `Chat:Retry429Times`

- **类型**：整数
- **默认值**：`5`
- **用途**：当上游模型服务返回 HTTP 429（限流）时的重试次数。
- **行为说明**：
  - 仅在“本次请求尚未产生任何输出片段（还没开始流式返回）”时才会触发重试。
  - 采用指数退避：1s、2s、4s、8s……最大 30s，并带 0~250ms 随机抖动。
  - 配置为 `0` 或不配置（为 `null`）则不进行 429 重试。

- **环境变量写法**：`Chat__Retry429Times=5`
- **命令行写法**：`--Chat:Retry429Times=5`
