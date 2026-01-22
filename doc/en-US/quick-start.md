# Quick Start

**English** | [简体中文](../zh-CN/quick-start.md)

## System Requirements

- **Docker Deployment**: Any system that supports Docker (Linux/Windows/macOS)
- **Executable Deployment**:
  - Windows: Windows 10 or higher
  - Linux: glibc 2.17+ or musl libc
  - macOS: macOS 10.15 or higher
- **Database**: SQLite (default, no installation required) / SQL Server / PostgreSQL

## Docker Deployment

For most users, Docker provides the simplest and fastest way to deploy.

### SQLite Quick Start

```bash
mkdir -p ./AppData && chmod 755 ./AppData && docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:/app/AppData -p 8080:8080 sdcb/chats:latest
```

> **Note**: SQLite requires mapping the `./AppData` folder to store the database file and uploaded files (when using local file provider for image hosting service).

### PostgreSQL Quick Start

```bash
docker run --restart unless-stopped --name sdcb-chats -e DBType=postgresql -e ConnectionStrings__ChatsDB="Host=host.docker.internal;Port=5432;Username=postgres;Password=mysecretpassword;Database=postgres" -p 8080:8080 sdcb/chats:latest
```

> **Note**: PostgreSQL does not depend on the `./AppData` folder for database storage, but if using local file provider for image hosting service, you still need to map the folder: `-v ./AppData:/app/AppData` (users can configure other file storage methods in the admin interface).

### Configuration Instructions

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

### Docker Image List

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

## Executable Deployment Guide

For environments where using Docker is inconvenient, Chats provides native executable files for 8 operating systems or architectures, which can be run directly without installing any runtime environment.

### Download Links

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

### Version Information

- **Latest Version**: Visit the [Releases](https://github.com/sdcb/chats/releases) page to view the latest version and changelog
- **Alternative Download**: When GitHub access is inconvenient, use the following format for the domestic mirror address:
  ```
  https://chats.sdcb.pub/release/latest/{artifact-id}.zip
  ```
  For example: `https://chats.sdcb.pub/release/latest/chats-win-x64.zip`

### Running Instructions

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

### .NET Runtime Dependent Version

For the downloaded `chats.zip`, you need to install .NET 10 runtime. After installation, use the following command to start:

```bash
dotnet Chats.BE.dll
```

Download .NET Runtime: [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
