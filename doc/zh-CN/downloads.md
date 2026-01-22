# 下载地址

[English](../en-US/downloads.md) | **简体中文**

本页面提供 Chats 的所有下载方式，包括 Docker 镜像和原生可执行文件。

## Docker 镜像列表

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

### 版本说明

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

### 使用示例

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

---

## 原生可执行文件下载

对于不便使用 Docker 部署的环境，Chats 提供了 8 种操作系统或架构的原生可执行文件，无需安装任何运行时环境即可直接运行。

### 下载地址

| 平台                   | GitHub 下载（最新稳定版）                                                                                       | 镜像下载（最新稳定版）                                                                         | 镜像下载（最新开发版）                                     |
| ---------------------- | --------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| Windows 64位           | [chats-win-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-win-x64.zip)                   | [chats-win-x64.zip](https://chats.sdcb.pub/release/latest/chats-win-x64.zip)                   | -                                                          |
| Linux 64位             | [chats-linux-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-x64.zip)               | [chats-linux-x64.zip](https://chats.sdcb.pub/release/latest/chats-linux-x64.zip)               | -                                                          |
| Linux ARM64            | [chats-linux-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-arm64.zip)           | [chats-linux-arm64.zip](https://chats.sdcb.pub/release/latest/chats-linux-arm64.zip)           | -                                                          |
| Linux musl x64         | [chats-linux-musl-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-x64.zip)     | [chats-linux-musl-x64.zip](https://chats.sdcb.pub/release/latest/chats-linux-musl-x64.zip)     | -                                                          |
| Linux musl ARM64       | [chats-linux-musl-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-arm64.zip) | [chats-linux-musl-arm64.zip](https://chats.sdcb.pub/release/latest/chats-linux-musl-arm64.zip) | -                                                          |
| macOS ARM64            | [chats-osx-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-arm64.zip)               | [chats-osx-arm64.zip](https://chats.sdcb.pub/release/latest/chats-osx-arm64.zip)               | -                                                          |
| macOS x64              | [chats-osx-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-x64.zip)                   | [chats-osx-x64.zip](https://chats.sdcb.pub/release/latest/chats-osx-x64.zip)                   | -                                                          |
| 通用包（需要 .NET 10） | [chats.zip](https://github.com/sdcb/chats/releases/latest/download/chats.zip)                                   | [chats.zip](https://chats.sdcb.pub/release/latest/chats.zip)                                   | [chats.zip](https://chats.sdcb.pub/latest/chats.zip)       |
| 纯前端文件             | [chats-fe.zip](https://github.com/sdcb/chats/releases/latest/download/chats-fe.zip)                             | [chats-fe.zip](https://chats.sdcb.pub/release/latest/chats-fe.zip)                             | [chats-fe.zip](https://chats.sdcb.pub/latest/chats-fe.zip) |

### 下载说明

- **国内镜像下载**（基于 Cloudflare R2）：推荐国内用户使用，速度更快
- **最新开发版下载**：仅提供通用包和纯前端文件包，不包括其他平台的原生可执行文件
  - ⚠️ 注意：开发版会从 `dev`/`feature` 分支自动更新，可能不稳定
- 除通用包外，所有平台都提供 AOT 编译的原生可执行文件，启动速度快，内存占用低

### 版本说明

- **最新版本**：访问 [Releases](https://github.com/sdcb/chats/releases) 页面查看最新版本和更新日志
- **替代下载**：在 GitHub 访问不便时，可使用以下格式的国内镜像地址：
  ```
  https://chats.sdcb.pub/release/latest/{artifact-id}.zip
  ```
  例如：`https://chats.sdcb.pub/release/latest/chats-win-x64.zip`

---

## 相关链接

- [快速开始](./quick-start.md) - 部署指南
- [配置说明](./configuration.md) - 详细配置参数
- [常见问题](./faq.md) - 部署和使用中的常见问题
