# FAQ

**English** | [简体中文](../zh-CN/faq.md)

## Deployment

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

## Usage

<details>
<summary><b>How to add a new model provider?</b></summary>

1. Log in to the admin interface
2. Go to the "Model Management" page
3. Click "Add Provider"
4. Fill in the provider name, API address, and API key
5. Save and start using

Any service compatible with OpenAI Chat Completion API can be added.
</details>

<details>
<summary><b>How to manage user permissions?</b></summary>

Administrators can do the following in the user management interface:
- Create new users
- Set user balance
- Assign available models
- Enable/disable user accounts
</details>

<details>
<summary><b>How to view usage statistics?</b></summary>

The admin interface provides complete usage statistics:
- Token consumption by user
- API calls by model
- View detailed conversation history
- Export statistical reports
</details>

## Troubleshooting

<details>
<summary><b>Cannot access the web interface after startup?</b></summary>

Please check:
1. Whether the port is occupied
2. Whether the firewall allows the port
3. Check console logs for error messages
</details>

<details>
<summary><b>Database connection failed?</b></summary>

Please check:
1. Whether the database service is running
2. Whether the connection string is correct
3. Whether the network is reachable (use `host.docker.internal` in Docker environment)
4. Whether the database user has sufficient permissions
</details>

<details>
<summary><b>API call returns error?</b></summary>

Common causes:
1. Invalid or expired API key
2. Incorrect model name
3. Request format does not meet provider requirements
4. Insufficient account balance

Please check detailed error messages for troubleshooting.
</details>

## More Help

If the above content does not solve your problem, please get help through the following ways:

- **GitHub Issues**: [https://github.com/sdcb/chats/issues](https://github.com/sdcb/chats/issues)
- **QQ Group**: 498452653 [![Join QQ Group](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=flat&logo=tencent-qq)](https://qm.qq.com/q/AM8tY9cAsS)
