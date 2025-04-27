import { useFetch } from '@/hooks/useFetch';

import {
  AdminChatsDto,
  AdminModelDto,
  ChatCountStatisticsByDateResult,
  CostStatisticsByDateResult,
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
  GetUserInitialConfigResult,
  GetUserMessageParams,
  GetUsersParams,
  GetUsersResult,
  ModelFastCreateParams,
  ModelProviderInitialConfig,
  ModelReferenceDto,
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
  PutUserModelParams,
  PutUserParams,
  SimpleModelReferenceDto,
  StatisticsTimeParams,
  TokenStatisticsByDateResult,
  UpdateModelDto,
  UserModelDisplay,
  UserModelDisplayDto,
  ValidateModelParams,
} from '@/types/adminApis';
import { GetChatShareResult, GetChatVersionResult } from '@/types/clientApis';
import { IKeyCount } from '@/types/common';
import { ChatModelFileConfig, DBModelProvider } from '@/types/model';
import { PageResult } from '@/types/page';

export const getModelsByUserId = async (
  userId: string,
): Promise<UserModelDisplay[]> => {
  const fetchService = useFetch();
  const data = await fetchService.get<UserModelDisplayDto[]>(
    `/api/admin/user-models/${userId}`,
  );
  return data.map((x) => new UserModelDisplay(x));
};

export const putUserModel = (params: PutUserModelParams): Promise<any> => {
  const fetchService = useFetch();
  return fetchService.put('/api/admin/user-models', {
    body: params,
  });
};

export const getModels = (all: boolean = true): Promise<AdminModelDto[]> => {
  const fetchService = useFetch();
  return fetchService.get('/api/admin/models?all=' + all);
};

export const putModels = (
  modelId: string,
  params: UpdateModelDto,
): Promise<any> => {
  const fetchService = useFetch();
  return fetchService.put(`/api/admin/models/${modelId}`, {
    body: params,
  });
};

export const deleteModels = (id: number): Promise<any> => {
  const fetchService = useFetch();
  return fetchService.delete(`/api/admin/models/${id}`);
};

export const postModels = (params: UpdateModelDto): Promise<any> => {
  const fetchService = useFetch();
  return fetchService.post('/api/admin/models', {
    body: params,
  });
};

export const getUsers = (
  params: GetUsersParams,
): Promise<PageResult<GetUsersResult[]>> => {
  const fetchService = useFetch();
  return fetchService.get(
    `/api/admin/users?page=${params.page}&pageSize=${params.pageSize}&query=${
      params?.query || ''
    }`,
  );
};

export const postUser = (params: PostUserParams) => {
  const fetchService = useFetch();
  return fetchService.post('/api/admin/users', {
    body: params,
  });
};

export const putUser = (params: PutUserParams) => {
  const fetchService = useFetch();
  return fetchService.put('/api/admin/users', {
    body: params,
  });
};

export const putUserBalance = (params: PutUserBalanceParams) => {
  const fetchService = useFetch();
  return fetchService.put('/api/admin/user-balances', {
    body: params,
  });
};

export const getMessages = (
  params: GetUserMessageParams,
): Promise<PageResult<AdminChatsDto[]>> => {
  const { query = null, page = 1, pageSize = 12 } = params;
  const fetchService = useFetch();
  return fetchService.get(
    `/api/admin/chats?page=${page}&pageSize=${pageSize}&query=${query}`,
  );
};

export const getMessageDetails = (
  chatId: string,
): Promise<GetMessageDetailsResult> => {
  const fetchService = useFetch();
  return fetchService.get(`/api/admin/message-details?chatId=${chatId}`);
};

export const getFileServices = (
  select: boolean = false,
): Promise<GetFileServicesResult[]> => {
  const fetchService = useFetch();
  return fetchService.get(
    '/api/admin/file-service?select=' + (!select ? '' : true),
  );
};

export const postFileService = (params: PostFileServicesParams) => {
  const fetchService = useFetch();
  return fetchService.post('/api/admin/file-service', {
    body: params,
  });
};

export const putFileService = (id: number, params: PostFileServicesParams) => {
  const fetchService = useFetch();
  return fetchService.put(`/api/admin/file-service/${id}`, {
    body: params,
  });
};

export const deleteFileService = (id: number) => {
  const fetchService = useFetch();
  return fetchService.delete(`/api/admin/file-service/${id}`);
};

export const getFileServiceTypeInitialConfig = (fileServiceTypeId: number) => {
  const fetchService = useFetch();
  return fetchService.get<string>(
    `/api/admin/file-service-type/${fileServiceTypeId}/initial-config`,
  );
};

export const getShareMessage = (
  chatId: string,
): Promise<GetMessageDetailsResult> => {
  const fetchService = useFetch();
  return fetchService.get(`/api/public/messages?chatId=${chatId}`);
};

export const getRequestLogs = (
  params: GetRequestLogsParams,
): Promise<PageResult<GetRequestLogsListResult[]>> => {
  const fetchService = useFetch();
  return fetchService.post(`/api/admin/request-logs`, { body: { ...params } });
};

export const getRequestLogDetails = (
  id: string,
): Promise<GetRequestLogsDetailsResult> => {
  const fetchService = useFetch();
  return fetchService.get(`/api/admin/request-logs?id=` + id);
};

export const getLoginServices = (): Promise<GetLoginServicesResult[]> => {
  const fetchService = useFetch();
  return fetchService.get('/api/admin/login-service');
};

export const postLoginService = (params: PostLoginServicesParams) => {
  const fetchService = useFetch();
  return fetchService.post('/api/admin/login-service', {
    body: params,
  });
};

export const putLoginService = (
  loginServiceId: number,
  params: PostLoginServicesParams,
) => {
  const fetchService = useFetch();
  return fetchService.put(`/api/admin/login-service/${loginServiceId}`, {
    body: params,
  });
};

export const getPayServices = (): Promise<GetPayServicesResult[]> => {
  const fetchService = useFetch();
  return fetchService.get('/api/admin/pay-service');
};

export const postPayService = (params: PostPayServicesParams) => {
  const fetchService = useFetch();
  return fetchService.post('/api/admin/pay-service', {
    body: params,
  });
};

export const putPayService = (params: PutPayServicesParams) => {
  const fetchService = useFetch();
  return fetchService.put('/api/admin/pay-service', {
    body: params,
  });
};

export const getModelKeys = async (): Promise<GetModelKeysResult[]> => {
  const fetchService = useFetch();
  const data = await fetchService.get<Object[]>('/api/admin/model-keys');
  return data.map((x) => new GetModelKeysResult(x));
};

export const postModelKeys = (params: PostModelKeysParams) => {
  const fetchService = useFetch();
  return fetchService.post<number>('/api/admin/model-keys', {
    body: params,
  });
};

export const putModelKeys = (id: number, params: PostModelKeysParams) => {
  const fetchService = useFetch();
  return fetchService.put(`/api/admin/model-keys/${id}`, {
    body: params,
  });
};

export const deleteModelKeys = (id: number) => {
  const fetchService = useFetch();
  return fetchService.delete(`/api/admin/model-keys/${id}`);
};

export const getUserInitialConfig = () => {
  const fetchServer = useFetch();
  return fetchServer.get<GetUserInitialConfigResult[]>(
    '/api/admin/user-config',
  );
};

export const postUserInitialConfig = (params: PostUserInitialConfigParams) => {
  const fetchServer = useFetch();
  return fetchServer.post('/api/admin/user-config', { body: params });
};

export const putUserInitialConfig = (params: PutUserInitialConfigParams) => {
  const fetchServer = useFetch();
  return fetchServer.put('/api/admin/user-config', { body: params });
};

export const deleteUserInitialConfig = (id: string) => {
  const fetchServer = useFetch();
  return fetchServer.delete('/api/admin/user-config?id=' + id);
};

export const getConfigs = () => {
  const fetchServer = useFetch();
  return fetchServer.get<GetConfigsResult[]>('/api/admin/global-configs');
};

export const postConfigs = (params: PostAndPutConfigParams) => {
  const fetchServer = useFetch();
  return fetchServer.post('/api/admin/global-configs', { body: params });
};

export const putConfigs = (params: PostAndPutConfigParams) => {
  const fetchServer = useFetch();
  return fetchServer.put('/api/admin/global-configs', { body: params });
};

export const deleteConfigs = (id: string) => {
  const fetchServer = useFetch();
  return fetchServer.delete('/api/admin/global-configs?id=' + id);
};

export const getInvitationCode = () => {
  const fetchServer = useFetch();
  return fetchServer.get<GetInvitationCodeResult[]>(
    '/api/admin/invitation-code',
  );
};

export const putInvitationCode = (params: PutInvitationCodeParams) => {
  const fetchServer = useFetch();
  return fetchServer.put('/api/admin/invitation-code', { body: params });
};

export const postInvitationCode = (params: PostInvitationCodeParams) => {
  const fetchServer = useFetch();
  return fetchServer.post('/api/admin/invitation-code', { body: params });
};

export const deleteInvitationCode = (id: string) => {
  const fetchServer = useFetch();
  return fetchServer.delete('/api/admin/invitation-code/' + id);
};

export const getAllModelProviderIds = () => {
  const fetchServer = useFetch();
  return fetchServer.get<DBModelProvider[]>('/api/model-provider');
};

export const getModelProviderInitialConfig = (
  modelProviderId: DBModelProvider,
) => {
  const fetchServer = useFetch();
  return fetchServer.get<ModelProviderInitialConfig>(
    `/api/model-provider/${modelProviderId}/initial-config`,
  );
};

export const getModelProviderModels = (modelProviderId: DBModelProvider) => {
  const fetchServer = useFetch();
  return fetchServer.get<SimpleModelReferenceDto[]>(
    `/api/model-provider/${modelProviderId}/models`,
  );
};

export const getModelReference = (modelReferenceId: number) => {
  const fetchServer = useFetch();
  return fetchServer.get<ModelReferenceDto>(
    `/api/model-reference/${modelReferenceId}`,
  );
};

export const getModelKeyPossibleModels = (modelKeyId: number) => {
  const fetchServer = useFetch();
  return fetchServer.get<PossibleModelResult[]>(
    `/api/admin/model-keys/${modelKeyId}/possible-models`,
  );
};

export const postModelValidate = (params: ValidateModelParams) => {
  const fetchServer = useFetch();
  return fetchServer.post<ErrorResult>(`/api/admin/models/validate`, {
    body: params,
  });
};

export const postModelFastCreate = (params: ModelFastCreateParams) => {
  const fetchServer = useFetch();
  return fetchServer.post<ErrorResult>(`/api/admin/models/fast-create`, {
    body: params,
  });
};

export const getAdminMessage = (chatId: string) => {
  const fetchServer = useFetch();
  return fetchServer.get<GetChatShareResult>(
    `/api/admin/message-details?chatId=${chatId}`,
  );
};

export const postChatsVersion = () => {
  const fetchServer = useFetch();
  return fetchServer.post<GetChatVersionResult>(`/api/version/check-update`);
};

export const defaultFileConfig: ChatModelFileConfig = {
  count: 5,
  maxSize: 10240,
};

export const getEnabledUserCount = () => {
  const fetchServer = useFetch();
  return fetchServer.get<number>('/api/admin/statistics/enabled-user-count');
};

export const getEnabledModelCount = () => {
  const fetchServer = useFetch();
  return fetchServer.get<number>('/api/admin/statistics/enabled-model-count');
};

export const getTokensDuring = (params: StatisticsTimeParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<number>('/api/admin/statistics/tokens-during', {
    params: params,
  });
};

export const getCostDuring = (params: StatisticsTimeParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<number>('/api/admin/statistics/cost-during', {
    params: params,
  });
};

export const getModelProviderStatistics = (params: StatisticsTimeParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<object>(
    '/api/admin/statistics/model-provider-statistics',
    {
      params: params,
    },
  );
};

export const getModelStatistics = (params: StatisticsTimeParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<IKeyCount[]>(
    '/api/admin/statistics/model-statistics',
    {
      params: params,
    },
  );
};

export const getModelKeyStatistics = (params: StatisticsTimeParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<IKeyCount[]>(
    '/api/admin/statistics/model-key-statistics',
    { params: params },
  );
};

export const getSourceStatistics = (params: StatisticsTimeParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<IKeyCount[]>(
    '/api/admin/statistics/source-statistics',
    { params: params },
  );
};

export const getTokenStatisticsByDate = (params: StatisticsTimeParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<TokenStatisticsByDateResult[]>(
    '/api/admin/statistics/token-statistics-by-date',
    { params: params },
  );
};

export const getCostStatisticsByDate = (params: StatisticsTimeParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<CostStatisticsByDateResult[]>(
    '/api/admin/statistics/cost-statistics-by-date',
    { params: params },
  );
};

export const getChatCountStatisticsByDate = (params: StatisticsTimeParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<ChatCountStatisticsByDateResult[]>(
    '/api/admin/statistics/chat-count-by-date',
    { params: params },
  );
};
