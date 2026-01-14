# Sdcb Chats [![docker pulls](https://img.shields.io/docker/pulls/sdcb/chats)](https://hub.docker.com/r/sdcb/chats) [![QQ](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=social&logo=tencent-qq&logoColor=000&logoWidth=20)](https://qm.qq.com/q/AM8tY9cAsS) [![License](https://img.shields.io/github/license/sdcb/chats)](LICENSE) [![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sdcb/chats) [![Release Notes](https://img.shields.io/static/v1?label=%F0%9F%93%9D%20&message=Release%20Notes&color=8A2BE2)](./doc/en-US/release-notes/README.md)

**English** | **[简体中文](README.md)**

Sdcb Chats is a powerful and flexible frontend for large language models, supporting 21+ mainstream AI model providers. Whether you want to unify the management of multiple model interfaces or need a simple and easy-to-use deployment solution, Sdcb Chats can meet your needs.

## ✨ Why Choose Sdcb Chats

- 🚀 **All-in-One**: One hub for 21+ AI model providers
- 🎯 **Ready in Minutes**: One-command Docker deploy, plus native executables for 8 platforms
- 🐳 **Code Interpreter**: Docker sandbox with built-in tools (browser, code execution, Excel, and more)
- 🔌 **API Gateway**: Chat Completions/Messages compatible, works with Claude Code
- 🌐 **Standard APIs**: Chat Completions/Messages/Responses/Gemini, with interleaved thinking
- 👁️ **Multimodal**: Vision in, images out
- 💾 **Storage Freedom**: SQLite/SQL Server/PostgreSQL, plus Local/S3/OSS/Azure Blob
- 🔐 **Enterprise Security**: Permissions & balance control, rate limiting & audit logs, Keycloak SSO & SMS login

<img alt="chats-en" src="https://github.com/user-attachments/assets/40d2376e-58a0-4309-a2f5-5ed8262a0c2e" />

## Quick Start

### System Requirements

- **Docker Deployment**: Any system that supports Docker (Linux/Windows/macOS)
- **Executable Deployment**:
  - Windows: Windows 10 or higher
  - Linux: glibc 2.17+ or musl libc
  - macOS: macOS 10.15 or higher
- **Database**: SQLite (default, no installation required) / SQL Server / PostgreSQL

### Docker Deployment

For most users, Docker provides the simplest and fastest way to deploy.

#### SQLite Quick Start

```bash
mkdir -p ./AppData && chmod 755 ./AppData && docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:/app/AppData -p 8080:8080 sdcb/chats:latest
```

> **Note**: SQLite requires mapping the `./AppData` folder to store the database file and uploaded files (when using local file provider for image hosting service).

#### PostgreSQL Quick Start

```bash
docker run --restart unless-stopped --name sdcb-chats -e DBType=postgresql -e ConnectionStrings__ChatsDB="Host=host.docker.internal;Port=5432;Username=postgres;Password=mysecretpassword;Database=postgres" -p 8080:8080 sdcb/chats:latest
```

> **Note**: PostgreSQL does not depend on the `./AppData` folder for database storage, but if using local file provider for image hosting service, you still need to map the folder: `-v ./AppData:/app/AppData` (users can configure other file storage methods in the admin interface).

#### Configuration Instructions

- **Database storage location**: By default, the SQLite database for Chats will be created in the `./AppData` directory. To avoid accidental clearing of the database each time the Docker container is restarted, we first create an `AppData` folder and set its permissions to writable (`chmod 755`, using 777 is not recommended for security reasons)
  
- **Port mapping**: This command maps port 8080 of the container to port 8080 of the host, allowing you to access the application via `http://localhost:8080`

- **Database type configuration**: The `DBType` environment variable specifies the database type, with the default value being `sqlite`. Besides SQLite, the application also supports using `mssql` (or `sqlserver`) and `postgresql` (or `pgsql`) as database options

- **Connection string**: The default value of `ConnectionStrings__ChatsDB` is `Data Source=./AppData/chats.db`, which is the ADO.NET connection string for connecting to the database

- **Non-first-time run**: If your `AppData` directory is already created and Docker has write permission to it, you can simplify the start command as follows:

    ```bash
    docker run --restart unless-stopped --name sdcb-chats -v ./AppData:/app/AppData -p 8080:8080 sdcb/chats:latest
    ```

- **Database initialization**: After the container starts, if the database file does not exist, it will be automatically created and initial data inserted
  - Initial admin username: `chats`
  - Initial default password: `RESET!!!`
  - ⚠️ **Important**: Please change the password immediately after the first login by going to the user management interface in the bottom left corner to ensure system security

By following the above steps, you will be able to use Docker to successfully deploy and run the application. If you encounter any problems during deployment, please contact us via [Issues](https://github.com/sdcb/chats/issues) or [QQ Group](https://qm.qq.com/q/AM8tY9cAsS).

#### Docker Image List

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

**Version Information:**

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

**Examples:**

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

### Executable Deployment Guide

For environments where using Docker is inconvenient, Chats provides native executable files for 8 operating systems or architectures, which can be run directly without installing any runtime environment.

#### Download Links

| Platform                   | GitHub Download (All Releases)                                                                                  | Mirror Download (Latest Stable)                                                                |
| -------------------------- | --------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| Windows 64-bit             | [chats-win-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-win-x64.zip)                   | [chats-win-x64.zip](https://chats.sdcb.pub/release/latest/chats-win-x64.zip)                   |
| Linux 64-bit               | [chats-linux-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-x64.zip)               | [chats-linux-x64.zip](https://chats.sdcb.pub/release/latest/chats-linux-x64.zip)               |
| Linux ARM64                | [chats-linux-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-arm64.zip)           | [chats-linux-arm64.zip](https://chats.sdcb.pub/release/latest/chats-linux-arm64.zip)           |
| Linux musl x64             | [chats-linux-musl-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-x64.zip)     | [chats-linux-musl-x64.zip](https://chats.sdcb.pub/release/latest/chats-linux-musl-x64.zip)     |
| Linux musl ARM64           | [chats-linux-musl-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-linux-musl-arm64.zip) | [chats-linux-musl-arm64.zip](https://chats.sdcb.pub/release/latest/chats-linux-musl-arm64.zip) |
| macOS ARM64                | [chats-osx-arm64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-arm64.zip)               | [chats-osx-arm64.zip](https://chats.sdcb.pub/release/latest/chats-osx-arm64.zip)               |
| macOS x64                  | [chats-osx-x64.zip](https://github.com/sdcb/chats/releases/latest/download/chats-osx-x64.zip)                   | [chats-osx-x64.zip](https://chats.sdcb.pub/release/latest/chats-osx-x64.zip)                   |
| Generic (requires .NET 10) | [chats.zip](https://github.com/sdcb/chats/releases/latest/download/chats.zip)                                   | [chats.zip](https://chats.sdcb.pub/release/latest/chats.zip)                                   |
| Frontend files only        | [chats-fe.zip](https://github.com/sdcb/chats/releases/latest/download/chats-fe.zip)                             | [chats-fe.zip](https://chats.sdcb.pub/release/latest/chats-fe.zip)                             |

> **💡 Download Tips**:
> - **Mirror Download** (powered by Cloudflare R2): Recommended for users in China for faster speed
> - **Latest Development Build**: For bleeding-edge features, development builds provide the following files
>   - Generic package: [chats.zip](https://chats.sdcb.pub/latest/chats.zip) (requires .NET 10)
>   - Frontend files: [chats-fe.zip](https://chats.sdcb.pub/latest/chats-fe.zip)
>   - ⚠️ Note: Development builds are automatically updated from `dev`/`feature` branches and may be unstable
> - All platforms (except Generic) provide AOT-compiled native executables with fast startup and low memory footprint

#### Version Information

- **Latest Version**: Visit the [Releases](https://github.com/sdcb/chats/releases) page to view the latest version and changelog
- **Alternative Download**: When GitHub access is inconvenient, use the following format for the domestic mirror address:
  ```
  https://chats.sdcb.pub/release/latest/{artifact-id}.zip
  ```
  For example: `https://chats.sdcb.pub/release/latest/chats-win-x64.zip`

#### Running Instructions

The directory structure after extracting the AOT executable files is as follows:

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

- **Start Application**: Run `Chats.BE.exe` to start the Chats application. Although this filename indicates "backend," it actually contains both frontend and backend components.
- **Database Configuration**: By default, the application will create a directory named `AppData` in the current directory and use SQLite as the database. Command-line parameters can be used to specify a different database type:
  ```pwsh
  .\Chats.BE.exe --urls http://+:5000 --DBType=mssql --ConnectionStrings:ChatsDB="Data Source=(localdb)\mssqllocaldb; Initial Catalog=ChatsDB; Integrated Security=True"
  ```
  - Parameter `--urls`: Used to specify the address and port the application listens on.
  - Parameter `DBType`: Options are `sqlite`, `mssql`, or `pgsql`.
  - Parameter `--ConnectionStrings:ChatsDB`: For specifying the ADO.NET connection string for the database.

#### .NET Runtime Dependent Version

For the downloaded `chats.zip`, you need to install .NET 10 runtime. After installation, use the following command to start:

```bash
dotnet Chats.BE.dll
```

Download .NET Runtime: [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

---

## Supported LLM Services

| Id  | Name             | Added                                                       | Interleaved Thinking |
| --- | ---------------- | ----------------------------------------------------------- | -------------------- |
| 0   | Test             | [2024-11-18](https://github.com/sdcb/chats/commit/66d011b1) |                      |
| 1   | Azure AI Foundry | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ✅                    |
| 2   | Tencent Hunyuan  | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ❓                    |
| 3   | 01.ai            | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) |                      |
| 4   | Moonshot         | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ✅                    |
| 5   | OpenAI           | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ✅                    |
| 6   | Wenxin Qianfan   | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) |                      |
| 7   | Alibaba Bailian  | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ❓                    |
| 8   | Xunfei SparkDesk | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) |                      |
| 9   | Zhipu AI         | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ❓                    |
| 10  | DeepSeek         | [2024-12-06](https://github.com/sdcb/chats/commit/30db0079) | ✅                    |
| 11  | x.ai             | [2024-12-11](https://github.com/sdcb/chats/commit/0d1cab20) |                      |
| 12  | Github Models    | [2024-12-11](https://github.com/sdcb/chats/commit/0d1cab20) |                      |
| 13  | Google AI        | [2025-01-10](https://github.com/sdcb/chats/commit/a4effc1b) | ✅                    |
| 14  | Ollama           | [2025-01-20](https://github.com/sdcb/chats/commit/6a5288e7) |                      |
| 15  | MiniMax          | [2025-01-20](https://github.com/sdcb/chats/commit/6a5288e7) | ✅                    |
| 16  | Doubao           | [2025-01-24](https://github.com/sdcb/chats/commit/843510ff) | ❓                    |
| 17  | SiliconFlow      | [2025-02-08](https://github.com/sdcb/chats/commit/889144cf) | ❓                    |
| 18  | OpenRouter       | [2025-03-05](https://github.com/sdcb/chats/commit/15adedfe) | ❓                    |
| 19  | Token Pony       | [2025-11-07](https://github.com/sdcb/chats/commit/32e4a0d5) | ❓                    |
| 20  | Anthropic        | [2025-11-24](https://github.com/sdcb/chats/commit/22ebef98) | ✅                    |
| 21  | Xiaomi Mimo      | [2025-12-17](https://github.com/sdcb/chats/commit/026f1a4e) | ✅                    |

**Note:**

- ✅ Any model provider that complies with the OpenAI Chat Completion API protocol can be accessed through Chats
- 🤖 OpenAI/Azure AI Foundry's o3/o4-mini/gpt-5 series models use the Response API protocol (not Chat Completion API), supporting thought summary and thought process features
- 🌐 Google AI's Gemini model uses the native Google Gemini API protocol
- ❓ The provider uses an Anthropic Messages API–based implementation. It should support interleaved thinking by protocol, but it's not end-to-end tested, so full support is unverified.

---

## Development Documentation

Chats is developed using `C#`/`TypeScript`. For information on how to compile and develop Chats, please refer to:

- [🛠️ Development Guide](./doc/en-US/build.md)

---

## FAQ

<details>
<summary><b>How to change the default port?</b></summary>

Use the `--urls` parameter when starting:

```bash
# Docker
docker run -e ASPNETCORE_URLS="http://+:5000" -p 5000:5000 sdcb/chats:latest

# Executable
./Chats.BE.exe --urls http://+:5000
```
</details>

<details>
<summary><b>How to switch to SQL Server or PostgreSQL?</b></summary>

Use the `--DBType` and `--ConnectionStrings:ChatsDB` parameters:

```bash
# SQL Server
./Chats.BE.exe --DBType=mssql --ConnectionStrings:ChatsDB="Server=localhost;Database=ChatsDB;User Id=sa;Password=YourPassword"

# PostgreSQL
./Chats.BE.exe --DBType=pgsql --ConnectionStrings:ChatsDB="Host=localhost;Database=chatsdb;Username=postgres;Password=YourPassword"
```
</details>

<details>
<summary><b>How to configure file storage services?</b></summary>

Chats supports multiple file storage services, which can be configured in the system settings of the management interface:
- Local file system
- AWS S3
- Minio
- Aliyun OSS
- Azure Blob Storage

Configuration takes effect without restart.
</details>

<details>
<summary><b>What if I forget the administrator password?</b></summary>

You can reset the password directly through the database, or delete the database file and reinitialize (remember to backup data).
</details>

<details>
<summary><b>How to use Docker deployment on pure Windows environment?</b></summary>

Pure Windows environment with SQLite database:

```powershell
mkdir AppData
icacls .\AppData /grant "Users:(OI)(CI)(M)" /T
docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:C:/app/AppData -p 8080:8080 sdcb/chats:latest
```

Pure Windows environment with PostgreSQL database:

```powershell
mkdir AppData
icacls .\AppData /grant "Users:(OI)(CI)(M)" /T
docker run --restart unless-stopped --name sdcb-chats -e DBType=postgresql -e ConnectionStrings__ChatsDB="Host=host.docker.internal;Port=5432;Username=postgres;Password=YourPassword;Database=postgres" -v ./AppData:C:/app/AppData -p 8080:8080 sdcb/chats:latest
```

**Note**: When accessing host services from container, use `host.docker.internal` instead of `localhost`.
</details>

---

## Contributing

We welcome contributions of all kinds, including but not limited to:

- 🐛 Report bugs
- 💡 Suggest new features
- 📝 Improve documentation
- 🔧 Submit code

Please submit issues or suggestions via [GitHub Issues](https://github.com/sdcb/chats/issues).

---

## Contact

- **GitHub Issues**: [https://github.com/sdcb/chats/issues](https://github.com/sdcb/chats/issues)
- **QQ Group**: 498452653 [![Join QQ Group](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=flat&logo=tencent-qq)](https://qm.qq.com/q/AM8tY9cAsS)

---

## License

This project is licensed under the [Apache 2.0](LICENSE).

---

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=sdcb/chats&type=Date)](https://star-history.com/#sdcb/chats&Date)

---

**If this project helps you, please give it a ⭐ Star!**
