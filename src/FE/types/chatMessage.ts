import { ChatRole, ChatSpanStatus, Content, Role } from './chat';

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
  | SseResponseLineStartReasoning;

export interface IChatMessage {
  id: string;
  spanId: number | null;
  parentId: string | null;
  siblingIds: string[];
  role: ChatRole;
  content: Content[];
  status: ChatSpanStatus;
  isActive?: boolean;
  modelName?: string;
  modelId?: number;
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
}

export interface MessageNode {
  id: string;
  parentId: string | null;
  content: Content[];
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
  content: Content[];
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
}

export const ResponseMessageTempId = 'RESPONSE_MESSAGE_TEMP_ID';
export const UserMessageTempId = 'USER_MESSAGE_TEMP_ID';

export enum ReactionMessageType {
  Good = 1,
  Bad = 2,
}
