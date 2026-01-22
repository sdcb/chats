# Chats Development Guide

**English** | [简体中文](../zh-CN/build.md)

Welcome to Chats! This guide will help you quickly get started with development and understand how to use and configure the Chats project during the development phase. In the development phase, Chats adopts a front-end and back-end separation model, but in production, they will be combined into a single deployment package.

## 📑 Table of Contents

- [Technology Stack](#technology-stack)
- [Environment Requirements](#environment-requirements)
- [Obtaining the Code](#obtaining-the-code)
- [Joint Frontend and Backend Development](#joint-frontend-and-backend-development)
  - [Backend Development Guide](#backend-development-guide)
  - [Frontend Development Guide](#frontend-development-guide)
- [Backend Only Development](#backend-only-development)
- [FAQ](#faq)

## Technology Stack

- **Backend**: C# / ASP.NET Core 10
- **Frontend**: Next.js / React / TypeScript
- **Styling**: Tailwind CSS
- **Database**: SQLite (default) / SQL Server / PostgreSQL
- **Storage**: Local files / AWS S3 / Minio / Aliyun OSS / Azure Blob Storage

## Environment Requirements

- **Git**: For code version management
- **[.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)**: Required for backend development
- **[Node.js](https://nodejs.org/) >= 20**: Required for frontend development
- **Visual Studio Code**: Lightweight code editor
- **Visual Studio 2026** (optional but recommended): Full-featured IDE with better debugging experience

## Obtaining the Code

First, clone the Chats code repository:

```bash
git clone https://github.com/sdcb/chats.git
```

## Joint Frontend and Backend Development

### Backend Development Guide

#### 1. Open the Solution with Visual Studio

Locate the `Chats.sln` solution file in the root directory and open it. In Visual Studio, you'll see a website project named `Chats.BE`.

#### 2. Run the Project

- **Method 1 (Visual Studio)**: Press `F5` or click the "Start Debugging" button to run the project
- **Method 2 (Command Line)**: Execute `dotnet run` in the project directory

**Running Notes**:

- The default configuration will check if the SQLite database file `chats.db` exists, and if not, it will automatically create it in the `./AppData` directory and initialize the database
- The service will run on `http://localhost:5146`, providing API services
- In development mode (`ASPNETCORE_ENVIRONMENT=Development`), Swagger UI will be available at `http://localhost:5146/swagger` for convenient API testing

#### 3. Configuration File Explanation

The default configuration is located in `appsettings.json`, but **it is strongly recommended to manage sensitive information using `userSecrets.json`** to prevent accidental exposure of sensitive development configurations in the codebase.

**Default Configuration Structure:**

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

**Configuration Options Details:**

| Configuration | Description | Default Value |
|--------------|-------------|---------------|
| `Logging` | Log level configuration | Information |
| `AllowedHosts` | Allowed host names | `*` (accepts all) |
| `FE_URL` | Frontend URL for CORS configuration | `http://localhost:3001` |
| `DBType` | Database type | `sqlite` (supports `mssql`, `postgresql`) |
| `ConnectionStrings:ChatsDB` | Database ADO.NET connection string | `Data Source=./AppData/chats.db` |
| `ENCRYPTION_PASSWORD` | Key for encrypting auto-increment IDs | Recommended to set as random string |

> For more detailed configuration instructions, please refer to the [Configuration Guide](./configuration.md).

> **💡 Why use integer + encryption instead of GUID?**
> 
> Initially, the Chats project used GUIDs, but after careful consideration, it was switched to auto-increment integer IDs:
> - ✅ GUID fields are larger, taking up more storage space
> - ✅ GUIDs as clustered indexes can lead to index fragmentation, affecting performance
> - ✅ Integer IDs are more efficient, and encryption can avoid direct exposure of IDs


#### 4. Managing Sensitive Configuration (Recommended)

⚠️ **It's not recommended to directly modify sensitive configuration items in `appsettings.json`**. Use `userSecrets.json` instead.

**Using Visual Studio:**

1. Right-click the `Chats.BE` project
2. Select "Manage User Secrets"
3. Add configuration in the opened `secrets.json` file

**Using Command Line:**

```bash
# Initialize user secrets
dotnet user-secrets init --project src/BE

# Set configuration items
dotnet user-secrets set "ENCRYPTION_PASSWORD" "your-random-string" --project src/BE
dotnet user-secrets set "ConnectionStrings:ChatsDB" "your-connection-string" --project src/BE

# View all configurations
dotnet user-secrets list --project src/BE
```

This helps avoid accidentally uploading sensitive information when committing code to the repository.

#### 5. Command Line Running Method

If not using Visual Studio, you can run via command line:

```bash
# Navigate to backend directory
cd ./src/BE

# Run project
dotnet run

# Or specify listening address
dotnet run --urls "http://localhost:5146"
```

### Frontend Development Guide

#### 1. Navigate to Frontend Directory

```bash
cd ./src/FE
```

#### 2. Create Environment Configuration File

Create `.env.local` file and specify the backend API address:

**Linux/macOS:**

```bash
echo "API_URL=http://localhost:5146" > .env.local
```

**Windows PowerShell:**

```powershell
"API_URL=http://localhost:5146" | Out-File -FilePath .env.local -Encoding utf8
```

#### 3. Install Dependencies and Run

```bash
# Install dependencies
npm install

# Or use pnpm (recommended, faster)
# pnpm install

# Start development server
npm run dev
```

**Running Notes**:

- Frontend service will listen on `http://localhost:3000`
- Backend already configured with CORS support, no extra configuration needed
- Code changes will automatically hot reload without manual browser refresh

## Backend Only Development

For backend-focused development scenarios, you can use pre-built frontend static files without compiling the frontend locally.

### Quick Start

#### 1. Clone Repository and Navigate to Backend Directory

```bash
git clone https://github.com/sdcb/chats.git
cd chats/src/BE
```

#### 2. Download and Deploy Frontend Static Files

**Linux/macOS:**

```bash
# Download frontend files
curl -L -O https://github.com/sdcb/chats/releases/latest/download/chats-fe.zip

# Extract to wwwroot directory
unzip -o chats-fe.zip
cp -r chats-fe/* wwwroot/
rm -rf chats-fe chats-fe.zip
```

**Windows PowerShell:**

```powershell
# Download frontend files
Invoke-WebRequest -Uri "https://github.com/sdcb/chats/releases/latest/download/chats-fe.zip" -OutFile "chats-fe.zip"

# Extract to wwwroot directory
Expand-Archive -Path "chats-fe.zip" -DestinationPath "." -Force
Copy-Item -Path ".\chats-fe\*" -Destination ".\wwwroot" -Recurse -Force
Remove-Item -Path "chats-fe" -Recurse -Force
Remove-Item -Path "chats-fe.zip"
```

**Alternative Mirror (Recommended):**

If downloading from GitHub is slow, use the domestic mirror:

```bash
# Linux/macOS
curl -L -O https://chats.sdcb.pub/release/latest/chats-fe.zip

# Windows PowerShell
Invoke-WebRequest -Uri "https://chats.sdcb.pub/release/latest/chats-fe.zip" -OutFile "chats-fe.zip"
```

> **📌 Notes**:
> 
> 1. `chats-fe.zip` is automatically generated by GitHub Actions when code is merged into the `main` branch
> 2. Merges to the `dev` branch do not trigger updates
> 3. For the latest development version, please use frontend development mode

#### 3. Run Backend

**Using Command Line:**

```bash
dotnet run
```

**Using Visual Studio:**

1. Open `Chats.sln` solution
2. Select `Chats.BE` project
3. Press `F5` to start debugging

#### 4. Access Application

Once running successfully, visit `http://localhost:5146/login` to access the Chats login page, implementing a unified front-end and back-end deployment mode.

---

## FAQ

### 1. How to switch database type?

Modify `appsettings.json` or use user secrets settings:

```json
{
  "DBType": "mssql",  // or "postgresql", "sqlite"
  "ConnectionStrings": {
    "ChatsDB": "Server=localhost;Database=ChatsDB;User Id=sa;Password=YourPassword;"
  }
}
```

### 2. Frontend requests to backend API fail?

Check the following configurations:

- Ensure backend is started and listening on `http://localhost:5146`
- Check `API_URL` configuration in frontend `.env.local` file
- Check if backend `FE_URL` configuration is correct
- Check browser console for CORS errors

### 3. How to reset database?

Delete database file and run again:

```bash
# SQLite
rm ./AppData/chats.db

# Then run project again
dotnet run
```

### 4. Visual Studio cannot recognize .NET 10?

Ensure you have Visual Studio 2026 or higher installed, along with .NET 10 SDK.

---

## Related Resources

- **GitHub Repository**: [https://github.com/sdcb/chats](https://github.com/sdcb/chats)
- **Issue Reporting**: [Create Issue](https://github.com/sdcb/chats/issues)
- **QQ Group**: 498452653
- **Changelog**: [Release Notes](./release-notes/README.md)

---

We hope this guide helps you successfully develop the Chats project. If you have any questions, feel free to get support via GitHub Issues or QQ Group!