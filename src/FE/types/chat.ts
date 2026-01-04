import { getApiUrl } from '@/utils/common';
import { ChatSpanDto } from './clientApis';
import { getUserSession } from '@/utils/user';

export type Role = 'assistant' | 'user' | 'system';
export enum ChatRole {
  'System' = 1,
  'User' = 2,
  'Assistant' = 3,
}
export const DEFAULT_TEMPERATURE = 0.5;

export enum ChatSpanStatus {
  None = 1,
  Chatting = 2,
  Failed = 3,
  Reasoning = 4,
  Pending = 5,
}

export enum ChatStatus {
  None = 1,
  Chatting = 2,
  Failed = 3,
}

export interface Message {
  role: ChatRole;
  content: ResponseContent[];
}

export interface FileDef {
  id: string;
  contentType: string;
  fileName: string | null;
  url: string | null;
}

export function getFileUrl(file: FileDef | string): string {
  const url = typeof file === 'string' ? null : file.url;
  if (url) {
    return url;
  }

  const fileId = typeof file === 'string' ? file : file.id;
  return `${getApiUrl()}/api/file/private/${fileId}?token=${getUserSession()}`;
}

export type ResponseContent =
  | ReasoningContent
  | TextContent
  | FileContent
  | TempFileContent
  | ErrorContent
  | ToolCallContent
  | ToolResponseContent;

export type ReasoningContent = {
  i: string;
  $type: MessageContentType.reasoning;
  c: string;
  // 标记该段推理是否已完整结束；未定义的历史数据视为已结束
  finished?: boolean;
};

export type TextContent = {
  i: string;
  $type: MessageContentType.text;
  c: string;
};

export type FileContent = {
  i: string;
  $type: MessageContentType.fileId;
  c: FileDef | string;
};

export type TempFileContent = {
  i: string;
  $type: MessageContentType.tempFileId;
  c: FileDef | string;
};

export type ErrorContent = {
  i: string;
  $type: MessageContentType.error;
  c: string;
};

export type ToolCallContent = {
  i: string;
  $type: MessageContentType.toolCall;
  u: string; // ToolCallId
  n: string; // Name
  p: string; // Parameters
};

export type StdOutToolProgressDelta = {
  kind: 'stdout';
  stdOutput: string;
};

export type StdErrorToolProgressDelta = {
  kind: 'stderr';
  stdError: string;
};

export type ToolProgressDelta = StdOutToolProgressDelta | StdErrorToolProgressDelta;

export type ToolResponseContent = {
  i: string;
  $type: MessageContentType.toolResponse;
  u: string; // ToolCallId
  r: string; // Response
  // 流式工具输出（SSE k=14）。存在时表示当前响应区展示的是进度流。
  progress?: ToolProgressDelta[];
};

export type TextRequestContent = {
  $type: MessageContentType.text;
  c: string;
};

export type FileRequestContent = {
  $type: MessageContentType.fileId;
  c: string;
};

export type RequestContent = TextRequestContent | FileRequestContent;

export interface ContentRequest {
  text: string;
  fileIds: string[] | null;
}

export interface ChatBody {
  modelId: number;
  userMessage: ContentRequest;
  messageId: string | null;
  chatId: string;
  userModelConfig: any;
}

export interface IChat {
  id: string;
  title: string;
  isShared: boolean;
  status: ChatStatus;
  spans: ChatSpanDto[];
  leafMessageId?: string;
  isTopMost: boolean;
  groupId: string | null;
  tags: string[];
  updatedAt: string;
  selected?: boolean;
}

export interface IGroupedChat {
  id: string;
  name: string;
  rank: 0;
  isExpanded: boolean;
  messages: {
    rows: IChat[];
  };
  count: 0;
}

export const ChatPinGroup = 'Pin';
export const UngroupedChatName = 'Ungrouped';
export const DefaultChatPaging = {
  groupId: null,
  page: 1,
  pageSize: 50,
};

export interface IChatPaging {
  groupId: string | null;
  count: number;
  page: number;
  pageSize: number;
}

export enum CHATS_SELECT_TYPE {
  NONE = 1,
  DELETE = 2,
  ARCHIVE = 3,
}

export const MAX_SELECT_MODEL_COUNT = 10;
export const MAX_CREATE_PRESET_CHAT_COUNT = 24;

export enum MessageContentType {
  error = 0,
  text = 1,
  fileId = 2,
  reasoning = 3,
  toolCall = 4,
  toolResponse = 5,
  tempFileId = 6,
}

export const EMPTY_ID = 'EMPTY_ID';

export enum UsageSource {
  Web = 1,
  API = 2,
}
