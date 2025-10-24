# 快速开始

## 1. 配置 API 密钥

使用用户机密设置你的 API 密钥（推荐）：

```bash
cd src/Chats.BE.ApiTest
dotnet user-secrets set "ApiKey" "your-api-key-here"
```

或者直接编辑 `appsettings.json`（不推荐用于生产环境）。

## 2. 确保后端服务运行

确保 Chats 后端服务正在运行，默认地址为 `http://localhost:5146`。

## 3. 运行测试

```bash
# 运行所有测试
dotnet test

# 运行并显示详细输出
dotnet test --logger "console;verbosity=detailed"

# 运行特定测试类
dotnet test --filter "FullyQualifiedName~ChatCompletionTests"
```

## 4. 查看测试结果

测试结果会显示在控制台，包括：
- 测试通过/失败状态
- API 响应内容
- Token 使用情况
- 错误详情（如果有）

## 示例输出

```
测试运行已成功。
测试总数: 10
     已通过: 10
总时间: 45.3 秒
```

详细信息请参阅 [README.md](README.md)。
