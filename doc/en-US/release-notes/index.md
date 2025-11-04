<!-- Language: en-US -->
<p align="right"><b>English</b> | <a href="../../zh-CN/release-notes/index.md">简体中文</a></p>

# Chats Release History

This page indexes all major version release notes for the Chats project, from the latest to earlier versions. Each version includes core features, improvements, and fixes.

---

## [1.8.0](1.8.0.md) - 2025-11-04 ⭐ Latest Release 🎉 Major Architecture Upgrade

**Core Highlights**: Model Configuration Architecture Refactor · User-Customizable Config · No Migration for New Models

- 🏗️ **Architecture Refactor**: Model config migrated from ModelReference static table to Model instance level
- 🎯 **User Customization**: Each model instance has independent complete configuration (18 new fields)
- 🚀 **Ready to Use**: Adding new models requires no database migration, users can freely configure
- 🗑️ **Simplified Architecture**: Deleted 6 static reference tables (ModelReference, ModelProvider, etc.)
- 🖼️ **Third API Type**: Added ImageGeneration type (ChatCompletion, Response, ImageGeneration)
- 🔍 **Model Validation Framework**: Custom validation attributes + 436 lines of unit tests
- ⚡ **Quick Add Models**: New batch add dialog, supports continuous additions
- 📸 **Full-screen Image Preview**: Zoom animation + keyboard navigation + thumbnail strip
- 🎨 **UI Optimizations**: Admin on-demand lazy loading, skeleton screens, separated preset configs
- 🛠️ **Image Processing Refactor**: ImageSharp replaces hand-written parsers (-1,217 lines)

[View Full Release Notes →](1.8.0.md) | [API Changes](../1.8.0-api-changes.md)

---

## [1.7.2](1.7.2.md) - 2025-10-27

**Core Highlights**: Streaming Image Generation · Code Execution · API Test Framework

- 🖼️ **Streaming Image Generation**: Real-time progress for `gpt-image-1` and `gpt-image-1-mini`
- 🔧 **Code Execution**: Gemini code execution results displayed as tool calls
- 🧪 **API Test Framework**: New `Chats.BE.ApiTest` project for comprehensive OpenAI compatible API testing
- 🤖 **New Models**: Added `gpt-5-codex`, `gpt-5-pro`, `gpt-image-1-mini`
- ⚙️ **Config Enhancement**: New `CodeExecutionEnabled` field in ChatConfig
- 📦 **Dependency Updates**: Upgraded 11 third-party packages

[View Full Release Notes →](1.7.2.md)

---

## [1.7.1](1.7.1.md) - 2025-10-13

**Core Highlights**: Security Audit Logs · Send Experience · Tool Call Display

- 🔒 **Security Audit**: Complete login attempt records and rate limit monitoring
- 📤 **Send Experience**: Multiple send modes (send, continue, regenerate) with mobile adaptation
- 🔨 **Tool Calls**: Dedicated `ToolCallBlock` component for clearer parameter and result display
- ⚡ **Performance**: On-demand loading of generation info, reduced initial load time
- 📱 **Mobile**: Better responsive layout and touch experience
- 👨‍💼 **Admin Panel**: Message content queries, optimized user usage statistics

[View Full Release Notes →](1.7.1.md)

---

## [1.7.0](1.7.md) - 2025-09-20 🎉 Major Update

**Core Highlights**: Full MCP Support · Database Refactor · Drag-and-Drop Ordering

- 🔌 **MCP Support**: End-to-end server/frontend integration, user authorization, tool discovery
- 🛠️ **Enhanced Tool Calls**: Richer SSE events, new tool request/response message content types
- 🗄️ **Database Refactor**: Message→ChatTurn/Step layering, data migration (breaking change)
- 🎨 **Drag-and-Drop**: Support for models/keys/presets ordering
- 📊 **Mermaid Support**: New Markdown Mermaid renderer with dark/light themes
- 🖼️ **Image Sizes**: Common size options (1024×1024, 1536×1024, 1024×1536)
- 🔄 **Regeneration**: Support for single message or whole segment regeneration

[View Full Release Notes →](1.7.md)

---

## [1.6](1.6.md) - 2024-06-30

**Core Highlights**: .NET 9 Upgrade · Enhanced Reasoning Models · Image Improvements

- ⬆️ **.NET 9**: Full upgrade to .NET 9 framework for improved performance and security
- 🧠 **Reasoning Models**: Support for o3-pro, o4-mini, Gemini Think
- 📸 **Image Features**: History upload, phone camera capture, forced 3:2/2:3 ratios
- 🤖 **New Models**: GPT-5 series, Qwen3 series, GLM-4.5 series, Kimi, Grok-4, Codex-mini
- ☁️ **Azure Deployment**: New one-click deployment script
- 💾 **Storage**: Migrated from MinIO to Cloudflare R2
- ⌨️ **UX**: Fullscreen hotkey (Ctrl+F), UI optimizations

[View Full Release Notes →](1.6.md)

---

## [1.5](1.5.md) - 2024-05-20

**Core Highlights**: Qwen3 Support · Stability Improvements

- 🤖 **Qwen3 Support**: Added Qwen3 models with optional reasoning chain disable
- 🗂️ **File Optimization**: Database indexes for better performance, improved download logic
- 🎨 **UI Enhancement**: Optimized dark theme icons, improved code/raw content toggle
- 🐛 **Bug Fixes**: Image generation, file sharing, encryption/decryption fixes
- 📦 **Dependency Updates**: Upgraded to latest package versions

[View Full Release Notes →](1.5.md)

---

## [1.4.0](1.4.md) - 2024-05-20

**Core Highlights**: Full Function Call Support · API Caching

- 🔧 **Function Calls**: All API endpoints support function calling
- ⚡ **API Caching**: New `/v1-cached` and `/v1-cached-createOnly` cache endpoints
- 💻 **Code Execution**: Google Gemini models support code execution
- 🚀 **Performance**: Async processing, reduced duplicate database calls

[View Full Release Notes →](1.4.md)

---

## [1.3](1.3.md) - 2024-04-25

**Core Highlights**: Enhanced Image Generation · Baidu ERNIE Integration

- 🖼️ **gpt-image-1**: Full support for Azure OpenAI gpt-image-1 model
- ✏️ **Image Editing**: Image editing, mask redrawing, quality control
- 🤖 **Baidu ERNIE**: Integration of new Baidu ERNIE models
- 📁 **File Management**: Improved file management features
- 🐛 **Bug Fixes**: 1.3.1 fixed critical password change issue

[View Full Release Notes →](1.3.md)

---

## [1.2.0](1.2.md) - 2025-04-25

**Core Highlights**: Admin Dashboard · Data Visualization

- 📊 **Dashboard**: Data visualization panel for chat volume, cost, and token statistics
- 📱 **Mobile Adaptation**: Full mobile device support for admin panel
- 💬 **Message UI**: Improved message display styles, new message type toggle
- ⌨️ **Interaction**: Message edit with Ctrl+Enter, search debounce optimization

[View Full Release Notes →](1.2.md)

---

## [1.1.0](1.1.md) - 2025-04-24

**Core Highlights**: Reasoning Model Support · SDK Upgrade

- 🧠 **Reasoning Models**: Azure OpenAI Response API with o3/o4-mini reasoning summary
- 📦 **SDK Upgrade**: OpenAI and Azure OpenAI SDK upgraded to 2.2.0-beta.4
- 📝 **Reasoning Optimization**: Improved reasoning process format for readability
- 🌏 **Mirror Upload**: Sync upload to Minio for faster China access

[View Full Release Notes →](1.1.md)

---

## [1.0](1.0.md) - 2025-04-21 🎊 Official Release

**Core Highlights**: Official Release · Comprehensive Features

- 🎉 **Official Release**: From 0.x preview to production-ready
- ⚙️ **User Settings**: New user settings page for centralized personal configuration
- 📈 **Usage Reports**: Detailed request and consumption tracking with Excel export
- 🤖 **Model Expansion**: GPT-4.1, o3/o4-mini, Doubao 1.5, Gemini 2.5 Flash
- ⏱️ **Timeout Optimization**: Thinking timeout extended from 100s to 24 hours
- 🔄 **Background Generation**: Model continues generating after window close
- 👨‍💼 **Admin Features**: Consumption summary with multi-condition filtering

> **Version Notes**:
> - 1.0.0 (756): Official release
> - 1.0.1 (759): Fixed reasoning level issues
> - 1.0.2 (762): Fixed Google Gemini reasoning level issues

[View Full Release Notes →](1.0.md)

---

## Version Naming Convention

Starting from 1.0.0, Chats follows Semantic Versioning:

- **Major**: Significant architectural changes or breaking updates (e.g., 1.0.0 → 2.0.0)
- **Minor**: New features with backward compatibility (e.g., 1.0.0 → 1.1.0)
- **Patch**: Bug fixes and minor optimizations (e.g., 1.0.0 → 1.0.1)

---

## Get Help

- 📖 [Build Documentation](../build.md)
- ☁️ [Azure Deployment Documentation](../azure-bicep.md)
- 🐛 [Report Issues](https://github.com/sdcb/chats/issues)
- 💬 [Join Discussions](https://github.com/sdcb/chats/discussions)

---

<p align="center">
  <sub>Last updated: 2025-11-04</sub>
</p>
