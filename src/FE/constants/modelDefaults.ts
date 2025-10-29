/**
 * ChatCompletion/Response API 默认模型配置
 * 用于创建新的聊天/对话模型时的初始配置
 */
export const DEFAULT_CHAT_MODEL_CONFIG = {
  allowVision: false,
  allowSearch: false,
  allowSystemPrompt: true,
  allowStreaming: true,
  allowCodeExecution: false,
  allowToolCall: true,
  thinkTagParserEnabled: false,
  minTemperature: 0.0,
  maxTemperature: 2.0,
  contextWindow: 128000,
  maxResponseTokens: 4096,
  reasoningEffortOptions: [] as number[],
  supportedImageSizes: [] as string[],
  apiType: 0,
  useAsyncApi: false,
  useMaxCompletionTokens: false,
  isLegacy: false,
  inputTokenPrice1M: 0,
  outputTokenPrice1M: 0,
};

/**
 * ImageGeneration API 默认模型配置
 * 用于创建新的图片生成模型时的初始配置
 */
export const DEFAULT_IMAGE_MODEL_CONFIG = {
  allowVision: false,
  allowSearch: false,
  allowSystemPrompt: false,
  allowStreaming: true, // 返回中间状态预览图片
  allowCodeExecution: false,
  allowToolCall: false,
  thinkTagParserEnabled: false,
  minTemperature: 0.0,
  maxTemperature: 2.0,
  contextWindow: 0,
  maxResponseTokens: 10, // 最大批量生成图片数量
  reasoningEffortOptions: [2, 3, 4] as number[], // 图片质量：低、中、高
  supportedImageSizes: ['1024x1024', '1792x1024', '1024x1792'] as string[],
  apiType: 2,
  useAsyncApi: false,
  useMaxCompletionTokens: false,
  isLegacy: false,
  inputTokenPrice1M: 0,
  outputTokenPrice1M: 0,
};

/**
 * 默认模型配置（向后兼容）
 * @deprecated 请使用 DEFAULT_CHAT_MODEL_CONFIG 或 DEFAULT_IMAGE_MODEL_CONFIG
 */
export const DEFAULT_MODEL_CONFIG = DEFAULT_CHAT_MODEL_CONFIG;
