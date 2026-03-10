<!-- Language: en-US -->
<p align="right"><b>English</b> | <a href="../../zh-CN/release-notes/README.md">简体中文</a></p>

# Chats Release History

This page indexes all major version release notes for the Chats project, from the latest to earlier versions. Each version includes core features, improvements, and fixes.

---

## [1.10.2](1.10.2.md) - 2026-03-10 ⭐ Latest Release

**Core Highlights**: Request Trace end-to-end tracing · Admin request trace workspace · Sampling/filtering/redaction/retention policies · Export, compare, and scheduled cleanup · Small UX and compatibility improvements

- 🔍 **Request Trace**: adds inbound and outbound HTTP request tracing with timing points, status codes, errors, body capture, and raw payload downloads
- 🧑‍💼 **Admin Workspace**: adds a dedicated request trace page with filters, 24-column customization, details, raw downloads, two-trace compare, Excel export, and bulk delete by filters
- ⚙️ **Trace Governance**: supports separate inbound/outbound settings, sample rate, include/exclude filters, URL parameter redaction, and retention policies
- ♻️ **Lifecycle Management**: adds bounded queue protection and scheduled cleanup to reduce long-running trace storage pressure
- 🛠️ **Other Improvements**: updates dashboard summary card metrics, adds long user message collapse, and improves Anthropic protocol compatibility

[View Full Release Notes →](1.10.2.md)

---

## [1.10.1](1.10.1.md) - 2026-02-15

**Core Highlights**: Sandbox Manager Enhancements · Server-side ETag Caching · ChatMiniMap Navigation · API Key Management Upgrades · Code Interpreter Image Pipeline Updates

- 🏷️ **Sandbox Manager**: Session Manager renamed and expanded with info/env/files/editor capabilities
- ⚡ **ETag Caching**: High-frequency APIs now support server-driven ETag + 304 responses, frontend chat list local cache removed
- 🗺️ **ChatMiniMap**: Adds right-side message mini-map and migrates scroll controls from ChatInput
- 🔐 **API Keys**: One-time full-key display on create, masked list responses, edit + bulk copy support
- 🐳 **Image Pipeline**: `RUN_NUMBER` rv marker in skills, `ripgrep` preinstalled, and non-main branches can publish `latest` manifest

[View Full Release Notes →](1.10.1.md)

---

## [1.10.0](1.10.0.md) - 2026-01-14

**Core Highlights**: Built-in Docker Code Interpreter · `sdcb/code-interpreter` Image & CI · Multi-provider Interleaved Thinking · DeepSeek v3.2 `reasoning_content` Compatibility · Tooling/Upload/Admin UI Improvements

- 🐳 **Built-in Code Interpreter**: New Docker-based toolset with session management (create/run/read/write/patch/download/destroy)
- 📦 **Default Image**: `sdcb/code-interpreter` as the out-of-box environment (multi-arch), preloaded with toolchains, document/media utilities, browser stack, and `anthropics/skills`
- 🧠 **Interleaved Thinking**: Moonshot/DeepSeek/MiMo support reasoning (`reasoning_content`) round-tripping across tool calls
- 🔁 **DeepSeek v3.2 Compatibility**: Chat Completions API parses/persists assistant `reasoning_content` for stable think–tool–think flows
- 🧰 **Frontend UX**: ToolCallBlock progress/long output display, drag/paste upload, file preview & share UI, generation info & dashboard improvements

[View Full Release Notes →](1.10.0.md)

---

## [1.9.1](1.9.1.md) - 2025-12-21

**Core Highlights**: Xiaomi MiMo Provider · Prompt Cache Token Billing · Next.js 16 & React 19 Upgrade · Interleaved Thinking · 4-Level Chat View Architecture

- 🤖 **Xiaomi MiMo Support**: New Xiaomi MiMo provider (ID=21), supports MiMo-V2-Flash with OpenAI/Anthropic API formats
- 💰 **Prompt Cache Billing**: Cache token pricing, distinguishes Fresh/Cached tokens, displays usage in generation info
- 🔄 **Framework Upgrades**: Next.js 15.5.3 → 16.0.7, React 18.2.0 → 19.2.1, upgraded all @radix-ui packages
- 🧠 **Interleaved Thinking**: Minimax and DeepSeek support for Chat Completions API format interleaved thinking
- 🏗️ **4-Level Architecture**: chat/turn/step/content structure, generation info bubbles, improved message display
- 📋 **LaTeX Copy**: One-click copy of original LaTeX formula code with custom rehype plugin
- 🗑️ **SDK Cleanup**: Removed OpenAI .NET SDK and Mscc.GenerativeAI, using native HttpClient
- 🐛 **Bug Fixes**: DeepSeek-R1 signature parsing, token counting, image generation, UI flickering

[View Full Release Notes →](1.9.1.md)

---

## [1.9.0](1.9.0.md) - 2025-11-27

**Core Highlights**: Anthropic Provider Support · Anthropic Messages API · OpenAI Image API · Build Developer Pages · .NET 10 Upgrade

- 🤖 **Anthropic Support**: Full support for Claude model series (ID=20), including thinking+signature flow
- 📡 **Anthropic Messages API**: Compatible with Anthropic API spec (/v1/messages)
- 🖼️ **OpenAI Image API**: Image generation (/v1/images/generations) and edit (/v1/images/edits)
- 🛠️ **Build Pages**: Developer-facing API Keys / Docs / Usage management pages
- 🎬 **UI Animations**: ChatInput expand/fullscreen, UserMenuPopover, ToolCallBlock animations
- ⬆️ **.NET 10 Upgrade**: Framework upgraded to .NET 10
- 🏗️ **Architecture Refactor**: ChatService changed to DB Steps driven, supporting multiple message formats
- 🐛 **Bug Fixes**: Thinking Budget, Gemini Thinking, UI flashing issues

[View Full Release Notes →](1.9.0.md)

---

## [1.8.1](1.8.1.md) - 2025-11-11

**Core Highlights**: User Model Permission Management · Reasoning Lifecycle Tracking · File Preview Refactor · Chat Cache Optimization

- 👥 **User Model Permission Management**: New permission management system with dual perspective (by user and by model)
- 🧠 **Reasoning Lifecycle Tracking**: Auto-track reasoning state, smart expand/collapse reasoning content
- 📎 **File Preview Refactor**: Unified file preview component supporting images, videos, audio, documents, and more
- ⚡ **Chat List Caching**: localStorage cache + parallel loading, significantly improved first-screen load speed
- 🐴 **TokenPony Provider**: Added TokenPony provider support (ID=19)
- 🔄 **Azure Branding Upgrade**: Azure OpenAI → Azure AI Foundry
- 🛠️ **ChatService Simplification**: Architecture simplified, removed redundant code
- 🐛 **OpenAI 2.6.0 Fix**: Switched to self-compiled Sdcb.OpenAI package, fixed reasoning content parsing issue

[View Full Release Notes →](1.8.1.md)

---

## [1.8.0](1.8.0.md) - 2025-11-04 🎉 Major Architecture Upgrade

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
  <sub>Last updated: 2025-12-21</sub>
</p>
