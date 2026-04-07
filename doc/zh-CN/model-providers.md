# 支持的大模型服务

[English](../en-US/model-providers.md) | **简体中文**

Sdcb Chats 支持 22+ 主流 AI 模型服务商，以下是完整列表：

| Id  | Name             | 加入时间                                                    | 交错思考 |
| --- | ---------------- | ----------------------------------------------------------- | -------- |
| 0   | 测试             | [2024-11-18](https://github.com/sdcb/chats/commit/66d011b1) |          |
| 1   | Azure AI Foundry | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ✅        |
| 2   | 腾讯混元         | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ❓        |
| 3   | 零一万物         | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) |          |
| 4   | 月之暗面         | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ✅        |
| 5   | OpenAI           | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ✅        |
| 6   | 百度千帆         | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) |          |
| 7   | 阿里百炼         | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ❓        |
| 8   | 讯飞星火         | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) |          |
| 9   | 智谱AI           | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ❓        |
| 10  | DeepSeek         | [2024-12-06](https://github.com/sdcb/chats/commit/30db0079) | ✅        |
| 11  | x.ai             | [2024-12-11](https://github.com/sdcb/chats/commit/0d1cab20) |          |
| 12  | Github Models    | [2024-12-11](https://github.com/sdcb/chats/commit/0d1cab20) |          |
| 13  | 谷歌AI           | [2025-01-10](https://github.com/sdcb/chats/commit/a4effc1b) | ✅        |
| 14  | Ollama           | [2025-01-20](https://github.com/sdcb/chats/commit/6a5288e7) |          |
| 15  | MiniMax          | [2025-01-20](https://github.com/sdcb/chats/commit/6a5288e7) | ✅        |
| 16  | 火山方舟         | [2025-01-24](https://github.com/sdcb/chats/commit/843510ff) | ❓        |
| 17  | 硅基流动         | [2025-02-08](https://github.com/sdcb/chats/commit/889144cf) | ❓        |
| 18  | OpenRouter       | [2025-03-05](https://github.com/sdcb/chats/commit/15adedfe) | ❓        |
| 19  | 小马算力         | [2025-11-07](https://github.com/sdcb/chats/commit/32e4a0d5) | ❓        |
| 20  | Anthropic        | [2025-11-24](https://github.com/sdcb/chats/commit/22ebef98) | ✅        |
| 21  | 小米Mimo         | [2025-12-17](https://github.com/sdcb/chats/commit/026f1a4e) | ✅        |
| 22  | Novita AI        | [2026-03-13](https://github.com/sdcb/chats/commit/cecfc66d) | ✅        |

## 注意事项

- ✅ 任何符合 OpenAI Chat Completion API 协议的模型提供商都可以通过 Chats 进行访问
- 🤖 OpenAI/Azure AI Foundry 的 o3/o4-mini/gpt-5 系列模型使用 Response API 协议（非 Chat Completion API），支持思考概要和思考过程功能
- 🌐 Google AI 的 Gemini 模型使用 Google Gemini 原生 API 协议
- ❓ 模型提供商使用了基于 Anthropic Messages API 的实现，按协议推断应该支持，但由于未做过端到端测试，因此不确定是否能实现完整的交错思考能力。

## 交错思考功能说明

交错思考（Interleaved Thinking）是一种高级推理功能，允许模型在生成回复时展示其思考过程。这对于复杂的推理任务特别有用。

- **✅ 完全支持**：经过端到端测试，确认支持交错思考功能
- **❓ 协议兼容**：基于 API 协议推断应该支持，但尚未完成端到端测试
- **空白**：该提供商不支持或未实现交错思考功能

## 支持的 API 协议

Chats 支持多种 AI 模型 API 协议，为不同的应用场景提供灵活的集成方式：

### 1. OpenAI Chat Completion API

这是 Chats 支持的基本协议，也是业界最广泛使用的标准。大多数模型提供商都兼容此协议。

**支持的功能：**
- 标准的对话生成
- 流式输出
- 函数调用（Function Calling）
- 视觉理解（Vision）
- 结构化输出（Structured Outputs）

### 2. OpenAI Response API

用于 OpenAI 和 Azure AI Foundry 的高级推理模型（如 o3、o4-mini、gpt-5 系列）。

**特殊功能：**
- 思考概要（Thinking Summary）
- 完整思考过程展示
- 增强的推理能力

### 3. Anthropic Messages API

为 Anthropic Claude 系列模型提供的原生协议支持。

**支持的功能：**
- Claude 原生消息格式
- 交错思考（Interleaved Thinking）
- 扩展上下文窗口
- 工具使用（Tool Use）

### 4. Google Gemini API

为 Google AI 的 Gemini 模型提供的原生协议支持。

**支持的功能：**
- Gemini 原生消息格式
- 多模态输入
- 代码执行
- 搜索增强

### 5. Image Generation API

支持 OpenAI 的 `gpt-image` 图像生成协议。

**支持的功能：**
- 文本到图像生成
- 图像变体生成
- 可配置的图像大小和质量

### 6. Image Edit API

支持 OpenAI 的图像编辑协议。

**支持的功能：**
- 基于提示的图像编辑
- 图像修复（Inpainting）
- 局部编辑

## 自定义模型提供商

由于 Chats 支持上述多种标准 API 协议，您可以添加任何兼容这些协议的自定义模型提供商，包括但不限于：

- 本地部署的开源模型（通过 Ollama、vLLM、LM Studio 等）
- 第三方 API 代理服务（如 OpenRouter、One API 等）
- 企业内部的模型服务
- 自建的模型推理服务
