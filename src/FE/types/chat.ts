import { DBModelProvider, UserModelConfig } from './model';

export type Role = 'assistant' | 'user' | 'system';
export const DEFAULT_TEMPERATURE = 0.5;

export enum ChatStatus {
  None = 1,
  Running = 2,
  Failed = 3,
}

export interface Message {
  role: Role;
  content: Content;
}

export interface ImageDef {
  id: string;
  url: string;
}

export interface Content {
  error?: string;
  text?: string;
  fileIds?: ImageDef[];
}

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
  // id: string;
  // title: string;
  // chatModelId?: string;
  // modelName: string;
  // userModelConfig: UserModelConfig;
  // isShared: boolean;

  id: string;
  title: string;
  modelId: number;
  modelName: string;
  isShared: boolean;
  userModelConfig: UserModelConfig;
  modelProviderId: DBModelProvider;
}
