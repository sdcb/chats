import {
  ChatRole,
  ChatSpanStatus,
  FileDef,
  ResponseContent,
  ToolProgressDelta,
  Role,
} from './chat';

// Enum equivalent to SseResponseKind
export enum SseResponseKind {
  EndStep = -2,
  EndTurn = -1,
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
  FileGenerating = 11,
  FileGenerated = 12,
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

interface SseResponseLineFileGenerated {
  k: SseResponseKind.FileGenerated; // Kind is FileGenerated
  i: number; // SpanId is required for FileGenerated
  r: FileDef;
}

interface SseResponseLineFileGenerating {
  k: SseResponseKind.FileGenerating; // Kind is FileGenerating (preview)
  i: number; // SpanId is required for FileGenerating
  r: FileDef;
}

interface SseResponseLineCallingTool {
  k: SseResponseKind.CallingTool; // Kind is CallingTool
  i: number; // SpanId is required for CallingTool
  u: string; // ToolCallId
  r: string; // ToolName
  p: string; // Parameters (流式输出的参数)
}

interface SseResponseLineToolProgress {
  k: SseResponseKind.ToolProgress; // Kind is ToolProgress
  i: number; // SpanId is required for ToolProgress
  u: string; // ToolCallId
  r: ToolProgressDelta; // Progress delta (strong-typed)
}

interface SseResponseLineToolCompleted {
  k: SseResponseKind.ToolCompleted; // Kind is ToolCompleted
  i: number; // SpanId is required for ToolCompleted
  u: string; // ToolCallId
  r: string; // Result (工具调用结果)
}

interface SseResponseLineEndStep {
  k: SseResponseKind.EndStep; // Kind is EndStep
  i: number; // SpanId
  r: IStep; // Step data
}

interface SseResponseLineEndTurn {
  k: SseResponseKind.EndTurn; // Kind is EndTurn
  i: number; // SpanId
  r: IChatMessage; // Turn data
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
  | SseResponseLineFileGenerated
  | SseResponseLineFileGenerating
  | SseResponseLineCallingTool
  | SseResponseLineToolProgress
  | SseResponseLineToolCompleted
  | SseResponseLineEndStep
  | SseResponseLineEndTurn;

/// Step represents a single step within a turn
export interface IStep {
  id: string;
  contents: ResponseContent[];
  edited: boolean;
  createdAt: string;
}

export interface IChatMessage {
  id: string;
  spanId: number | null;
  parentId: string | null;
  siblingIds: string[];
  role: ChatRole;
  steps: IStep[];
  status: ChatSpanStatus;
  isActive?: boolean;
  modelName?: string;
  modelId: number;
  modelProviderId?: number;
  reaction?: boolean | null;
  displayType?: MessageDisplayType;
  createdAt?: string;
}

/// Helper function to get all contents from a message's steps
export function getMessageContents(message: IChatMessage): ResponseContent[] {
  return message.steps.flatMap((step) => step.contents);
}

/// Helper function to check if all steps in the message are edited
export function isAllStepsEdited(message: IChatMessage): boolean {
  return message.steps.length > 0 && message.steps.every((step) => step.edited);
}

/// Helper function to check if any step in the message is edited (kept for compatibility)
export function isMessageEdited(message: IChatMessage): boolean {
  return message.steps.some((step) => step.edited);
}

export interface IStepGenerateInfo {
  inputCachedTokens?: number;
  inputOverallTokens?: number;
  outputTokens: number;
  inputFreshPrice?: number;
  inputCachedPrice?: number;
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
  steps: IStep[];
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
  steps: IStep[];
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
}

export const ResponseMessageTempId = 'RESPONSE_MESSAGE_TEMP_ID';
export const UserMessageTempId = 'USER_MESSAGE_TEMP_ID';

export enum ReactionMessageType {
  Good = 1,
  Bad = 2,
}

export type MessageDisplayType = 'Preview' | 'Raw';
