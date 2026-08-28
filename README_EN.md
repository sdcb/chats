# Sdcb Chats [![docker pulls](https://img.shields.io/docker/pulls/sdcb/chats)](https://hub.docker.com/r/sdcb/chats) [![QQ](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=social&logo=tencent-qq&logoColor=000&logoWidth=20)](https://qm.qq.com/q/AM8tY9cAsS) [![License](https://img.shields.io/github/license/sdcb/chats)](LICENSE)

**English** | [简体中文](README.md)

Sdcb Chats is a powerful and flexible frontend for large language models, supporting 22+ mainstream AI model providers. Whether you want to unify the management of multiple model interfaces or need a simple and easy-to-use deployment solution, Sdcb Chats can meet your needs.

## ✨ Why Choose Sdcb Chats

- 🚀 **All-in-One**: One hub for 22+ AI model providers
- 🎯 **Ready in Minutes**: One-command Docker deploy, plus native executables for 8 platforms
- 🐳 **Code Interpreter**: Docker sandbox with built-in tools (browser, code execution, Excel, and more)
- 🔌 **API Gateway**: Chat Completions/Messages compatible, works with Claude Code
- 🌐 **Standard APIs**: Chat Completions/Messages/Responses/Gemini, with interleaved thinking
- 🔍 **Observability**: Request Trace provides end-to-end inbound and outbound HTTP tracing for faster troubleshooting
- 👁️ **Multimodal**: Vision in, images out
- 💾 **Storage Freedom**: SQLite/SQL Server/PostgreSQL, plus Local/S3/OSS/Azure Blob
- 🔐 **Enterprise Security**: Permissions & balance control, rate limiting & audit logs, Keycloak SSO & SMS login

<img alt="chats-en" src="https://github.com/user-attachments/assets/40d2376e-58a0-4309-a2f5-5ed8262a0c2e" />

## 🆕 Latest Release (1.17.0)

- 📅 Release Date: 2026-08-27
- ⚡ Response speed: token speed excludes first-token (TTFT) latency and measures generation only
- 🖼️ Image-generation backgrounds: supports provider default, `auto`, `opaque`, and `transparent`
- 🧠 Context prompts: sends original user text to image-generation models and preserves required runtime context
- 🌳 Branching stability: fixes live SSE branch metadata and invalid subtree requests
- 👥 User management: separates user-edit and password-change forms and improves password-manager compatibility
- 🐛 Stability fixes: corrects duplicated `/v1` image endpoints, validates missing models, and adds regression coverage
- ⬆️ Upgrade focus: SQL Server and SQLite deployments must run the corresponding `1.17.0` database migration

👉 [View 1.17.0 Release Notes](./doc/en-US/release-notes/1.x/1.17.0.md) · [View All Releases](./doc/en-US/release-notes/README.md)

## Quick Start

Start with a single command (requires Docker):

```bash
mkdir -p ./AppData && chmod 755 ./AppData && docker run --restart unless-stopped --name sdcb-chats -e DBType=sqlite -e ConnectionStrings__ChatsDB="Data Source=./AppData/chats.db" -v ./AppData:/app/AppData -v /var/run/docker.sock:/var/run/docker.sock --user 0:0 -p 8080:8080 sdcb/chats:latest
```

After startup, visit `http://localhost:8080` and log in with the default account `chats` / `RESET!!!`.

📖 **[View Full Deployment Guide](./doc/en-US/quick-start.md)** - Including Docker deployment, executable deployment, database configuration, and more.

---

## 📚 Documentation

Chats is developed using `C#`/`TypeScript`. Here are the complete documentation resources:

- [🚀 Quick Start](./doc/en-US/quick-start.md) - Deployment guide, Docker configuration, database setup
- [💾 Downloads](./doc/en-US/downloads.md) - Docker images and executable file downloads
- [🤖 Supported Model Providers](./doc/en-US/model-providers.md) - 22+ model providers list and support status
- [🛠️ Development Guide](./doc/en-US/build.md) - How to compile and develop Chats
- [⚙️ Configuration Guide](./doc/en-US/configuration.md) - Detailed configuration parameters
- [📝 Release Notes](./doc/en-US/release-notes/README.md) - Version update history
- [🔍 Ask DeepWiki](https://deepwiki.com/sdcb/chats) - AI-powered project knowledge base
- [❓ FAQ](./doc/en-US/faq.md) - Common questions about deployment and usage

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
- **WeChat Group** ![](https://io.starworks.cc:88/cv-public/2026/chats-wxg-qr.png?t=0827) If the WeChat group is full, please join the QQ group to get a temporary invitation QR code.

---

## Special Thanks

<div align="left">
  <h1>RoutinAI</h1>
  <img width="154" height="151" src="https://routin.ai/favicon.png"/>
</div>

[RoutinAI](https://routin.ai/) is an enterprise-grade unified LLM API gateway that provides a single, type-safe interface to access over 100 leading large language models from the GPT, Claude, and Gemini families, including models such as gpt-5.6-sol, claude-opus-5 and gemini-3.1-pro-preview. It eliminates the complexity of managing multiple AI vendors by providing zero-latency edge routing, seamless model switching without code modifications, unified billing, and centralized governance with spending caps and access policies.

---

## License

This project is licensed under the [Apache 2.0](LICENSE).

---

**If this project helps you, please give it a ⭐ Star!**
