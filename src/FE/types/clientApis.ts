import { ChatStatus, ResponseContent, UsageSource } from './chat';
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

export interface ChatSpanMcp {
  id: number;
  customHeaders?: string;
}

export interface ChatSpanDto {
  spanId: number;
  enabled: boolean;
  modelId: number;
  modelName: string;
  systemPrompt: string;
  modelProviderId: DBModelProvider;
  temperature: number | null;
  webSearchEnabled: boolean;
  reasoningEffort: number;
  maxOutputTokens: number | null;
  imageSize: number;
  mcps: ChatSpanMcp[];
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
  webSearchEnabled?: boolean;
}

export interface PostUserChatSpanResult {
  enabled: boolean;
  spanId: number;
  modelId: number;
  modelName: string;
  modelProviderId: number;
  temperature: number;
  webSearchEnabled: boolean;
  reasoningEffort: number;
  maxOutputTokens: number;
  imageSize: number;
  mcps: ChatSpanMcp[];
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
  imageSize?: number;
  mcps?: ChatSpanMcp[];
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
  imageSize?: number;
  mcps?: ChatSpanMcp[];
}

export interface ChatPresetReorderRequest {
  sourceId: string;
  previousId: string | null; // 新位置的前一个元素
  nextId: string | null;     // 新位置的后一个元素
}

export interface GetUsageParams {
  user?: string;
  kid?: string;
  provider?: string;
  start?: string;
  end?: string;
  page?: number;
  pageSize?: number;
  skip?: number;
  tz: number;
  source?: UsageSource;
}

export interface GetUsageResult {
  userName: string;
  apiKeyId: string;
  apiKey: string;
  modelProviderName: string;
  modelReferenceName: string;
  modelName: string;
  preprocessDurationMs: number;
  firstResponseDurationMs: number;
  postprocessDurationMs: number;
  totalDurationMs: number;
  finishReason: string;
  userAgent: string;
  ip: string;
  inputTokens: number;
  outputTokens: number;
  reasoningTokens: number;
  inputCost: number;
  outputCost: number;
  usagedCreatedAt: string;
}

export interface GetUsageStatResult {
  totalRequests: number;
  sumInputTokens: number;
  sumOutputTokens: number;
  sumReasoningTokens: number;
  sumInputCost: number;
  sumOutputCost: number;
  sumTotalCost: number;
  avgTotalCost: number;
  avgPreprocessDurationMs: number;
  avgFirstResponseDurationMs: number;
  avgPostprocessDurationMs: number;
  avgTotalDurationMs: number;
}

export interface GetUserFilesParams extends Paging {
  skip?: number;
}

export interface GetUserFilesResult {
  id: string;
  fileName: string;
  contentType: string;
  url: string;
}

// MCP related types
export interface McpToolBasicInfo {
  name: string;
  description?: string;
  parameters?: string;
}

export interface McpServerListItemDto {
  id: number;
  label: string;
}

export interface McpServerListManagementItemDto {
  id: number;
  label: string;
  url: string;
  createdAt: string;
  updatedAt: string;
  toolsCount: number;
  owner: string;
  editable: boolean;
  assignedUserCount: number;
}

export interface McpServerDetailsDto extends McpServerListManagementItemDto {
  headers?: string;
  tools: McpToolBasicInfo[];
}

export interface UpdateMcpServerRequest {
  label: string;
  url: string;
  headers?: string;
  tools: McpToolBasicInfo[];
}

export interface FetchToolsRequest {
  serverUrl: string;
  headers?: string;
}

// MCP用户分配相关类型
export interface AssignedUserInfo {
  id: number;
  customHeaders?: string;
}

export interface AssignUsersToMcpRequest {
  toAssignedUsers: AssignedUserInfo[];
  toUpdateUsers: AssignedUserInfo[];
  toDeleteUserIds: number[];
}

export interface UnassignedUserDto {
  id: number;
  userName: string;
}

export interface AssignedUserDetailsDto {
  id: number;
  userName: string;
  customHeaders?: string;
}

export interface AssignedUserNameDto {
  id: number;
  userName: string;
}
