# Sdcb Chats [![docker pulls](https://img.shields.io/docker/pulls/sdcb/chats)](https://hub.docker.com/r/sdcb/chats) [![QQ](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=social&logo=tencent-qq&logoColor=000&logoWidth=20)](https://qm.qq.com/q/AM8tY9cAsS) [![License](https://img.shields.io/github/license/sdcb/chats)](LICENSE)

[English](README_EN.md) | **简体中文** 

Sdcb Chats 是一个强大且灵活的大语言模型前端，支持 22+ 主流 AI 模型服务商。无论您是希望统一管理多种模型接口，还是需要一个简单易用的部署方案，Sdcb Chats 都能满足您的需求。

## ✨ 为什么选择 Sdcb Chats

- 🚀 **一站式**：22+ 模型服务商，一个入口
- 🎯 **分钟级上手**：一条命令 Docker 部署，8 平台原生可执行
- 🐳 **代码解释器**：Docker 沙箱，内置浏览器/代码执行/Excel 等工具
- 🔌 **API 网关**：Chat Completions/Messages 兼容，支持 Claude Code
- 🌐 **标准协议**：Chat Completions/Messages/Responses/Gemini，支持交错思考
- 🔍 **可观测性**：Request Trace 全链路追踪，快速定位入站/出站请求问题
- 👁️ **多模态**：视觉输入，图像生成
- 💾 **灵活存储**：SQLite/SQL Server/PostgreSQL + 本地/AWS S3/Aliyun OSS/Azure Blob
- 🔐 **企业级安全**：完善的用户权限管理和账户余额控制，限流审计日志，支持 Keycloak SSO 与短信验证码登录

<img alt="chats" src="https://github.com/user-attachments/assets/106ece3f-d94d-460e-9313-4a01f624a647" />

## 🆕 最新版本（1.11.0）

- 📅 发布日期：2026-04-07
- 🧠 智能标题摘要：新增首轮 AI 自动命名，支持管理员全局配置、个人覆盖、指定模型与 Prompt Template，并将标题生成单独记为 `Summary` 来源
- 💵 模型定价页：新增模型价格总览页，支持按提供商、API 类型、免费模型和关键字筛选，并展示上下文窗口与多种价格口径
- 📊 用量来源细分：`UserModelUsage` 新增 `SourceId`，支持 `WebChat` / `Api` / `Summary` 分类筛选和后台来源统计
- 🆕 新模型提供商：新增第 22 个模型提供商 Novita AI，支持思考信息回传
- ↔️ 体验与性能：桌面聊天侧边栏支持拖拽调宽并持久化，Markdown/KaTeX/图表延迟加载，聊天页初始负载约从 3.5MB/3.6MB 降到 1.7MB
- 🛠️ 升级重点：迁移脚本会删除 `TransactionType` 表、回填 `UserModelUsage.SourceId`，并新增 `UserConfig` 表承载用户级配置

👉 [查看 1.11.0 发布说明](./doc/zh-CN/release-notes/1.11.0.md) · [查看全部版本](./doc/zh-CN/release-notes/README.md)

## 快速开始

一条命令即可启动（需要 Docker）：

```bash
mkdir -p ./AppData && chmod 755 ./AppData && docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:/app/AppData -v /var/run/docker.sock:/var/run/docker.sock --user 0:0 -p 8080:8080 sdcb/chats:latest
```

启动后访问 `http://localhost:8080`，使用默认账号 `chats` / `RESET!!!` 登录。

📖 **[查看完整部署指南](./doc/zh-CN/quick-start.md)** - 包含 Docker 部署、可执行文件部署、数据库配置等详细说明。

---

## 📚 文档中心

Chats 使用 `C#`/`TypeScript` 开发，以下是完整的文档资源：

- [🚀 快速开始](./doc/zh-CN/quick-start.md) - 部署指南、Docker 配置、数据库设置
- [💾 下载地址](./doc/zh-CN/downloads.md) - Docker 镜像和可执行文件下载
- [🤖 支持的模型提供商](./doc/zh-CN/model-providers.md) - 22+ 模型服务商列表及支持情况
- [🛠️ 开发指南](./doc/zh-CN/build.md) - 如何编译和开发 Chats
- [⚙️ 配置说明](./doc/zh-CN/configuration.md) - 详细配置参数说明
- [📝 更新日志](./doc/zh-CN/release-notes/README.md) - 版本更新记录
- [🔍 问 DeepWiki](https://deepwiki.com/sdcb/chats) - AI 驱动的项目知识库
- [❓ 常见问题](./doc/zh-CN/faq.md) - 部署和使用中的常见问题解答

---

## 贡献指南

我们欢迎各种形式的贡献，包括但不限于：

- 🐛 报告 Bug
- 💡 提出新功能建议
- 📝 改进文档
- 🔧 提交代码

请通过 [GitHub Issues](https://github.com/sdcb/chats/issues) 提交问题或建议。

---

## 联系方式

- **GitHub Issues**：[https://github.com/sdcb/chats/issues](https://github.com/sdcb/chats/issues)
- **QQ 群**：498452653 [![加入QQ群](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=flat&logo=tencent-qq)](https://qm.qq.com/q/AM8tY9cAsS)
- **微信群** ![](https://io.starworks.cc:88/cv-public/2026/chats-wxg-qr.png?t=0423) 如果微信群已满，请加 QQ 群获取临时入群二维码。

---

## 许可证

本项目采用 [Apache 2.0](LICENSE) 开源许可证。

---

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=sdcb/chats&type=Date)](https://star-history.com/#sdcb/chats&Date)

---

**如果这个项目对你有帮助，欢迎给个 ⭐ Star！**
