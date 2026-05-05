import { UpdateModelDto } from '@/types/adminApis';

/**
 * API 类型枚举
 */
export enum ApiType {
  ChatCompletion = 0,
  Response = 1,
  ImageGeneration = 2,
  AnthropicMessages = 3,
}

/**
 * ChatCompletion/Response API 默认模型配置
 * 用于创建新的聊天/对话模型时的初始配置
 */
const DEFAULT_CHAT_MODEL_CONFIG: Partial<UpdateModelDto> = {
  allowVision: false,
  allowSearch: false,
  allowStreaming: true,
  allowCodeExecution: false,
  allowToolCall: true,
  thinkTagParserEnabled: false,
  minTemperature: 0.0,
  maxTemperature: 2.0,
  contextWindow: 128000,
  maxResponseTokens: 8192,
  maxThinkingBudget: null,
  supportedEfforts: [] as string[],
  supportedImageSizes: [] as string[],
  supportedFormats: [] as string[],
  overrideUrl: null,
  customHeaders: null,
  customBody: null,
  apiType: ApiType.ChatCompletion,
  useAsyncApi: false,
  useMaxCompletionTokens: false,
  isLegacy: false,
};

/**
 * Response API 默认模型配置
 * 用于创建新的推理模型时的初始配置（如 gpt-5 系列）
 */
const DEFAULT_RESPONSE_MODEL_CONFIG: Partial<UpdateModelDto> = {
  allowVision: true,
  allowSearch: false,
  allowStreaming: true,
  allowCodeExecution: false,
  allowToolCall: true,
  thinkTagParserEnabled: false,
  minTemperature: 0.0,
  maxTemperature: 2.0,
  contextWindow: 128000,
  maxResponseTokens: 16384,
  maxThinkingBudget: null,
  supportedEfforts: ['low', 'medium', 'high'],
  supportedImageSizes: [] as string[],
  supportedFormats: [] as string[],
  overrideUrl: null,
  customHeaders: null,
  customBody: null,
  apiType: ApiType.Response,
  useAsyncApi: false,
  useMaxCompletionTokens: true,
  isLegacy: false,
};

/**
 * AnthropicMessages API 默认模型配置
 * 用于创建新的 Anthropic Messages 模型时的初始配置
 */
const DEFAULT_ANTHROPIC_MESSAGES_CONFIG: Partial<UpdateModelDto> = {
  allowVision: true,
  supportsVisionLink: true,
  allowSearch: true,
  allowStreaming: true,
  allowCodeExecution: false,
  allowToolCall: true,
  thinkTagParserEnabled: false,
  minTemperature: 0.0,
  maxTemperature: 2.0,
  contextWindow: 200000,
  maxResponseTokens: 32000,
  maxThinkingBudget: 31999, // 默认为 maxResponseTokens - 1
  supportedEfforts: [] as string[],
  supportedImageSizes: [] as string[],
  supportedFormats: [] as string[],
  overrideUrl: null,
  customHeaders: null,
  customBody: null,
  apiType: ApiType.AnthropicMessages,
  useAsyncApi: false,
  useMaxCompletionTokens: false,
  isLegacy: false,
};

/**
 * ImageGeneration API 默认模型配置
 * 用于创建新的图片生成模型时的初始配置
 */
const DEFAULT_IMAGE_MODEL_CONFIG: Partial<UpdateModelDto> = {
  allowVision: true,
  allowSearch: false,
  allowStreaming: true, // 返回中间状态预览图片
  allowCodeExecution: false,
  allowToolCall: false,
  thinkTagParserEnabled: false,
  minTemperature: 0.0,
  maxTemperature: 2.0,
  contextWindow: 0,
  maxResponseTokens: 10, // 最大批量生成图片数量
  maxThinkingBudget: null,
  supportedEfforts: ['low', 'medium', 'high'],
  supportedImageSizes: ['1024x1024', '1536x1024', '1024x1536'],
  supportedFormats: ['png', 'jpeg', 'webp'],
  overrideUrl: null,
  customHeaders: null,
  customBody: null,
  apiType: ApiType.ImageGeneration,
  useAsyncApi: false,
  useMaxCompletionTokens: false,
  isLegacy: false,
};

/**
 * 根据 API 类型获取默认配置
 * @param apiType API 类型
 * @returns 默认配置的部分字段
 */
export function getDefaultConfigByApiType(apiType: ApiType): Partial<UpdateModelDto> {
  switch (apiType) {
    case ApiType.ChatCompletion:
      return { ...DEFAULT_CHAT_MODEL_CONFIG };
    case ApiType.Response:
      return { ...DEFAULT_RESPONSE_MODEL_CONFIG };
    case ApiType.ImageGeneration:
      return { ...DEFAULT_IMAGE_MODEL_CONFIG };
    case ApiType.AnthropicMessages:
      return { ...DEFAULT_ANTHROPIC_MESSAGES_CONFIG };
    default:
      return { ...DEFAULT_CHAT_MODEL_CONFIG };
  }
}
