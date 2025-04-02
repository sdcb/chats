import { ChatStatus, Content } from './chat';
import { IChatMessage } from './chatMessage';
import { DBModelProvider } from './model';
import { Paging } from './page';
import { LoginType } from './user';

export interface SingInParams {
  username?: string;
  password?: string;
  code?: string;
  provider?: string;
}

export interface SingInResult {
  sessionId: string;
  username: string;
  role: string;
}

export interface LoginConfigsResult {
  type: LoginType;
  configs?: {
    appId: string;
  };
}

export interface GetUserBalanceLogsResult {
  date: string;
  value: number;
}

export interface GetUserBalanceResult {
  balance: number;
  logs: GetUserBalanceLogsResult[];
}

export interface ModelUsageDto {
  modelId: number;
  tokens: number;
  counts: number;
  expires: string;
  isTerm: boolean;
  inputTokenPrice1M: number;
  outputTokenPrice1M: number;
}

export interface GetLoginProvidersResult {
  id: string;
  key: LoginType;
  config?: {
    appId: string;
  };
}

export interface GetChatsParams extends Paging {
  groupId: string | null;
  query?: string;
}

export interface ChatResult {
  id: string;
  title: string;
  isShared: boolean;
  spans: ChatSpanDto[];
  leafMessageId?: string;
  updatedAt: string;
  isTopMost: boolean;
  groupId: string | null;
  tags: string[];
}

export interface ChatSpanDto {
  spanId: number;
  enabled: boolean;
  modelId: number;
  modelName: string;
  systemPrompt: string;
  modelProviderId: DBModelProvider;
  temperature: number | null;
  enableSearch: boolean;
  reasoningEffort: number;
  maxOutputTokens: number | null;
}

export interface PostChatParams {
  title: string;
  groupId: string | null;
}

export interface PutChatParams {
  title?: string;
  isShared?: boolean;
  setsLeafMessageId?: boolean;
  leafMessageId?: string;
  isTopMost?: boolean;
  groupId?: string;
  setsGroupId?: boolean;
  isArchived?: boolean;
}

export interface PostUserPassword {
  oldPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface GetBalance7DaysUsageResult {
  date: string;
  costAmount: number;
}

export interface GetUserApiKeyResult {
  id: number;
  key: string;
  isRevoked: boolean;
  comment: string;
  allowEnumerate: boolean;
  allowAllModels: boolean;
  expires: string;
  createdAt: string;
  updatedAt: string;
  lastUsedAt: string;
  modelCount: number;
}

export interface PostUserChatSpanParams {
  modelId?: number;
  setTemperature?: boolean;
  temperature?: number | null;
  enableSearch?: boolean;
}

export interface PostUserChatSpanResult {
  enabled: boolean;
  spanId: number;
  modelId: number;
  modelName: string;
  modelProviderId: number;
  temperature: number;
  enableSearch: boolean;
  reasoningEffort: number;
  maxOutputTokens: number;
}

interface GetUserChatResult {
  id: string;
  title: string;
  isShared: boolean;
  status: ChatStatus;
  spans: ChatSpanDto[];
  leafMessageId?: string;
  isTopMost: boolean;
  groupId: string;
  tags: string[];
  updatedAt: string;
}

export interface GetUserChatGroupWithMessagesResult {
  id: string;
  name: string;
  rank: 0;
  isExpanded: boolean;
  chats: {
    rows: GetUserChatResult[];
    count: 0;
  };
}

export interface PostChatGroupParams {
  name: string;
  rank?: number;
  isExpanded?: boolean;
}

export interface PutChatGroupParams {
  id: string;
  name?: string;
  rank?: number;
  isExpanded?: boolean;
}

export interface PutMoveChatGroupParams {
  groupId: string;
  beforeGroupId: string | null;
  afterGroupId: string | null;
}

export interface PostUserChatShareResult {
  shareId: string;
  expiresAt: string;
  snapshotTime: string;
}

export interface GetChatShareResult {
  id: string;
  title: string;
  isTopMost: boolean;
  isShared: boolean;
  spans: ChatSpanDto[];
  groupId: string;
  tags: string[];
  leafMessageId: string;
  updatedAt: string;
  messages: IChatMessage[];
}

export interface GetChatVersionResult {
  hasNewVersion: boolean;
  tagName: string;
  currentVersion: number;
}

export interface PutResponseMessageContent {
  text?: string;
  fileIds?: string[];
}

export interface PutResponseMessageEditAndSaveNewParams {
  messageId: string;
  contentId: string;
  c: string;
}

export interface PutResponseMessageEditAndSaveNewResult {
  id: string;
  parentId: string;
  edited: boolean;
  content: Content[];
}

export interface PutResponseMessageEditInPlaceParams {
  messageId: string;
  contentId: string;
  c: string;
}

export interface PutChatSpanParams {
  modelId: number;
  enabled: boolean;
  systemPrompt: string;
  temperature?: number | null;
  webSearchEnabled?: boolean;
  maxOutputTokens: number | null;
  reasoningEffort?: number | null;
}

export interface GetChatPresetResult {
  id: string;
  name: string;
  updatedAt: string;
  spans: ChatSpanDto[];
}

export interface PutChatPresetParams {
  name: string;
  spans: PutChatPresetSpanParams[];
}

export interface PutChatPresetSpanParams {
  modelId: number;
  enabled: boolean;
  systemPrompt: string;
  temperature?: number | null;
  webSearchEnabled?: boolean;
  maxOutputTokens: number | null;
  reasoningEffort?: number | null;
}
