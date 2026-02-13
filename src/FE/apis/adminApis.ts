import { createFetchClient } from '@/hooks/createFetchClient';

import {
  AdminChatsDto,
  AdminModelDto,
  AddUserModelParams,
  BatchUserModelsParams,
  BatchUserModelsByProviderParams,
  BatchUserModelsByKeyParams,
  BatchUserModelsByModelParams,
  ChatCountStatisticsByDateResult,
  CostStatisticsByDateResult,
  EditUserModelParams,
  ErrorResult,
  GetConfigsResult,
  GetFileServicesResult,
  GetInvitationCodeResult,
  GetLoginServicesResult,
  GetMessageDetailsResult,
  GetModelKeysResult,
  GetPayServicesResult,
  GetRequestLogsDetailsResult,
  GetRequestLogsListResult,
  GetRequestLogsParams,
  KeycloakAttemptLog,
  GetUserInitialConfigResult,
  GetUserMessageParams,
  GetUsersParams,
  GetUsersResult,
  UserModelPermissionUserDto,
  UserModelProviderDto,
  UserModelOperationResponse,
  UserModelKeyDto,
  UserModelPermissionModelDto,
  ModelUserPermissionDto,
  ModelProviderDto,
  ModelProviderInitialConfig,
  PasswordAttemptLog,
  PossibleModelResult,
  PostAndPutConfigParams,
  PostFileServicesParams,
  PostInvitationCodeParams,
  PostLoginServicesParams,
  PostModelKeysParams,
  PostPayServicesParams,
  PostUserInitialConfigParams,
  PostUserParams,
  PutInvitationCodeParams,
  PutPayServicesParams,
  PutUserBalanceParams,
  PutUserInitialConfigParams,
  PutUserParams,
  SecurityLogQueryParams,
  SecurityLogExportParams,
  ReorderRequest,
  StatisticsTimeParams,
  TokenStatisticsByDateResult,
  UpdateModelDto,
  UserModelDisplay,
  UserModelDisplayDto,
  UserModelUserDto,
  ValidateModelParams,
  SmsAttemptLog,
} from '@/types/adminApis';
import { GetChatShareResult, GetChatVersionResult } from '@/types/clientApis';
import { IKeyCount } from '@/types/common';
import { ChatModelFileConfig, DBModelProvider } from '@/types/model';
import { PageResult } from '@/types/page';

export const getModelsByUserId = async (
  userId: string,
): Promise<UserModelDisplay[]> => {
  const fetchService = createFetchClient();
  const data = await fetchService.get<UserModelDisplayDto[]>(
    `/api/admin/user-models/user/${userId}`,
  );
  return data.map((x) => new UserModelDisplay(x));
};

export const getUsersByModelId = async (
  modelId: number,
): Promise<UserModelUserDto[]> => {
  const fetchService = createFetchClient();
  return fetchService.get<UserModelUserDto[]>(
    `/api/admin/user-models/model/${modelId}`,
  );
};

export const getUserUnassignedModels = async (
  userId: string,
): Promise<AdminModelDto[]> => {
  const fetchService = createFetchClient();
  return fetchService.get<AdminModelDto[]>(
    `/api/admin/user-models/user/${userId}/unassigned`,
  );
};

export const addUserModel = async (params: AddUserModelParams): Promise<UserModelOperationResponse> => {
  const fetchService = createFetchClient();
  return fetchService.post<UserModelOperationResponse>('/api/admin/user-models', {
    body: params,
  });
};

export const batchAddUserModelsByProvider = async (params: BatchUserModelsByProviderParams): Promise<UserModelOperationResponse> => {
  const fetchService = createFetchClient();
  return fetchService.post<UserModelOperationResponse>('/api/admin/user-models/batch-by-provider', {
    body: params,
  });
};

export const batchDeleteUserModelsByProvider = async (params: BatchUserModelsByProviderParams): Promise<UserModelOperationResponse> => {
  const fetchService = createFetchClient();
  return fetchService.post<UserModelOperationResponse>('/api/admin/user-models/batch-delete-by-provider', {
    body: params,
  });
};

export const batchAddUserModelsByKey = async (params: BatchUserModelsByKeyParams): Promise<UserModelOperationResponse> => {
  const fetchService = createFetchClient();
  return fetchService.post<UserModelOperationResponse>('/api/admin/user-models/batch-by-key', {
    body: params,
  });
};

export const batchDeleteUserModelsByKey = async (params: BatchUserModelsByKeyParams): Promise<UserModelOperationResponse> => {
  const fetchService = createFetchClient();
  return fetchService.post<UserModelOperationResponse>('/api/admin/user-models/batch-delete-by-key', {
    body: params,
  });
};

export const editUserModel = (userModelId: number, params: EditUserModelParams): Promise<UserModelOperationResponse> => {
  const fetchService = createFetchClient();
  return fetchService.put<UserModelOperationResponse>(`/api/admin/user-models/${userModelId}`, {
    body: params,
  });
};

export const deleteUserModel = (userModelId: number): Promise<UserModelOperationResponse> => {
  const fetchService = createFetchClient();
  return fetchService.delete<UserModelOperationResponse>(`/api/admin/user-models/${userModelId}`);
};

export const getModels = (all: boolean = true): Promise<AdminModelDto[]> => {
  const fetchService = createFetchClient();
  return fetchService.get('/api/admin/models?all=' + all);
};

export const putModels = (
  modelId: string,
  params: UpdateModelDto,
): Promise<any> => {
  const fetchService = createFetchClient();
  return fetchService.put(`/api/admin/models/${modelId}`, {
    body: params,
  });
};

export const deleteModels = (id: number): Promise<any> => {
  const fetchService = createFetchClient();
  return fetchService.delete(`/api/admin/models/${id}`);
};

export const postModels = (params: UpdateModelDto): Promise<any> => {
  const fetchService = createFetchClient();
  return fetchService.post('/api/admin/models', {
    body: params,
  });
};

export const getUsers = (
  params: GetUsersParams,
): Promise<PageResult<GetUsersResult[]>> => {
  const fetchService = createFetchClient();
  return fetchService.get(
    `/api/admin/users?page=${params.page}&pageSize=${params.pageSize}&query=${
      params?.query || ''
    }`,
  );
};

export const getUsersForPermission = (
  params: GetUsersParams,
): Promise<PageResult<UserModelPermissionUserDto[]>> => {
  const fetchService = createFetchClient();
  return fetchService.get(
    `/api/admin/user-models/users?page=${params.page}&pageSize=${params.pageSize}&query=${
      params?.query || ''
    }`,
  );
};

export const getModelProvidersForUser = async (
  userId: string,
): Promise<UserModelProviderDto[]> => {
  const fetchService = createFetchClient();
  return fetchService.get<UserModelProviderDto[]>(
    `/api/admin/user-models/user/${userId}/providers`,
  );
};

export const getModelKeysByProviderForUser = async (
  userId: string,
  providerId: number,
): Promise<UserModelKeyDto[]> => {
  const fetchService = createFetchClient();
  return fetchService.get<UserModelKeyDto[]>(
    `/api/admin/user-models/user/${userId}/provider/${providerId}/keys`,
  );
};

export const getModelsByKeyForUser = async (
  userId: string,
  keyId: number,
): Promise<UserModelPermissionModelDto[]> => {
  const fetchService = createFetchClient();
  return fetchService.get<UserModelPermissionModelDto[]>(
    `/api/admin/user-models/user/${userId}/key/${keyId}/models`,
  );
};

export const getUsersByModel = async (
  modelId: number,
  params: { page: number; pageSize: number; query?: string },
): Promise<PageResult<ModelUserPermissionDto[]>> => {
  const fetchService = createFetchClient();
  return fetchService.get<PageResult<ModelUserPermissionDto[]>>(
    `/api/admin/user-models/model/${modelId}/users?page=${params.page}&pageSize=${params.pageSize}&query=${params.query || ''}`,
  );
};

export const batchAddUserModelsByModel = async (params: BatchUserModelsByModelParams): Promise<void> => {
  const fetchService = createFetchClient();
  return fetchService.post<void>('/api/admin/user-models/batch-by-model', {
    body: params,
  });
};

export const batchDeleteUserModelsByModel = async (params: BatchUserModelsByModelParams): Promise<void> => {
  const fetchService = createFetchClient();
  return fetchService.post<void>('/api/admin/user-models/batch-delete-by-model', {
    body: params,
  });
};

export const postUser = (params: PostUserParams) => {
  const fetchService = createFetchClient();
  return fetchService.post('/api/admin/users', {
    body: params,
  });
};

export const putUser = (params: PutUserParams) => {
  const fetchService = createFetchClient();
  return fetchService.put('/api/admin/users', {
    body: params,
  });
};

export const putUserBalance = (params: PutUserBalanceParams) => {
  const fetchService = createFetchClient();
  return fetchService.put('/api/admin/user-balances', {
    body: params,
  });
};

export const getMessages = (
  params: GetUserMessageParams,
): Promise<PageResult<AdminChatsDto[]>> => {
  const { user, content, page = 1, pageSize = 12 } = params;
  const fetchService = createFetchClient();
  
  // 构建查询参数
  const queryParams = new URLSearchParams();
  queryParams.append('page', page.toString());
  queryParams.append('pageSize', pageSize.toString());
  if (user) queryParams.append('user', user);
  if (content) queryParams.append('content', content);
  
  return fetchService.get(
    `/api/admin/chats?${queryParams.toString()}`,
  );
};

export const getMessageDetails = (
  chatId: string,
): Promise<GetMessageDetailsResult> => {
  const fetchService = createFetchClient();
  return fetchService.get(`/api/admin/message-details?chatId=${chatId}`);
};

export const getFileServices = (
  select: boolean = false,
): Promise<GetFileServicesResult[]> => {
  const fetchService = createFetchClient();
  return fetchService.get(
    '/api/admin/file-service?select=' + (!select ? '' : true),
  );
};

export const postFileService = (params: PostFileServicesParams) => {
  const fetchService = createFetchClient();
  return fetchService.post('/api/admin/file-service', {
    body: params,
  });
};

export const putFileService = (id: number, params: PostFileServicesParams) => {
  const fetchService = createFetchClient();
  return fetchService.put(`/api/admin/file-service/${id}`, {
    body: params,
  });
};

export const deleteFileService = (id: number) => {
  const fetchService = createFetchClient();
  return fetchService.delete(`/api/admin/file-service/${id}`);
};

export const validateFileService = (params: PostFileServicesParams) => {
  const fetchService = createFetchClient();
  return fetchService.post<void>(
    '/api/admin/file-service/validate',
    { body: params, suppressDefaultToast: true },
  );
};

export const getFileServiceTypeInitialConfig = (fileServiceTypeId: number) => {
  const fetchService = createFetchClient();
  return fetchService.get<string>(
    `/api/admin/file-service-type/${fileServiceTypeId}/initial-config`,
  );
};

export const getShareMessage = (
  chatId: string,
): Promise<GetMessageDetailsResult> => {
  const fetchService = createFetchClient();
  return fetchService.get(`/api/public/messages?chatId=${chatId}`);
};

export const getRequestLogs = (
  params: GetRequestLogsParams,
): Promise<PageResult<GetRequestLogsListResult[]>> => {
  const fetchService = createFetchClient();
  return fetchService.post(`/api/admin/request-logs`, { body: { ...params } });
};

export const getRequestLogDetails = (
  id: string,
): Promise<GetRequestLogsDetailsResult> => {
  const fetchService = createFetchClient();
  return fetchService.get(`/api/admin/request-logs?id=` + id);
};

export const getLoginServices = (): Promise<GetLoginServicesResult[]> => {
  const fetchService = createFetchClient();
  return fetchService.get('/api/admin/login-service');
};

export const postLoginService = (params: PostLoginServicesParams) => {
  const fetchService = createFetchClient();
  return fetchService.post('/api/admin/login-service', {
    body: params,
  });
};

export const putLoginService = (
  loginServiceId: number,
  params: PostLoginServicesParams,
) => {
  const fetchService = createFetchClient();
  return fetchService.put(`/api/admin/login-service/${loginServiceId}`, {
    body: params,
  });
};

export const getPayServices = (): Promise<GetPayServicesResult[]> => {
  const fetchService = createFetchClient();
  return fetchService.get('/api/admin/pay-service');
};

export const postPayService = (params: PostPayServicesParams) => {
  const fetchService = createFetchClient();
  return fetchService.post('/api/admin/pay-service', {
    body: params,
  });
};

export const putPayService = (params: PutPayServicesParams) => {
  const fetchService = createFetchClient();
  return fetchService.put('/api/admin/pay-service', {
    body: params,
  });
};

export const getModelKeys = async (): Promise<GetModelKeysResult[]> => {
  const fetchService = createFetchClient();
  const data = await fetchService.get<Object[]>('/api/admin/model-keys');
  return data.map((x) => new GetModelKeysResult(x));
};

export const getModelProviders = async (): Promise<ModelProviderDto[]> => {
  const fetchService = createFetchClient();
  return fetchService.get('/api/admin/model-providers');
};

export const getModelKeysByProvider = async (providerId: number): Promise<GetModelKeysResult[]> => {
  const fetchService = createFetchClient();
  const data = await fetchService.get<Object[]>(`/api/admin/model-providers/${providerId}/model-keys`);
  return data.map((x) => new GetModelKeysResult(x));
};

export const getModelsByKey = async (modelKeyId: number): Promise<AdminModelDto[]> => {
  const fetchService = createFetchClient();
  return fetchService.get(`/api/admin/model-providers/model-keys/${modelKeyId}/models`);
};

export const postModelKeys = (params: PostModelKeysParams) => {
  const fetchService = createFetchClient();
  return fetchService.post<number>('/api/admin/model-keys', {
    body: params,
  });
};

export const putModelKeys = (id: number, params: PostModelKeysParams) => {
  const fetchService = createFetchClient();
  return fetchService.put(`/api/admin/model-keys/${id}`, {
    body: params,
  });
};

export const deleteModelKeys = (id: number) => {
  const fetchService = createFetchClient();
  return fetchService.delete(`/api/admin/model-keys/${id}`);
};

export const reorderModelProviders = (params: ReorderRequest) => {
  const fetchService = createFetchClient();
  return fetchService.put('/api/admin/model-providers/reorder', {
    body: params,
  });
};

export const reorderModelKeys = (params: ReorderRequest) => {
  const fetchService = createFetchClient();
  return fetchService.put('/api/admin/model-keys/reorder', {
    body: params,
  });
};

export const reorderModels = (params: ReorderRequest) => {
  const fetchService = createFetchClient();
  return fetchService.put('/api/admin/models/reorder', {
    body: params,
  });
};

export const getUserInitialConfig = () => {
  const fetchServer = createFetchClient();
  return fetchServer.get<GetUserInitialConfigResult[]>(
    '/api/admin/user-config',
  );
};

export const postUserInitialConfig = (params: PostUserInitialConfigParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.post('/api/admin/user-config', { body: params });
};

export const putUserInitialConfig = (params: PutUserInitialConfigParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.put('/api/admin/user-config', { body: params });
};

export const deleteUserInitialConfig = (id: string) => {
  const fetchServer = createFetchClient();
  return fetchServer.delete('/api/admin/user-config/' + id);
};

export const getConfigs = () => {
  const fetchServer = createFetchClient();
  return fetchServer.get<GetConfigsResult[]>('/api/admin/global-configs');
};

export const postConfigs = (params: PostAndPutConfigParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.post('/api/admin/global-configs', { body: params });
};

export const putConfigs = (params: PostAndPutConfigParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.put('/api/admin/global-configs', { body: params });
};

export const deleteConfigs = (id: string) => {
  const fetchServer = createFetchClient();
  return fetchServer.delete('/api/admin/global-configs?id=' + id);
};

export const getInvitationCode = () => {
  const fetchServer = createFetchClient();
  return fetchServer.get<GetInvitationCodeResult[]>(
    '/api/admin/invitation-code',
  );
};

export const putInvitationCode = (params: PutInvitationCodeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.put('/api/admin/invitation-code', { body: params });
};

export const postInvitationCode = (params: PostInvitationCodeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.post('/api/admin/invitation-code', { body: params });
};

export const deleteInvitationCode = (id: string) => {
  const fetchServer = createFetchClient();
  return fetchServer.delete('/api/admin/invitation-code/' + id);
};

export const getAllModelProviderIds = () => {
  const fetchServer = createFetchClient();
  return fetchServer.get<DBModelProvider[]>('/api/model-provider');
};

export const getModelProviderInitialConfig = (
  modelProviderId: DBModelProvider,
) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<ModelProviderInitialConfig>(
    `/api/model-provider/${modelProviderId}/initial-config`,
  );
};

// getModelProviderModels 已删除 - 不再需要
// getModelReference 已删除 - 不再需要

export const getModelKeyPossibleModels = (modelKeyId: number) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<PossibleModelResult[]>(
    `/api/admin/model-keys/${modelKeyId}/possible-models`,
    { suppressDefaultToast: true },
  );
};

export const postModelValidate = (params: ValidateModelParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.post<ErrorResult>(`/api/admin/models/validate`, {
    body: params,
  });
};

// postModelFastCreate 已删除 - 使用 postModels 代替

export const getAdminMessage = (chatId: string) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<GetChatShareResult>(
    `/api/admin/message-details?chatId=${chatId}`,
  );
};

export const postChatsVersion = () => {
  const fetchServer = createFetchClient();
  return fetchServer.post<GetChatVersionResult>(`/api/version/check-update`);
};

export const defaultFileConfig: ChatModelFileConfig = {
  count: 5,
  maxSize: 10240,
};

export const getEnabledUserCount = () => {
  const fetchServer = createFetchClient();
  return fetchServer.get<number>('/api/admin/statistics/enabled-user-count');
};

export const getEnabledModelCount = () => {
  const fetchServer = createFetchClient();
  return fetchServer.get<number>('/api/admin/statistics/enabled-model-count');
};

export const getTokensDuring = (params: StatisticsTimeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<number>('/api/admin/statistics/tokens-during', {
    params: params,
  });
};

export const getCostDuring = (params: StatisticsTimeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<number>('/api/admin/statistics/cost-during', {
    params: params,
  });
};

export const getModelProviderStatistics = (params: StatisticsTimeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<object>(
    '/api/admin/statistics/model-provider-statistics',
    {
      params: params,
    },
  );
};

export const getModelStatistics = (params: StatisticsTimeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<IKeyCount[]>(
    '/api/admin/statistics/model-statistics',
    {
      params: params,
    },
  );
};

export const getModelKeyStatistics = (params: StatisticsTimeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<IKeyCount[]>(
    '/api/admin/statistics/model-key-statistics',
    { params: params },
  );
};

export const getSourceStatistics = (params: StatisticsTimeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<IKeyCount[]>(
    '/api/admin/statistics/source-statistics',
    { params: params },
  );
};

export const getTokenStatisticsByDate = (params: StatisticsTimeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<TokenStatisticsByDateResult[]>(
    '/api/admin/statistics/token-statistics-by-date',
    { params: params },
  );
};

export const getCostStatisticsByDate = (params: StatisticsTimeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<CostStatisticsByDateResult[]>(
    '/api/admin/statistics/cost-statistics-by-date',
    { params: params },
  );
};

export const getChatCountStatisticsByDate = (params: StatisticsTimeParams) => {
  const fetchServer = createFetchClient();
  return fetchServer.get<ChatCountStatisticsByDateResult[]>(
    '/api/admin/statistics/chat-count-by-date',
    { params: params },
  );
};

const mapSecurityLogQueryParams = (params: SecurityLogQueryParams) => ({
  page: params.page,
  pageSize: params.pageSize,
  start: params.start,
  end: params.end,
  username: params.username,
  success: params.success,
});

const mapSecurityLogExportParams = (params: SecurityLogExportParams) => ({
  start: params.start,
  end: params.end,
  username: params.username,
  success: params.success,
});

export const getPasswordAttempts = (
  params: SecurityLogQueryParams,
): Promise<PageResult<PasswordAttemptLog[]>> => {
  const fetchServer = createFetchClient();
  return fetchServer.get('/api/admin/security-logs/password-attempts', {
    params: mapSecurityLogQueryParams(params),
  });
};

export const exportPasswordAttempts = (
  params: SecurityLogExportParams,
): Promise<Blob | null> => {
  const fetchServer = createFetchClient();
  return fetchServer.get('/api/admin/security-logs/password-attempts/export', {
    params: mapSecurityLogExportParams(params),
  });
};

export const clearPasswordAttempts = (
  params: SecurityLogExportParams,
): Promise<number> => {
  const fetchServer = createFetchClient();
  return fetchServer.delete('/api/admin/security-logs/password-attempts', {
    body: params,
  });
};

export const getKeycloakAttempts = (
  params: SecurityLogQueryParams,
): Promise<PageResult<KeycloakAttemptLog[]>> => {
  const fetchServer = createFetchClient();
  return fetchServer.get('/api/admin/security-logs/keycloak-attempts', {
    params: mapSecurityLogQueryParams(params),
  });
};

export const exportKeycloakAttempts = (
  params: SecurityLogExportParams,
): Promise<Blob | null> => {
  const fetchServer = createFetchClient();
  return fetchServer.get('/api/admin/security-logs/keycloak-attempts/export', {
    params: mapSecurityLogExportParams(params),
  });
};

export const clearKeycloakAttempts = (
  params: SecurityLogExportParams,
): Promise<number> => {
  const fetchServer = createFetchClient();
  return fetchServer.delete('/api/admin/security-logs/keycloak-attempts', {
    body: params,
  });
};

export const getSmsAttempts = (
  params: SecurityLogQueryParams,
): Promise<PageResult<SmsAttemptLog[]>> => {
  const fetchServer = createFetchClient();
  return fetchServer.get('/api/admin/security-logs/sms-attempts', {
    params: mapSecurityLogQueryParams(params),
  });
};

export const exportSmsAttempts = (
  params: SecurityLogExportParams,
): Promise<Blob | null> => {
  const fetchServer = createFetchClient();
  return fetchServer.get('/api/admin/security-logs/sms-attempts/export', {
    params: mapSecurityLogExportParams(params),
  });
};

export const clearSmsAttempts = (
  params: SecurityLogExportParams,
): Promise<number> => {
  const fetchServer = createFetchClient();
  return fetchServer.delete('/api/admin/security-logs/sms-attempts', {
    body: params,
  });
};
