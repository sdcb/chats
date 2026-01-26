# Sdcb Chats [![docker pulls](https://img.shields.io/docker/pulls/sdcb/chats)](https://hub.docker.com/r/sdcb/chats) [![QQ](https://img.shields.io/badge/QQ_Group-498452653-52B6EF?style=social&logo=tencent-qq&logoColor=000&logoWidth=20)](https://qm.qq.com/q/AM8tY9cAsS) [![License](https://img.shields.io/github/license/sdcb/chats)](LICENSE)

**English** | [简体中文](README.md)

Sdcb Chats is a powerful and flexible frontend for large language models, supporting 21+ mainstream AI model providers. Whether you want to unify the management of multiple model interfaces or need a simple and easy-to-use deployment solution, Sdcb Chats can meet your needs.

## ✨ Why Choose Sdcb Chats

- 🚀 **All-in-One**: One hub for 21+ AI model providers
- 🎯 **Ready in Minutes**: One-command Docker deploy, plus native executables for 8 platforms
- 🐳 **Code Interpreter**: Docker sandbox with built-in tools (browser, code execution, Excel, and more)
- 🔌 **API Gateway**: Chat Completions/Messages compatible, works with Claude Code
- 🌐 **Standard APIs**: Chat Completions/Messages/Responses/Gemini, with interleaved thinking
- 👁️ **Multimodal**: Vision in, images out
- 💾 **Storage Freedom**: SQLite/SQL Server/PostgreSQL, plus Local/S3/OSS/Azure Blob
- 🔐 **Enterprise Security**: Permissions & balance control, rate limiting & audit logs, Keycloak SSO & SMS login

<img alt="chats-en" src="https://github.com/user-attachments/assets/40d2376e-58a0-4309-a2f5-5ed8262a0c2e" />

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
- [🤖 Supported Model Providers](./doc/en-US/model-providers.md) - 21+ model providers list and support status
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
- **WeChat Group** ![](https://io.starworks.cc:88/cv-public/2026/chats-wxg-qr.png) If the WeChat group is full, please join the QQ group to get a temporary invitation QR code.

---

## License

This project is licensed under the [Apache 2.0](LICENSE).

---

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=sdcb/chats&type=Date)](https://star-history.com/#sdcb/chats&Date)

---

**If this project helps you, please give it a ⭐ Star!**
