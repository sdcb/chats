import { IChatMessage } from './chatMessage';
import { ChatSpanDto } from './clientApis';
import { DBFileServiceType } from './file';
import { DBModelProvider } from './model';
import { Paging } from './page';
import { PayServiceType } from './pay';
import { StatusCode } from './statusCode';
import { LoginType } from './user';

export const enum UserRole {
  'admin' = 'admin',
}

export interface AddUserModelParams {
  userId: number;
  modelId: number;
  tokens: number;
  counts: number;
  expires: string;
}

export interface BatchUserModelsParams {
  userId: number;
  modelIds: number[];
}

export interface BatchUserModelsByProviderParams {
  userId: number;
  providerId: number;
}

export interface BatchUserModelsByKeyParams {
  userId: number;
  keyId: number;
}

export interface EditUserModelParams {
  tokensDelta: number;
  countsDelta: number;
  expires: string;
}

export interface AdminModelDto {
  modelId: number;
  modelProviderId: number;
  name: string;
  rank: number | null;
  enabled: boolean;
  modelKeyId: number;
  deploymentName: string;
  inputTokenPrice1M: number;
  outputTokenPrice1M: number;
  
  // === 1.8.0 新增字段（全部必填）===
  allowSearch: boolean;
  allowVision: boolean;
  allowSystemPrompt: boolean;
  allowStreaming: boolean;
  allowCodeExecution: boolean;
  allowToolCall: boolean;
  thinkTagParserEnabled: boolean;
  
  minTemperature: number;
  maxTemperature: number;
  
  contextWindow: number;
  maxResponseTokens: number;
  
  reasoningEffortOptions: number[];
  supportedImageSizes: string[];
  
  apiType: number;
  useAsyncApi: boolean;
  useMaxCompletionTokens: boolean;
  isLegacy: boolean;
}

export interface UpdateModelDto {
  name: string;
  enabled: boolean;
  deploymentName: string;
  modelKeyId: number;
  inputTokenPrice1M: number;
  outputTokenPrice1M: number;
  
  // === 1.8.0 新增字段（全部必填）===
  allowSearch: boolean;
  allowVision: boolean;
  allowSystemPrompt: boolean;
  allowStreaming: boolean;
  allowCodeExecution: boolean;
  allowToolCall: boolean;
  thinkTagParserEnabled: boolean;
  
  minTemperature: number;
  maxTemperature: number;
  
  contextWindow: number;
  maxResponseTokens: number;
  
  reasoningEffortOptions: number[];
  supportedImageSizes: string[];
  
  apiType: number;
  useAsyncApi: boolean;
  useMaxCompletionTokens: boolean;
  isLegacy: boolean;
}

export interface PostUserParams {
  username: string;
  password: string;
  role: string;
}

export interface PutUserParams extends PostUserParams {
  id: string;
}

export interface PutUserBalanceParams {
  userId: string;
  value: number;
}

export interface GetUsersParams {
  query?: string;
  page: number;
  pageSize: number;
}
export interface GetUsersResult {
  id: string;
  account: string;
  username: string;
  role: string;
  email: string;
  phone: string;
  balance: number;
  provider: string;
  createdAt: string;
  updatedAt: string;
  enabled: boolean;
  userModelCount: number;
}

export interface UserModelPermissionUserDto {
  id: number;
  username: string;
  email: string | null;
  phone: string | null;
  enabled: boolean;
  userModelCount: number;
  modelProviderCount: number;
}

export interface UserModelProviderDto {
  providerId: number;
  keyCount: number;
  modelCount: number;
  assignedModelCount: number;
}

export interface UserModelKeyDto {
  id: number;
  name: string;
  modelCount: number;
  assignedModelCount: number;
}

export interface UserModelPermissionModelDto {
  modelId: number;
  name: string;
  isAssigned: boolean;
  userModelId: number | null;
  counts: number | null;
  tokens: number | null;
  expires: string | null;
  isDeleted: boolean;
}

export interface UserModelOperationResponse {
  affectedCount: number;
  userModelCount: number;
  providerStats: UserModelProviderDto | null;
  keyStats: UserModelKeyDto | null;
  model: UserModelPermissionModelDto | null;
}

export interface GetUserMessageParams extends Paging {
  user?: string;
  content?: string;
}

export interface AdminChatsDto {
  id: string;
  username: string;
  isDeleted: boolean;
  isShared: boolean;
  title: string;
  createdAt: string;
  spans: ChatSpanDto[];
}

export interface GetMessageDetailsResult {
  name: string;
  modelName?: string;
  modelTemperature?: number;
  modelPrompt?: number;
  messages: IChatMessage[];
}

export interface PostFileServicesParams {
  fileServiceTypeId: DBFileServiceType;
  name: string;
  isDefault: boolean;
  configs: string;
}

export interface GetFileServicesResult extends PostFileServicesParams {
  id: number;
  createdAt: string;
  fileCount: number;
  updatedAt: string;
}

export interface GetRequestLogsParams extends Paging {
  query?: string;
  statusCode?: number;
}

export interface GetRequestLogsListResult {
  id: string;
  ip: string;
  username: string;
  url: string;
  method: string;
  statusCode: StatusCode;
  createdAt: string;
}

export interface GetRequestLogsDetailsResult extends GetRequestLogsListResult {
  headers: string;
  request: string;
  response: string;
  requestTime: number;
  responseTime: number;
  user: { username: string };
}

export interface SecurityLogQueryParams extends Paging {
  tz?: number;
  start?: string;
  end?: string;
  username?: string;
  success?: boolean;
}

export interface SecurityLogExportParams {
  tz?: number;
  start?: string;
  end?: string;
  username?: string;
  success?: boolean;
}

export interface PasswordAttemptLog {
  id: number;
  userName: string;
  userId: number | null;
  matchedUserName: string | null;
  isSuccessful: boolean;
  failureReason: string | null;
  ip: string;
  userAgent: string;
  createdAt: string;
}

export interface KeycloakAttemptLog {
  id: number;
  provider: string;
  sub: string | null;
  email: string | null;
  userId: number | null;
  userName: string | null;
  isSuccessful: boolean;
  failureReason: string | null;
  ip: string;
  userAgent: string;
  createdAt: string;
}

export interface SmsAttemptLog {
  id: number;
  phoneNumber: string;
  code: string;
  userId: number | null;
  userName: string | null;
  type: string | null;
  status: string | null;
  ip: string;
  userAgent: string;
  createdAt: string;
}

export interface GetLoginServicesResult {
  id: number;
  type: LoginType;
  enabled: boolean;
  configs: string;
  createdAt: string;
}

export interface PostLoginServicesParams {
  type: LoginType;
  enabled: boolean;
  configs: string;
}

export interface GetPayServicesResult {
  id: string;
  type: PayServiceType;
  enabled: boolean;
  configs: string;
  createdAt: string;
}

export interface PostPayServicesParams {
  type: PayServiceType;
  enabled: boolean;
  configs: string;
}

export interface PutPayServicesParams extends PostPayServicesParams {
  id: string;
}

export interface ModelProviderDto {
  providerId: number;
  keyCount: number;
  modelCount: number;
}

export class GetModelKeysResult {
  id: number;
  modelProviderId: number;
  name: string;
  enabledModelCount: number;
  totalModelCount: number;
  host: string | null;
  secret: string | null;
  createdAt: string;

  constructor(dto: any) {
    this.id = dto.id;
    this.modelProviderId = dto.modelProviderId;
    this.name = dto.name;
    this.enabledModelCount = dto.enabledModelCount;
    this.totalModelCount = dto.totalModelCount;
    this.host = dto.host;
    this.secret = dto.secret;
    this.createdAt = dto.createdAt;
  }

  toConfigs() {
    const configs: any = {};
    if (this.host !== null) {
      configs.host = this.host;
    }
    if (this.secret !== null) {
      configs.secret = this.secret;
    }
    return configs;
  }
}

export interface PostModelKeysParams {
  modelProviderId: number;
  name: string;
  host: string | null;
  secret: string | null;
}

export interface PostPromptParams {
  name: string;
  content: string;
  isDefault: boolean;
  isSystem: boolean;
}

export interface GetConfigsResult {
  key: string;
  value: string;
  description: string;
}

export interface PostAndPutConfigParams {
  key: string;
  value: string;
  description: string;
}

export interface GetInvitationCodeResult {
  id: string;
  value: string;
  count: number;
  username: string;
}

export interface PostInvitationCodeParams {
  value: string;
  count: number;
}

export interface PutInvitationCodeParams {
  id: string;
  count: number;
}

export interface GetUserInitialConfigResult {
  id: string;
  name: string;
  loginType: string;
  price: number;
  invitationCodeId: string;
  invitationCode: string;
  models: UserInitialModel[];
}

export interface PostUserInitialConfigParams {
  name: string;
  price: number;
  loginType: string;
  invitationCodeId: string | null;
  models: UserInitialModel[];
}

export interface PutUserInitialConfigParams {
  id: string;
  name: string;
  price: number;
  loginType: string;
  invitationCodeId: string | null;
  models: UserInitialModel[];
}

export interface UserInitialModel {
  modelId: number;
  tokens: number;
  counts: number;
  expires: string;
}

export interface UserModelDisplayDto {
  id: number;
  modelId: number;
  tokens: number;
  counts: number;
  expires: string;
  displayName: string;
  modelKeyName: string;
  modelProviderId: number;
}

export class UserModelDisplay implements UserModelDisplayDto {
  id: number;
  modelId: number;
  tokens: number;
  counts: number;
  expires: string;
  displayName: string;
  modelKeyName: string;
  modelProviderId: number;

  constructor(dto: UserModelDisplayDto) {
    this.id = dto.id;
    this.modelId = dto.modelId;
    this.tokens = dto.tokens;
    this.counts = dto.counts;
    this.expires = dto.expires;
    this.displayName = dto.displayName;
    this.modelKeyName = dto.modelKeyName;
    this.modelProviderId = dto.modelProviderId;
  }
}

export interface UserModelUserDto {
  id: number;
  userId: number;
  username: string;
  displayName: string;
  tokens: number;
  counts: number;
  expires: string;
}

export interface SimpleModelReferenceDto {
  id: number;
  name: string;
}

export interface ModelProviderInitialConfig {
  initialHost: string | null;
  initialSecret: string | null;
}

export interface ModelReferenceDto extends SimpleModelReferenceDto {
  modelProviderId: DBModelProvider;
  minTemperature: number;
  maxTemperature: number;
  allowVision: boolean;
  allowSearch: boolean;
  contextWindow: number;
  maxResponseTokens: number;
  promptTokenPrice1M: number;
  responseTokenPrice1M: number;
  rawPromptTokenPrice1M: number;
  rawResponseTokenPrice1M: number;
  currencyCode: string;
  exchangeRate: number;
}

export interface PossibleModelResult {
  deploymentName: string;
  existingModel: AdminModelDto | null;
}

export interface ValidateModelParams extends UpdateModelDto {
  // ValidateModelParams 使用完整的 UpdateModelDto
}

// 删除 ModelFastCreateParams，改用普通创建接口

export interface ErrorResult {
  isSuccess: boolean;
  errorMessage: string;
}

export interface StatisticsTimeParams {
  start?: string;
  end?: string;
  tz?: number;
}

export interface TokenStatisticsByDateResult {
  date: string;
  value: {
    inputTokens: number;
    outputTokens: number;
    reasoningTokens: number;
    totalTokens: number;
  };
}

export interface CostStatisticsByDateResult {
  date: string;
  value: {
    inputCost: number;
    outputCost: number;
  };
}

export interface ChatCountStatisticsByDateResult {
  date: string;
  value: number;
}

export interface ReorderRequest {
  sourceId: number;
  previousId: number | null; // 新位置的前一个元素
  nextId: number | null;     // 新位置的后一个元素
}
