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
mkdir -p ./AppData
chmod 755 ./AppData
docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:/app/AppData -v /var/run/docker.sock:/var/run/docker.sock --user 0:0 -p 8080:8080 sdcb/chats:latest
```

> **Note**:
> - SQLite requires mapping the `./AppData` folder to store the database file and uploaded files (when using local file provider for image hosting service).
> - `-v /var/run/docker.sock:/var/run/docker.sock` and `--user 0:0` are for supporting Docker sandbox-based Code Interpreter functionality. If you don't need this feature, you can remove these two parameters.

### PostgreSQL Quick Start

```bash
docker run --restart unless-stopped --name sdcb-chats -e DBType=postgresql -e ConnectionStrings__ChatsDB="Host=host.docker.internal;Port=5432;Username=postgres;Password=mysecretpassword;Database=postgres" -v /var/run/docker.sock:/var/run/docker.sock --user 0:0 -p 8080:8080 sdcb/chats:latest
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

> 💾 **Docker Image List**: For detailed Docker version information and usage examples, please refer to the [Downloads page](./downloads.md).

## Executable Deployment Guide

For environments where using Docker is inconvenient, Chats provides native executable files for 8 operating systems or architectures, which can be run directly without installing any runtime environment.

> 💾 **Download Links**: For a complete list of download links (including GitHub and mirrors), please refer to the [Downloads page](./downloads.md).

### Running Instructions

The directory structure after extracting the executable files is as follows:

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
  .\Chats.BE.exe --urls http://+:5000 --CodePod:DockerEndpoint npipe://./pipe/docker_engine --DBType=mssql --ConnectionStrings:ChatsDB="Data Source=(localdb)\mssqllocaldb; Initial Catalog=ChatsDB; Integrated Security=True"
  ```
  - Parameter `--urls`: Used to specify the address and port the application listens on.
  - Parameter `--CodePod:DockerEndpoint`: Specifies the Docker service endpoint address. On Windows, if you want to connect to Linux containers in Docker Desktop as the Code Interpreter sandbox, you need to use `npipe://./pipe/docker_engine` instead of the default `unix:///var/run/docker.sock`, otherwise you'll encounter a `Connection failed` error when creating Docker sessions.
  - Parameter `DBType`: Options are `sqlite`, `mssql`, or `pgsql`.
  - Parameter `--ConnectionStrings:ChatsDB`: For specifying the ADO.NET connection string for the database.
  
  > For more configuration options, please refer to the [Configuration Guide](./configuration.md).

### .NET Runtime Dependent Version

For the downloaded `chats.zip`, you need to install .NET 10 runtime. After installation, use the following command to start:

```bash
dotnet Chats.BE.dll
```

Download .NET Runtime: [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
