# Supported LLM Services

**English** | [简体中文](../zh-CN/model-providers.md)

Sdcb Chats supports 21+ mainstream AI model providers. Here is the complete list:

| Id  | Name             | Added                                                       | Interleaved Thinking |
| --- | ---------------- | ----------------------------------------------------------- | -------------------- |
| 0   | Test             | [2024-11-18](https://github.com/sdcb/chats/commit/66d011b1) |                      |
| 1   | Azure AI Foundry | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ✅                    |
| 2   | Tencent Hunyuan  | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ❓                    |
| 3   | 01.ai            | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) |                      |
| 4   | Moonshot         | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ✅                    |
| 5   | OpenAI           | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ✅                    |
| 6   | Wenxin Qianfan   | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) |                      |
| 7   | Alibaba Bailian  | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ❓                    |
| 8   | Xunfei SparkDesk | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) |                      |
| 9   | Zhipu AI         | [2024-09-05](https://github.com/sdcb/chats/commit/3b3918af) | ❓                    |
| 10  | DeepSeek         | [2024-12-06](https://github.com/sdcb/chats/commit/30db0079) | ✅                    |
| 11  | x.ai             | [2024-12-11](https://github.com/sdcb/chats/commit/0d1cab20) |                      |
| 12  | Github Models    | [2024-12-11](https://github.com/sdcb/chats/commit/0d1cab20) |                      |
| 13  | Google AI        | [2025-01-10](https://github.com/sdcb/chats/commit/a4effc1b) | ✅                    |
| 14  | Ollama           | [2025-01-20](https://github.com/sdcb/chats/commit/6a5288e7) |                      |
| 15  | MiniMax          | [2025-01-20](https://github.com/sdcb/chats/commit/6a5288e7) | ✅                    |
| 16  | Doubao           | [2025-01-24](https://github.com/sdcb/chats/commit/843510ff) | ❓                    |
| 17  | SiliconFlow      | [2025-02-08](https://github.com/sdcb/chats/commit/889144cf) | ❓                    |
| 18  | OpenRouter       | [2025-03-05](https://github.com/sdcb/chats/commit/15adedfe) | ❓                    |
| 19  | Token Pony       | [2025-11-07](https://github.com/sdcb/chats/commit/32e4a0d5) | ❓                    |
| 20  | Anthropic        | [2025-11-24](https://github.com/sdcb/chats/commit/22ebef98) | ✅                    |
| 21  | Xiaomi Mimo      | [2025-12-17](https://github.com/sdcb/chats/commit/026f1a4e) | ✅                    |

## Notes

- ✅ Any model provider that complies with the OpenAI Chat Completion API protocol can be accessed through Chats
- 🤖 OpenAI/Azure AI Foundry's o3/o4-mini/gpt-5 series models use the Response API protocol (not Chat Completion API), supporting thought summary and thought process features
- 🌐 Google AI's Gemini model uses the native Google Gemini API protocol
- ❓ The provider uses an Anthropic Messages API–based implementation. It should support interleaved thinking by protocol, but it's not end-to-end tested, so full support is unverified.

## Interleaved Thinking Feature

Interleaved Thinking is an advanced reasoning feature that allows models to display their thought process while generating responses. This is particularly useful for complex reasoning tasks.

- **✅ Fully Supported**: End-to-end tested, confirmed to support interleaved thinking
- **❓ Protocol Compatible**: Should support based on API protocol inference, but end-to-end testing not yet completed
- **Blank**: The provider does not support or has not implemented interleaved thinking

## Supported API Protocols

Chats supports multiple AI model API protocols, providing flexible integration options for different scenarios:

### 1. OpenAI Chat Completion API

This is the basic protocol supported by Chats and the most widely adopted standard in the industry. Most model providers are compatible with this protocol.

**Supported Features:**
- Standard conversation generation
- Streaming output
- Function Calling
- Vision understanding
- Structured Outputs

### 2. OpenAI Response API

Used for advanced reasoning models from OpenAI and Azure AI Foundry (such as o3, o4-mini, gpt-5 series).

**Special Features:**
- Thinking Summary
- Full thought process display
- Enhanced reasoning capabilities

### 3. Anthropic Messages API

Native protocol support for Anthropic Claude series models.

**Supported Features:**
- Claude native message format
- Interleaved Thinking
- Extended context window
- Tool Use

### 4. Google Gemini API

Native protocol support for Google AI's Gemini models.

**Supported Features:**
- Gemini native message format
- Multimodal input
- Code execution
- Search grounding

### 5. Image Generation API

Supports OpenAI's `gpt-image` image generation protocol.

**Supported Features:**
- Text-to-image generation
- Image variation generation
- Configurable image size and quality

### 6. Image Edit API

Supports OpenAI's image editing protocol.

**Supported Features:**
- Prompt-based image editing
- Image inpainting
- Localized editing

## Custom Model Providers

Since Chats supports the multiple standard API protocols mentioned above, you can add any custom model provider compatible with these protocols, including but not limited to:

- Locally deployed open-source models (via Ollama, vLLM, LM Studio, etc.)
- Third-party API proxy services (such as OpenRouter, One API, etc.)
- Enterprise internal model services
- Self-hosted model inference services
