import {
  ChatRole,
  ChatSpanStatus,
  FileDef,
  ResponseContent,
  Role,
} from './chat';

// Enum equivalent to SseResponseKind
export enum SseResponseKind {
  StopId = 0,
  Segment = 1,
  Error = 2,
  UserMessage = 3,
  UpdateTitle = 4,
  TitleSegment = 5,
  ResponseMessage = 6,
  ChatLeafMessageId = 7,
  ReasoningSegment = 8,
  StartResponse = 9,
  StartReasoning = 10,
  ImageGenerating = 11,
  ImageGenerated = 12,
  CallingTool = 13,
  ToolProgress = 14,
  ToolCompleted = 15,
}

// Discriminated unions for SseResponseLine
interface SseResponseLineStopId {
  k: SseResponseKind.StopId; // Kind is StopId
  r: string; // Result is a string
}

interface SseResponseLineSegment {
  i: number; // SpanId is required for Segment
  k: SseResponseKind.Segment; // Kind is Segment
  r: string; // Result is a string
}

interface SseResponseLineError {
  i: number; // SpanId is required for Error
  k: SseResponseKind.Error; // Kind is Error
  r: string; // Result is a string
}

interface SseResponseLineUserMessage {
  k: SseResponseKind.UserMessage; // Kind is UserMessage
  r: IChatMessage; // Result is ChatMessage
}

interface SseResponseLineResponseMessage {
  i: number; // SpanId is required for ResponseMessage
  k: SseResponseKind.ResponseMessage; // Kind is ResponseMessage
  r: IChatMessage; // Result is ChatMessage
}

interface SseResponseLineUpdateTitle {
  k: SseResponseKind.UpdateTitle; // Kind is UpdateTitle
  r: string; // Result is a string
}

interface SseResponseLineTitleSegment {
  k: SseResponseKind.TitleSegment; // Kind is TitleSegment
  r: string; // Result is a string
}

interface SseResponseLineReasoningSegment {
  i: number; // SpanId is required for Segment
  k: SseResponseKind.ReasoningSegment; // Kind is ReasoningSegment
  r: string; // Result is a string
}

interface SseResponseLineStartResponse {
  k: SseResponseKind.StartResponse; // Kind is StartResponse
  r: number; // Result is reasoning duration
  i: number; // SpanId is required for StartResponse
}

interface SseResponseLineStartReasoning {
  k: SseResponseKind.StartReasoning; // Kind is StartReasoning
  i: number; // SpanId is required for StartReasoning
}

interface SseResponseLineImageGenerated {
  k: SseResponseKind.ImageGenerated; // Kind is StartReasoning
  i: number; // SpanId is required for StartReasoning
  r: FileDef;
}

interface SseResponseLineCallingTool {
  k: SseResponseKind.CallingTool; // Kind is CallingTool
  i: number; // SpanId is required for CallingTool
  u: string; // ToolCallId
  r: string; // ToolName
  p: string; // Parameters (流式输出的参数)
}

interface SseResponseLineToolCompleted {
  k: SseResponseKind.ToolCompleted; // Kind is ToolCompleted
  i: number; // SpanId is required for ToolCompleted
  u: string; // ToolCallId
  r: string; // Result (工具调用结果)
}

// Combined type for SseResponseLine
export type SseResponseLine =
  | SseResponseLineStopId
  | SseResponseLineSegment
  | SseResponseLineError
  | SseResponseLineUserMessage
  | SseResponseLineResponseMessage
  | SseResponseLineUpdateTitle
  | SseResponseLineTitleSegment
  | SseResponseLineReasoningSegment
  | SseResponseLineStartResponse
  | SseResponseLineStartReasoning
  | SseResponseLineImageGenerated
  | SseResponseLineCallingTool
  | SseResponseLineToolCompleted;

export interface IChatMessage {
  id: string;
  spanId: number | null;
  parentId: string | null;
  siblingIds: string[];
  role: ChatRole;
  content: ResponseContent[];
  status: ChatSpanStatus;
  isActive?: boolean;
  modelName?: string;
  modelId: number;
  modelProviderId?: number;
  inputPrice?: number;
  outputPrice?: number;
  inputTokens?: number;
  outputTokens?: number;
  reasoningTokens?: number;
  reasoningDuration?: number;
  duration?: number;
  firstTokenLatency?: number;
  reaction?: boolean | null;
  edited?: boolean;
  displayType?: MessageDisplayType;
  createdAt?: string;
}

export interface IStepGenerateInfo {
  inputTokens: number;
  outputTokens: number;
  inputPrice: number;
  outputPrice: number;
  reasoningTokens: number;
  duration: number;
  reasoningDuration: number;
  firstTokenLatency: number;
}

export interface MessageNode {
  id: string;
  parentId: string | null;
  content: ResponseContent[];
  siblingIds: string[];
  modelName?: string;
  role: Role;
  inputTokens?: number;
  outputTokens?: number;
  reasoningTokens?: number;
  inputPrice?: number;
  outputPrice?: number;
}

export interface ChatMessageNode {
  id: string;
  parentId: string | null;
  modelId: number;
  content: ResponseContent[];
  siblingIds: string[];
  isActive?: boolean;
  status: ChatSpanStatus;
  spanId: number | null;
  role: ChatRole;
  modelName?: string;
  inputTokens?: number;
  outputTokens?: number;
  reasoningTokens?: number;
  inputPrice?: number;
  outputPrice?: number;
  reasoningDuration?: number;
  duration?: number;
  firstTokenLatency?: number;
  reaction?: boolean | null;
  edited?: boolean;
}

export const ResponseMessageTempId = 'RESPONSE_MESSAGE_TEMP_ID';
export const UserMessageTempId = 'USER_MESSAGE_TEMP_ID';

export enum ReactionMessageType {
  Good = 1,
  Bad = 2,
}

export type MessageDisplayType = 'Preview' | 'Raw';