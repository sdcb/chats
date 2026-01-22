# Downloads

**English** | [简体中文](../zh-CN/downloads.md)

This page provides all download options for Chats, including Docker images and native executable files.

## Docker Image List

Chats provides the following images:

| Description                   | Docker Image                                         |
| ----------------------------- | ---------------------------------------------------- |
| Latest (Recommended)          | `docker.io/sdcb/chats:latest`                        |
| Specific Full Version         | `docker.io/sdcb/chats:{version}`                     |
| Specific Major Version        | `docker.io/sdcb/chats:{major}`                       |
| Specific Minor Version        | `docker.io/sdcb/chats:{major.minor}`                 |
| Linux x64                     | `docker.io/sdcb/chats:{version}-linux-x64`           |
| Linux ARM64                   | `docker.io/sdcb/chats:{version}-linux-arm64`         |
| Windows Nano Server LTSC 2022 | `docker.io/sdcb/chats:{version}-nanoserver-ltsc2022` |
| Windows Nano Server LTSC 2025 | `docker.io/sdcb/chats:{version}-nanoserver-ltsc2025` |

### Version Information

- **Version Format**: Uses semantic versioning, e.g., `1.8.1`
  - `{major}`: Major version number, e.g., `1`
  - `{major.minor}`: Major.minor version number, e.g., `1.8`
  - `{version}`: Full version number, e.g., `1.8.1`

- **Multi-platform Support**: `latest` and version tags (like `1.8.1`, `1.8`, `1`) are multi-platform images containing:
  - Linux x64
  - Linux ARM64
  - Windows Nano Server LTSC 2022 (for Windows Server 2022)
  - Windows Nano Server LTSC 2025 (for Windows Server 2025)

- **Automatic Platform Selection**: When using `docker pull`, you don't need to specify the operating system version. Docker will automatically select the correct version for your system through manifest

### Usage Examples

```bash
# Use latest version (recommended)
docker pull sdcb/chats:latest

# Use specific version
docker pull sdcb/chats:1.8.1

# Use major version (automatically gets latest 1.x.x)
docker pull sdcb/chats:1

# Use minor version (automatically gets latest 1.8.x)
docker pull sdcb/chats:1.8

# Specify particular platform (usually not needed)
docker pull sdcb/chats:1.8.1-linux-x64
```

---

## Native Executable Downloads

For environments where using Docker is inconvenient, Chats provides native executable files for 8 operating systems or architectures, which can be run directly without installing any runtime environment.

### Download Links

| Platform                   | GitHub Download (Latest Stable)                                                                                 | Mirror (Latest Stable)                                                                         | Mirror (Latest Development)                                                            |
| -------------------------- | --------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| Windows 64-bit             | [chats-win-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-win-x64.zip)                   | [chats-win-x64.zip](https://chats.sdcb.pub/release/latest/chats-win-x64.zip)                   | [chats-win-x64.zip](https://chats.sdcb.pub/latest/chats-win-x64.zip)                   |
| Linux 64-bit               | [chats-linux-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-x64.zip)               | [chats-linux-x64.zip](https://chats.sdcb.pub/release/latest/chats-linux-x64.zip)               | [chats-linux-x64.zip](https://chats.sdcb.pub/latest/chats-linux-x64.zip)               |
| Linux ARM64                | [chats-linux-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-arm64.zip)           | [chats-linux-arm64.zip](https://chats.sdcb.pub/release/latest/chats-linux-arm64.zip)           | [chats-linux-arm64.zip](https://chats.sdcb.pub/latest/chats-linux-arm64.zip)           |
| Linux musl x64             | [chats-linux-musl-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-x64.zip)     | [chats-linux-musl-x64.zip](https://chats.sdcb.pub/release/latest/chats-linux-musl-x64.zip)     | [chats-linux-musl-x64.zip](https://chats.sdcb.pub/latest/chats-linux-musl-x64.zip)     |
| Linux musl ARM64           | [chats-linux-musl-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-arm64.zip) | [chats-linux-musl-arm64.zip](https://chats.sdcb.pub/release/latest/chats-linux-musl-arm64.zip) | [chats-linux-musl-arm64.zip](https://chats.sdcb.pub/latest/chats-linux-musl-arm64.zip) |
| macOS ARM64                | [chats-osx-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-arm64.zip)               | [chats-osx-arm64.zip](https://chats.sdcb.pub/release/latest/chats-osx-arm64.zip)               | [chats-osx-arm64.zip](https://chats.sdcb.pub/latest/chats-osx-arm64.zip)               |
| macOS x64                  | [chats-osx-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-x64.zip)                   | [chats-osx-x64.zip](https://chats.sdcb.pub/release/latest/chats-osx-x64.zip)                   | [chats-osx-x64.zip](https://chats.sdcb.pub/latest/chats-osx-x64.zip)                   |
| Generic (Requires .NET 10) | [chats.zip](https://github.com/sdcb/chats/releases/latest/download/chats.zip)                                   | [chats.zip](https://chats.sdcb.pub/release/latest/chats.zip)                                   | [chats.zip](https://chats.sdcb.pub/latest/chats.zip)                                   |
| Frontend Only              | [chats-fe.zip](https://github.com/sdcb/chats/releases/latest/download/chats-fe.zip)                             | [chats-fe.zip](https://chats.sdcb.pub/release/latest/chats-fe.zip)                             | [chats-fe.zip](https://chats.sdcb.pub/latest/chats-fe.zip)                             |

### Download Notes

- **Mirror Download** (Based on Cloudflare R2): Recommended for users in China, faster speed
- **Latest Development Build**: To experience the latest features, development builds provide:
  - Generic package: [chats.zip](https://chats.sdcb.pub/latest/chats.zip) (Requires .NET 10)
  - Frontend files: [chats-fe.zip](https://chats.sdcb.pub/latest/chats-fe.zip)
  - ⚠️ Note: Development builds auto-update from `dev`/`feature` branches and may be unstable
- Except for the generic package, all platforms provide AOT-compiled native executables with fast startup and low memory footprint

### Version Notes

- **Latest Version**: Visit the [Releases](https://github.com/sdcb/chats/releases) page to view the latest version and changelog
- **Alternative Download**: When GitHub access is inconvenient, use mirror URLs in this format:
  ```
  https://chats.sdcb.pub/release/latest/{artifact-id}.zip
  ```
  Example: `https://chats.sdcb.pub/release/latest/chats-win-x64.zip`

---

## Related Links

- [Quick Start](./quick-start.md) - Deployment guide
- [Configuration Guide](./configuration.md) - Detailed configuration parameters
- [FAQ](./faq.md) - Common questions about deployment and usage
