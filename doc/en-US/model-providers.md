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

## Custom Model Providers

Since Chats supports the standard OpenAI Chat Completion API protocol, you can also add any custom model provider compatible with this protocol, including but not limited to:

- Locally deployed open-source models (via Ollama, vLLM, etc.)
- Third-party API proxy services
- Enterprise internal model services
