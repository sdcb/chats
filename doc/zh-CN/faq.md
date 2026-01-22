# 常见问题

[English](../en-US/faq.md) | **简体中文**

## 部署相关

<details>
<summary><b>如何修改默认端口？</b></summary>

在启动时使用 `--urls` 参数指定端口：

```bash
# Docker
docker run -e ASPNETCORE_URLS="http://+:5000" -p 5000:5000 sdcb/chats:latest

# 可执行文件
./Chats.BE.exe --urls http://+:5000
```
</details>

<details>
<summary><b>如何切换到 SQL Server 或 PostgreSQL？</b></summary>

使用 `--DBType` 参数和 `--ConnectionStrings:ChatsDB` 参数：

```bash
# SQL Server
./Chats.BE.exe --DBType=mssql --ConnectionStrings:ChatsDB="Server=localhost;Database=ChatsDB;User Id=sa;Password=YourPassword"

# PostgreSQL
./Chats.BE.exe --DBType=pgsql --ConnectionStrings:ChatsDB="Host=localhost;Database=chatsdb;Username=postgres;Password=YourPassword"
```
</details>

<details>
<summary><b>如何配置文件存储服务？</b></summary>

Chats 支持多种文件存储服务，可在管理界面的系统设置中配置，支持：
- 本地文件系统
- AWS S3
- Minio
- Aliyun OSS
- Azure Blob Storage

配置后无需重启即可生效。
</details>

<details>
<summary><b>忘记管理员密码怎么办？</b></summary>

可以通过数据库直接重置密码,或删除数据库文件重新初始化（注意备份数据）。
</details>

<details>
<summary><b>如何在纯 Windows 环境使用 Docker 部署？</b></summary>

纯 Windows 环境下使用 SQLite 数据库：

```powershell
mkdir AppData
icacls .\AppData /grant "Users:(OI)(CI)(M)" /T
docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:C:/app/AppData -p 8080:8080 sdcb/chats:latest
```

纯 Windows 环境下使用 PostgreSQL 数据库：

```powershell
mkdir AppData
icacls .\AppData /grant "Users:(OI)(CI)(M)" /T
docker run --restart unless-stopped --name sdcb-chats -e DBType=postgresql -e ConnectionStrings__ChatsDB="Host=host.docker.internal;Port=5432;Username=postgres;Password=YourPassword;Database=postgres" -v ./AppData:C:/app/AppData -p 8080:8080 sdcb/chats:latest
```

**注意**：从容器访问宿主机服务时，使用 `host.docker.internal` 而不是 `localhost`。
</details>

## 使用相关

<details>
<summary><b>如何添加新的模型提供商？</b></summary>

1. 登录管理界面
2. 进入"模型管理"页面
3. 点击"添加提供商"
4. 填写提供商名称、API 地址和 API 密钥
5. 保存后即可使用

任何兼容 OpenAI Chat Completion API 的服务都可以添加。
</details>

<details>
<summary><b>如何管理用户权限？</b></summary>

管理员可以在用户管理界面：
- 创建新用户
- 设置用户余额
- 分配可使用的模型
- 启用/禁用用户账户
</details>

<details>
<summary><b>如何查看使用统计？</b></summary>

管理界面提供完整的使用统计功能：
- 按用户统计 Token 消耗
- 按模型统计调用次数
- 查看详细的对话历史
- 导出统计报表
</details>

## 故障排除

<details>
<summary><b>启动后无法访问 Web 界面？</b></summary>

请检查：
1. 端口是否被占用
2. 防火墙是否允许该端口
3. 查看控制台日志是否有错误信息
</details>

<details>
<summary><b>数据库连接失败？</b></summary>

请检查：
1. 数据库服务是否正在运行
2. 连接字符串是否正确
3. 网络是否可达（Docker 环境中使用 `host.docker.internal`）
4. 数据库用户权限是否足够
</details>

<details>
<summary><b>API 调用返回错误？</b></summary>

常见原因：
1. API 密钥无效或过期
2. 模型名称不正确
3. 请求格式不符合提供商要求
4. 账户余额不足

请查看详细错误信息进行排查。
</details>

## 更多帮助

如果以上内容未能解决您的问题，请通过以下方式获取帮助：

- **GitHub Issues**：[https://github.com/sdcb/chats/issues](https://github.com/sdcb/chats/issues)
- **QQ 群**：498452653 [![加入QQ群](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=flat&logo=tencent-qq)](https://qm.qq.com/q/AM8tY9cAsS)
