import { useFetch } from '@/hooks/useFetch';

import { calculateMessages } from '@/utils/message';

import { PostPromptParams, PutPromptParams } from '@/types/admin';
import { ChatMessage } from '@/types/chatMessage';
import { DBModelProvider, Model } from '@/types/model';
import { PageResult, Paging } from '@/types/page';
import { IdName, Prompt } from '@/types/prompt';
import {
  GetLoginProvidersResult,
  GetModelUsageResult,
  GetSiteInfoResult,
  GetUserBalanceResult,
  LoginConfigsResult,
  SingInParams,
  SingInResult,
  SmsType,
} from '@/types/user';

export interface GetChatsParams extends Paging {
  query?: string;
}

export interface ChatResult {
  id: string;
  title: string;
  chatModelId?: string;
  modelName: string;
  modelConfig: any;
  userModelConfig: any;
  isShared: boolean;
  modelProvider: DBModelProvider;
}

export interface PostChatParams {
  title: string;
  chatModelId?: string;
}

export interface PutChatParams {
  title?: string;
  isShared?: boolean;
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

export const changeUserPassword = (params: PostUserPassword) => {
  const fetchService = useFetch();
  return fetchService.put('/api/user/reset-password', {
    body: { ...params },
  });
};

export const getUserMessages = (chatId: string): Promise<ChatMessage[]> => {
  const fetchService = useFetch();
  return fetchService
    .get('/api/messages?chatId=' + chatId)
    .then((data: any) => {
      return calculateMessages(data) as any;
    });
};

export const getChatsByPaging = (
  params: GetChatsParams,
): Promise<PageResult<ChatResult[]>> => {
  const { query, page, pageSize } = params;
  const fetchService = useFetch();
  return fetchService.get(
    `/api/user/chats?page=${page}&pageSize=${pageSize}&query=${query || ''}`,
  );
};

export const getChat = (id: string): Promise<ChatResult> => {
  const fetchService = useFetch();
  return fetchService.get('/api/user/chats/' + id);
};

export const postChats = (params: PostChatParams): Promise<ChatResult> => {
  const fetchService = useFetch();
  return fetchService.post('/api/user/chats', { body: params });
};

export const putChats = (chatId: string, params: PutChatParams) => {
  const fetchService = useFetch();
  return fetchService.put(`/api/user/chats/${chatId}`, { body: params });
};

export const deleteChats = (id: string) => {
  const fetchService = useFetch();
  return fetchService.delete(`/api/user/chats/${id}`);
};

export const getCsrfToken = (): Promise<{ csrfToken: string }> => {
  const fetchServer = useFetch();
  return fetchServer.get('/api/auth/csrf');
};

export const singIn = (params: SingInParams): Promise<SingInResult> => {
  const fetchServer = useFetch();
  return fetchServer.post('/api/public/account-login', { body: params });
};

export const getUserModels = () => {
  const fetchServer = useFetch();
  return fetchServer.get<Model[]>('/api/models');
};

export const getUserBalance = () => {
  const fetchServer = useFetch();
  return fetchServer.get<GetUserBalanceResult>('/api/user/balance');
};

export const getUserBalanceOnly = () => {
  const fetchServer = useFetch();
  return fetchServer.get<number>('/api/user/balance-only');
};

export const getBalance7DaysUsage = () => {
  const fetchServer = useFetch();
  return fetchServer.get<GetBalance7DaysUsageResult[]>(
    `/api/user/7-days-usage?timezoneOffset=${new Date().getTimezoneOffset()}`,
  );
};

export const getLoginProvider = () => {
  const fetchServer = useFetch();
  return fetchServer.get<LoginConfigsResult[]>('/api/public/login-provider');
};

export const getUserPrompts = () => {
  const fetchServer = useFetch();
  return fetchServer.get<Prompt[]>('/api/prompts');
};

export const getUserPromptBrief = () => {
  const fetchServer = useFetch();
  return fetchServer.get<IdName[]>('/api/prompts/brief');
};

export const getUserPromptDetail = (id: string) => {
  const fetchServer = useFetch();
  return fetchServer.get<Prompt>('/api/prompts/' + id);
};

export const postUserPrompts = (params: PostPromptParams) => {
  const fetchServer = useFetch();
  return fetchServer.post('/api/prompts', { body: params });
};

export const putUserPrompts = (params: PutPromptParams) => {
  const fetchServer = useFetch();
  return fetchServer.put('/api/prompts', { body: params });
};

export const deleteUserPrompts = (id: string) => {
  const fetchServer = useFetch();
  return fetchServer.delete('/api/prompts?id=' + id);
};

const postSignCode = (
  phone: string,
  type: SmsType,
  invitationCode: string | null = null,
) => {
  const fetchServer = useFetch();
  return fetchServer.post('/api/public/sms', {
    body: { phone, type, invitationCode },
  });
};

export const sendLoginSmsCode = (phone: string) => {
  return postSignCode(phone, SmsType.SignIn);
};

export const sendRegisterSmsCode = (
  phone: string,
  invitationCode: string | undefined,
) => {
  return postSignCode(phone, SmsType.Register, invitationCode);
};

export const registerByPhone = (
  phone: string,
  smsCode: string,
  invitationCode: string,
): Promise<SingInResult> => {
  const fetchServer = useFetch();
  return fetchServer.post('/api/public/phone-register', {
    body: { phone, smsCode, invitationCode },
  });
};

export const signByPhone = (
  phone: string,
  smsCode: string,
): Promise<SingInResult> => {
  const fetchServer = useFetch();
  return fetchServer.post('/api/public/phone-login', {
    body: { phone, smsCode },
  });
};

export const getUserModelUsage = (modelId: string) => {
  const fetchServer = useFetch();
  return fetchServer.get<GetModelUsageResult>(
    '/api/user/model-usage?modelId=' + modelId,
  );
};

export const getLoginProviders = () => {
  const fetchServer = useFetch();
  return fetchServer.get<GetLoginProvidersResult[]>(
    '/api/public/login-providers',
  );
};

export const getSiteInfo = () => {
  const fetchServer = useFetch();
  return fetchServer.get<GetSiteInfoResult>('/api/public/siteInfo');
};

export const putUserChatModel = (chatId: string, modelId: string) => {
  const fetchServer = useFetch();
  return fetchServer.put('/api/user/chats/' + chatId, {
    body: { modelId },
  });
};

export const getUserApiKey = () => {
  const fetchServer = useFetch();
  return fetchServer.get<GetUserApiKeyResult[]>('/api/user/api-key');
};

export const postUserApiKey = () => {
  const fetchServer = useFetch();
  return fetchServer.post<GetUserApiKeyResult>('/api/user/api-key');
};

export const putUserApiKey = (id: number, body: any) => {
  const fetchServer = useFetch();
  return fetchServer.put<string>('/api/user/api-key/' + id, { body });
};

export const deleteUserApiKey = (id: number) => {
  const fetchServer = useFetch();
  return fetchServer.delete('/api/user/api-key/' + id);
};