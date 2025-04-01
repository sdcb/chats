import { ChatSpanDto } from './clientApis';

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
}

export enum ChatStatus {
  None = 1,
  Chatting = 2,
  Failed = 3,
}

export interface Message {
  role: ChatRole;
  content: Content[];
}

export interface ImageDef {
  id: string;
  url: string;
}

export type Content =
  | ReasoningContent
  | TextContent
  | FileContent
  | ErrorContent;

export type ReasoningContent = {
  i: string;
  $type: MessageContentType.reasoning;
  c: string;
};

export type TextContent = {
  i: string;
  $type: MessageContentType.text;
  c: string;
};

export type FileContent = {
  i: string;
  $type: MessageContentType.fileId;
  c: ImageDef | string;
};

export type ErrorContent = {
  i: string;
  $type: MessageContentType.error;
  c: string;
};

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
}
