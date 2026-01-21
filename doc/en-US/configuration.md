# Configuration Guide (appsettings.json)

This document explains the configuration items in the backend configuration file (based on `src/BE/web/appsettings.json`), organized by functionality and explained item by item.

> Convention: This document intentionally does not cover ASP.NET Core general items (such as `Logging`, `AllowedHosts`). For general configuration details, please visit the [official ASP.NET Core Configuration documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/).

## 0. Configuration Sources and Priority

Chats reads configuration based on the .NET configuration system, with priority from high to low:

1. **Command-line arguments** (e.g., `--DBType=sqlite`, `--ConnectionStrings:ChatsDB=...`)
2. **Environment variables** (e.g., `DBType`, `ConnectionStrings__ChatsDB`)
3. **appsettings.json** (default configuration file)

> Tip: Nested configurations use double underscores `__` in environment variables to represent hierarchy, such as `ConnectionStrings__ChatsDB`, `CodeInterpreter__DefaultTimeoutSeconds`.

### Configuration Summary

| Configuration Name                                                                             | Default Value                           | Notes                                       |
| ---------------------------------------------------------------------------------------------- | --------------------------------------- | ------------------------------------------- |
| [`FE_URL`](#11-fe_url)                                                                         | `http://localhost:3001`                 | Required, affects CORS policy               |
| [`ENCRYPTION_PASSWORD`](#21-encryption_password)                                               | Example placeholder text                | Must set random string in production        |
| [`DBType`](#31-dbtype)                                                                         | `sqlite`                                | Supports sqlite/mssql/postgresql            |
| [`ConnectionStrings:ChatsDB`](#32-connectionstringschatsdb)                                    | `Data Source=./AppData/chats.db`        | Required                                    |
| [`CodePod:IsWindowsContainer`](#41-codepodiswindowscontainer)                                  | `false`                                 | Affects Docker endpoint and commands        |
| [`CodePod:DockerEndpoint`](#42-codepoddockerendpoint)                                          | `null`                                  | Auto-selects default endpoint when null     |
| [`CodePod:WorkDir`](#43-codepodworkdir)                                                        | `/app`                                  | Recommended to keep default                 |
| [`CodePod:ArtifactsDir`](#44-codepodartifactsdir)                                              | `artifacts`                             | Subdirectory relative to WorkDir            |
| [`CodePod:LabelPrefix`](#45-codepodlabelprefix)                                                | `codepod`                               | Used for container naming and labels        |
| [`CodePod:OutputOptions:MaxOutputBytes`](#46-codepodoutputoptionsmaxoutputbytes)               | `6144`                                  | 6KB, truncates when exceeded                |
| [`CodeInterpreter:DefaultImage`](#51-codeinterpreterdefaultimage)                              | `sdcb/code-interpreter:r-26`            | Default image for new sessions              |
| [`CodeInterpreter:DefaultImageDescription`](#52-codeinterpreterdefaultimagedescription)        | `Pre-installed with common packages...` | Enriches system prompt                      |
| [`CodeInterpreter:DefaultTimeoutSeconds`](#53-codeinterpreterdefaulttimeoutseconds)            | `300`                                   | null means approximately unlimited (24h)    |
| [`CodeInterpreter:SessionIdleTimeoutSeconds`](#54-codeinterpretersessionidletimeoutseconds)    | `1800`                                  | 30 minutes, session idle timeout            |
| [`CodeInterpreter:DefaultNetworkMode`](#55-codeinterpreterdefaultnetworkmode)                  | `bridge`                                | none/bridge/host                            |
| [`CodeInterpreter:MaxAllowedNetworkMode`](#56-codeinterpretermaxallowednetworkmode)            | `bridge`                                | Limits maximum network permissions          |
| [`CodeInterpreter:DefaultResourceLimits`](#57-codeinterpreterdefaultresourcelimits)            | See details                             | Memory 2GB/CPU 2 cores/Processes 200        |
| [`CodeInterpreter:MaxResourceLimits`](#58-codeinterpretermaxresourcelimits)                    | All `null`                              | null means no limit                         |
| [`CodeInterpreter:MaxArtifactsFilesToUpload`](#59-codeinterpretermaxartifactsfilestoupload)    | `50`                                    | Max files to upload per turn                |
| [`CodeInterpreter:MaxSingleUploadBytes`](#510-codeinterpretermaxsingleuploadbytes)             | `157286400`                             | 150MB, single file limit                    |
| [`CodeInterpreter:MaxTotalUploadBytesPerTurn`](#511-codeinterpretermaxtotaluploadbytesperturn) | `314572800`                             | 300MB, total limit per turn                 |
| [`JwtValidPeriod`](#61-jwtvalidperiod)                                                         | `1.00:00:00`                            | 1 day, JWT validity period                  |
| [`JwtSecretKey`](#62-jwtsecretkey)                                                             | `null`                                  | Recommended to set stable key in production |
| [`Chat:Retry429Times`](#71-chatretry429times)                                                  | `5`                                     | HTTP 429 retry count                        |

---

## 1. Frontend URL and CORS

### 1.1 `FE_URL`

- **Type**: String (URL)
- **Default**: `http://localhost:3001`
- **Purpose**:
  - Used to configure the backend's CORS allowed origins (allowing frontend cross-origin access to backend APIs).
  - The backend adds this value to the `WithOrigins(...)` whitelist of the `FrontendCORS` policy.
  - Code location: The backend reads `FE_URL` on startup; missing it will throw an exception (i.e., it must be configured).

- **Notes**:
  - In addition to the configured `FE_URL`, the backend also allows `http://localhost:3000` (for local development convenience).

- **Environment variable syntax**: `FE_URL=http://localhost:3001`
- **Command-line syntax**: `--FE_URL=http://localhost:3001`

---

## 2. Link/ID Encryption

### 2.1 `ENCRYPTION_PASSWORD`

- **Type**: String
- **Default**: Example placeholder text (you should replace it)
- **Purpose**:
  - Used for "URL-friendly" encryption/obfuscation of auto-incrementing integer IDs in the system (e.g., share links, some API path parameters, etc.).
  - If not configured (empty/whitespace), the backend logs a warning and falls back to **no encryption** (NoOp).

- **Risks and recommendations**:
  - In production, be sure to set this to a **sufficiently long and random** string, and keep it stable; changing this value will cause old encrypted IDs/links to potentially become unrecognizable.

- **Environment variable syntax**: `ENCRYPTION_PASSWORD=...`
- **Command-line syntax**: `--ENCRYPTION_PASSWORD=...`

---

## 3. Database

### 3.1 `DBType`

- **Type**: String
- **Default**: `sqlite`
- **Purpose**: Specify the database type to use.
- **Supported values**:
  - `sqlite` (default)
  - `mssql` / `sqlserver`
  - `postgresql` / `pgsql`
- **Behavior**:
  - When not configured, it's equivalent to `sqlite`.
  - Configuring an unknown value will throw an exception and terminate startup.

- **Environment variable syntax**: `DBType=sqlite`
- **Command-line syntax**: `--DBType=sqlite`

### 3.2 `ConnectionStrings:ChatsDB`

- **Type**: String (ADO.NET connection string)
- **Default**: `Data Source=./AppData/chats.db`
- **Purpose**: Specify the database connection string.
- **Behavior**:
  - This connection string **must exist**; missing it will throw an exception on startup (`ConnectionStrings:ChatsDB not found`).
  - When `DBType=sqlite` and the connection string is the default value `Data Source=./AppData/chats.db`, if the `AppData` directory doesn't exist in the current directory, the backend will automatically create it to improve first-run experience.

- **Environment variable syntax**: `ConnectionStrings__ChatsDB=...`
- **Command-line syntax**: `--ConnectionStrings:ChatsDB=...`

---

## 4. CodePod (Docker Container Foundation)

The `CodePod` configuration group manages Docker container creation, working directories, output truncation, and other behaviors that the code interpreter sandbox depends on.

> Important: The current version of the code interpreter toolchain **defaults to assuming the working directory is `/app` and the Artifacts directory is `/app/artifacts`** in multiple places. Unless you know what you're doing, it's recommended to keep `WorkDir=/app` and `ArtifactsDir=artifacts`.

### 4.1 `CodePod:IsWindowsContainer`

- **Type**: Boolean
- **Default**: `false` (use Linux containers)
- **Purpose**:
  - Indicates whether to use Windows containers (defaults to Linux containers).
  - Affects default Docker endpoint and container internal commands (e.g., keep-alive, mkdir, delete files, etc.).

### 4.2 `CodePod:DockerEndpoint`

- **Type**: String or `null`
- **Default**: `null` (automatically select default endpoint)
- **Purpose**: Specify the Docker service endpoint address.
- **Behavior**:
  - When not configured, it automatically selects the default endpoint based on `CodePod:IsWindowsContainer`:
    - Windows containers: `npipe://./pipe/docker_engine`
    - Linux/macOS containers: `unix:///var/run/docker.sock`

### 4.3 `CodePod:WorkDir`

- **Type**: String (container internal path)
- **Default**: `/app`
- **Purpose**: The working directory of the container (Docker `WorkingDir`).

### 4.4 `CodePod:ArtifactsDir`

- **Type**: String (subdirectory name relative to `WorkDir`)
- **Default**: `artifacts`
- **Purpose**: Directory for storing exported files (for download/upload return).

### 4.5 `CodePod:LabelPrefix`

- **Type**: String
- **Default**: `codepod`
- **Purpose**:
  - Generate container name prefix (e.g., `codepod-xxxxxxxx`).
  - Used as a prefix for Docker labels to identify "containers managed by Chats" for easy cleanup and filtering.

### 4.6 `CodePod:OutputOptions:MaxOutputBytes`

- **Type**: Integer (bytes)
- **Default**: `6144` (6KB)
- **Purpose**: Limit the maximum byte size of container command output (stdout/stderr); exceeding this triggers truncation (default strategy is to keep the beginning and end, inserting an "output truncated" message).

- **Environment variable example**: `CodePod__OutputOptions__MaxOutputBytes=6144`

---

## 5. CodeInterpreter (Code Interpreter / Sandbox Policy)

The `CodeInterpreter` configuration group controls: the default sandbox image to use, execution timeout per command, session idle reclamation, network isolation, resource limits, and the artifacts upload quota that can be returned per round.

### 5.1 `CodeInterpreter:DefaultImage`

- **Type**: String (Docker image name)
- **Default**: `sdcb/code-interpreter:r-26`
- **Purpose**: The default image to use when creating a new code interpreter session.

### 5.2 `CodeInterpreter:DefaultImageDescription`

- **Type**: String
- **Default**: `Pre-installed with common packages, suitable for most daily tasks`
- **Purpose**: Used to enrich the system prompt (telling the model what capabilities/tools are available in this image).

### 5.3 `CodeInterpreter:DefaultTimeoutSeconds`

- **Type**: Integer seconds or `null`
- **Default**: `300`
- **Purpose**: Default timeout for single command execution.
- **Behavior**:
  - `null` means "approximately unlimited" (in implementation, treated as 24 hours).
  - Eventually clamped to the range of `1..86400` seconds.

### 5.4 `CodeInterpreter:SessionIdleTimeoutSeconds`

- **Type**: Integer seconds
- **Default**: `1800` (30 minutes)
- **Purpose**: How long a session is idle before being considered expired (used to set the session's `ExpiresAt`, and reclaimed by the background cleanup service).

### 5.5 `CodeInterpreter:DefaultNetworkMode`

- **Type**: String
- **Default**: `bridge`
- **Purpose**: Default network mode for new sessions.
- **Possible values**: `none` | `bridge` | `host`

### 5.6 `CodeInterpreter:MaxAllowedNetworkMode`

- **Type**: String
- **Default**: `bridge`
- **Purpose**: Limits the "maximum network permissions the model can request" when calling tools.
- **Rule description**: Allowed modes are: all modes "not higher in level than this value" (`none < bridge < host`).
  - For example, with `MaxAllowedNetworkMode=bridge`, `none` and `bridge` are allowed, but `host` is forbidden.
  - The system validates at startup: `DefaultNetworkMode` cannot be higher than `MaxAllowedNetworkMode`.

### 5.7 `CodeInterpreter:DefaultResourceLimits`

- **Type**: Object
- **Default**:
  - `MemoryBytes`: `2147483648` (2GB)
  - `CpuCores`: `2.0`
  - `MaxProcesses`: `200`
- **Purpose**: When creating a session without explicitly specifying resource limits, use this default.
- **Field meanings**:
  - `MemoryBytes`: Memory limit (bytes)
  - `CpuCores`: Number of CPU cores (can be decimal)
  - `MaxProcesses`: Process limit (effective for Linux containers; Windows containers do not support this limit)

### 5.8 `CodeInterpreter:MaxResourceLimits`

- **Type**: Object
- **Default**: All `null`
- **Purpose**: Acts as a "hard limit" to prevent the model/tools from requesting resources beyond what you allow.
- **Behavior**:
  - `null` means no limit (converted to Docker's "unlimited/0" semantics).

### 5.9 `CodeInterpreter:MaxArtifactsFilesToUpload`

- **Type**: Integer
- **Default**: `50`
- **Purpose**: Maximum number of files allowed to be uploaded/returned from `/app/artifacts` per round.

### 5.10 `CodeInterpreter:MaxSingleUploadBytes`

- **Type**: Integer (bytes) or `null`
- **Default**: `157286400` (150MB)
- **Purpose**: Limit the maximum return size of a single artifacts file.
- **Behavior**: `null` means no limit.

### 5.11 `CodeInterpreter:MaxTotalUploadBytesPerTurn`

- **Type**: Integer (bytes) or `null`
- **Default**: `314572800` (300MB)
- **Purpose**: Limit the total size of all artifacts returned in a "single conversation turn".
- **Behavior**: `null` means no limit.

---

## 6. Login Sessions and JWT

### 6.1 `JwtValidPeriod`

- **Type**: TimeSpan string
- **Default**: `1.00:00:00` (1 day)
- **Purpose**: Controls the validity period of JWT after user login.
- **Behavior**:
  - If this item is not configured, the default validity period is 8 hours.
  - Common format: `d.hh:mm:ss`, for example, `0.08:00:00` means 8 hours.

### 6.2 `JwtSecretKey`

- **Type**: String or `null`
- **Default**: `null`
- **Purpose**: JWT signing key.
- **Behavior**:
  - If empty, the system generates a random value as the key when the process starts; this causes **all old JWTs to become invalid after service restart** (users need to log in again).
  - For production environments, it's recommended to configure a stable random string (and keep it secure).

---

## 7. Chat Request Retry

### 7.1 `Chat:Retry429Times`

- **Type**: Integer
- **Default**: `5`
- **Purpose**: Number of retries when the upstream model service returns HTTP 429 (rate limiting).
- **Behavior**:
  - Retry is only triggered when "this request has not yet produced any output fragments (streaming hasn't started yet)".
  - Uses exponential backoff: 1s, 2s, 4s, 8s... up to a maximum of 30s, with 0~250ms random jitter.
  - Configured as `0` or not configured (`null`) means no 429 retry.

- **Environment variable syntax**: `Chat__Retry429Times=5`
- **Command-line syntax**: `--Chat:Retry429Times=5`
