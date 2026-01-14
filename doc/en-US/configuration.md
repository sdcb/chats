# Configuration Guide (appsettings.json)

This document explains each backend configuration item (based on `src/BE/web/appsettings.json`) and groups them by feature.

> Convention: This document intentionally does not cover ASP.NET Core common settings (e.g., `Logging`, `AllowedHosts`).

## 0. Configuration sources and precedence

Chats uses the standard .NET configuration system. from higher to lower precedence are:

1. **Command-line arguments** (e.g., `--DBType=sqlite`, `--ConnectionStrings:ChatsDB=...`)
2. **Environment variables** (e.g., `DBType`, `ConnectionStrings__ChatsDB`)
3. **appsettings.json** (default config file)

> Tip: For nested configuration keys, use double underscores `__` in environment variables, for example `ConnectionStrings__ChatsDB` and `CodeInterpreter__DefaultTimeoutSeconds`.

---

## 1. Frontend URL & CORS

### 1.1 `FE_URL`

- **Type**: string (URL)
- **Default**: `http://localhost:3001`
- **Purpose**:
  - Configures the backend CORS allowed origins (so the frontend can call backend APIs across origins).
  - The backend adds this value into the `FrontendCORS` policy via `WithOrigins(...)`.
  - Code behavior: the backend reads `FE_URL` at startup; if missing, it throws an exception (i.e., it must be configured).

- **Notes**:
  - Besides your configured `FE_URL`, the backend also allows `http://localhost:3000` (for local development convenience).

- **Environment variable**: `FE_URL=http://localhost:3001`
- **Command line**: `--FE_URL=http://localhost:3001`

---

## 2. Link/ID encryption

### 2.1 `ENCRYPTION_PASSWORD`

- **Type**: string
- **Default**: placeholder text in the sample (you should replace it)
- **Purpose**:
  - Used to encrypt/obfuscate auto-increment integer IDs into URL-friendly values (e.g., share links and some API route parameters).
  - If not configured (empty/whitespace), the backend logs a warning and falls back to **no encryption** (NoOp).

- **Risk & recommendation**:
  - In production, set it to a **long and random** string and keep it stable; changing it may make previously generated encrypted IDs/links unresolvable.

- **Environment variable**: `ENCRYPTION_PASSWORD=...`
- **Command line**: `--ENCRYPTION_PASSWORD=...`

---

## 3. Database

### 3.1 `DBType`

- **Type**: string
- **Default**: `sqlite`
- **Purpose**: selects the database provider.
- **Supported values**:
  - `sqlite` (default)
  - `mssql` / `sqlserver`
  - `postgresql` / `pgsql`
- **Behavior**:
  - If not set, it is treated as `sqlite`.
  - Unknown values will throw at startup and stop the application.

- **Environment variable**: `DBType=sqlite`
- **Command line**: `--DBType=sqlite`

### 3.2 `ConnectionStrings:ChatsDB`

- **Type**: string (ADO.NET connection string)
- **Default**: `Data Source=./AppData/chats.db`
- **Purpose**: specifies the database connection string.
- **Behavior**:
  - This connection string **must exist**; if missing, the backend throws at startup (`ConnectionStrings:ChatsDB not found`).
  - When `DBType=sqlite` and the connection string is the default `Data Source=./AppData/chats.db`, if the `AppData` folder does not exist in the current working directory, the backend will automatically create it for a smoother first-run experience.

- **Environment variable**: `ConnectionStrings__ChatsDB=...`
- **Command line**: `--ConnectionStrings:ChatsDB=...`

---

## 4. CodePod (Docker container base)

The `CodePod` section controls how the Docker containers (used by the code interpreter sandbox) are created and managed, including working directory and output truncation.

> Important: In the current version, the code interpreter toolchain assumes the working directory is `/app` and the artifacts directory is `/app/artifacts` in multiple places. Unless you know what you are doing, keep `WorkDir=/app` and `ArtifactsDir=artifacts`.

### 4.1 `CodePod:WorkDir`

- **Type**: string (path inside container)
- **Default**: `/app`
- **Purpose**: container working directory (Docker `WorkingDir`).

### 4.2 `CodePod:ArtifactsDir`

- **Type**: string (directory name relative to `WorkDir`)
- **Default**: `artifacts`
- **Purpose**: directory used for exported files (so users can download/upload artifacts back).

### 4.3 `CodePod:LabelPrefix`

- **Type**: string
- **Default**: `codepod`
- **Purpose**:
  - Prefix for generated container names (e.g., `codepod-xxxxxxxx`).
  - Prefix for Docker labels, marking containers managed by Chats for filtering/cleanup.

### 4.4 `CodePod:OutputOptions:MaxOutputBytes`

- **Type**: integer (bytes)
- **Default**: `6144` (6KB)
- **Purpose**: maximum output bytes for container commands (stdout/stderr). When exceeded, output is truncated (default strategy keeps head and tail and inserts a truncation message).

- **Environment variable example**: `CodePod__OutputOptions__MaxOutputBytes=6144`

---

## 5. CodeInterpreter (sandbox policy)

The `CodeInterpreter` section controls: default sandbox image, command timeout, session idle cleanup, network isolation, resource limits, and per-turn artifacts upload quotas.

### 5.1 `CodeInterpreter:DefaultImage`

- **Type**: string (Docker image)
- **Default (repo example)**: `sdcb/code-interpreter:r-26`
- **Purpose**: default image used for new code interpreter sessions.

### 5.2 `CodeInterpreter:DefaultImageDescription`

- **Type**: string
- **Default (repo example)**: `Pre-installed with common packages...`
- **Purpose**: enriches the system prompt (tells the model what is available inside the image).

### 5.3 `CodeInterpreter:DefaultTimeoutSeconds`

- **Type**: integer seconds or `null`
- **Default**: `300`
- **Purpose**: default timeout for a single command.
- **Behavior**:
  - `null` means “effectively unlimited” (implemented as 24 hours).
  - The effective value is clamped to `1..86400` seconds.

### 5.4 `CodeInterpreter:SessionIdleTimeoutSeconds`

- **Type**: integer seconds
- **Default**: `1800` (30 minutes)
- **Purpose**: session idle timeout (used to set `ExpiresAt`, and a background cleanup service reclaims idle containers).

### 5.5 `CodeInterpreter:DefaultNetworkMode`

- **Type**: string
- **Default (repo example)**: `bridge`
- **Purpose**: default network mode for new sessions.
- **Allowed values**: `none` | `bridge` | `host`

### 5.6 `CodeInterpreter:MaxAllowedNetworkMode`

- **Type**: string
- **Default (repo example)**: `bridge`
- **Purpose**: limits the maximum network permission the model can request when calling tools.
- **Rule**: all modes with “level” not higher than this value are allowed (`none < bridge < host`).
  - For example, if `MaxAllowedNetworkMode=bridge`, then `none` and `bridge` are allowed, while `host` is forbidden.
  - The system validates at startup that `DefaultNetworkMode` does not exceed `MaxAllowedNetworkMode`.

### 5.7 `CodeInterpreter:DefaultResourceLimits`

- **Type**: object
- **Default (repo example)**:
  - `MemoryBytes`: `2147483648` (2GB)
  - `CpuCores`: `2.0`
  - `MaxProcesses`: `200`
- **Purpose**: default resource limits used when a session is created without explicit limits.
- **Fields**:
  - `MemoryBytes`: memory limit (bytes)
  - `CpuCores`: CPU cores (can be fractional)
  - `MaxProcesses`: process limit (effective on Linux containers; Windows containers do not support this limit)

### 5.8 `CodeInterpreter:MaxResourceLimits`

- **Type**: object
- **Default (repo example)**: all `null`
- **Purpose**: hard upper bounds to prevent the model/tools from requesting more than you allow.
- **Behavior**:
  - `null` means unlimited (converted to Docker-side “unlimited/0” semantics).

### 5.9 `CodeInterpreter:MaxArtifactsFilesToUpload`

- **Type**: integer
- **Default**: `50`
- **Purpose**: maximum number of files allowed to upload/return from `/app/artifacts` per turn.

### 5.10 `CodeInterpreter:MaxSingleUploadBytes`

- **Type**: integer (bytes) or `null`
- **Default (repo example)**: `157286400` (150MB)
- **Purpose**: maximum size for a single artifacts file to upload/return.
- **Behavior**: `null` means unlimited.

### 5.11 `CodeInterpreter:MaxTotalUploadBytesPerTurn`

- **Type**: integer (bytes) or `null`
- **Default (repo example)**: `314572800` (300MB)
- **Purpose**: maximum total artifacts upload/return bytes in a single turn.
- **Behavior**: `null` means unlimited.

---

## 6. Login sessions & JWT

### 6.1 `JwtValidPeriod`

- **Type**: TimeSpan string
- **Default (repo example)**: `1.00:00:00` (1 day)
- **Purpose**: JWT validity duration after login.
- **Behavior**:
  - If not configured, the default validity is 8 hours.
  - Common format: `d.hh:mm:ss`, for example `0.08:00:00` means 8 hours.

### 6.2 `JwtSecretKey`

- **Type**: string or `null`
- **Default**: `null`
- **Purpose**: JWT signing key.
- **Behavior**:
  - If empty, the system generates a random key at process startup; this makes **all existing JWTs invalid after a restart** (users must log in again).
  - For production, configure a stable random string (and keep it secret).

---

## 7. Chat request retry

### 7.1 `Chat:Retry429Times`

- **Type**: integer
- **Default (repo example)**: `5`
- **Purpose**: retry count when the upstream model service returns HTTP 429 (rate limit).
- **Behavior**:
  - Retries only happen when **no output has been yielded yet** (i.e., before streaming starts).
  - Uses exponential backoff: 1s, 2s, 4s, 8s... (capped at 30s) plus a small random jitter (0–250ms).
  - Set to `0` or leave unset (`null`) to disable 429 retries.

- **Environment variable**: `Chat__Retry429Times=5`
- **Command line**: `--Chat:Retry429Times=5`
