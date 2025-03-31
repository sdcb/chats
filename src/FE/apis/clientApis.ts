import { useFetch } from '@/hooks/useFetch';

import { AdminModelDto, PostPromptParams } from '@/types/adminApis';
import { IChatMessage } from '@/types/chatMessage';
import {
  ChatResult,
  GetBalance7DaysUsageResult,
  GetChatPresetResult,
  GetChatShareResult,
  GetChatsParams,
  GetLoginProvidersResult,
  GetUserApiKeyResult,
  GetUserBalanceResult,
  GetUserChatGroupWithMessagesResult as GetUserChatGroupWithChatsResult,
  LoginConfigsResult,
  ModelUsageDto,
  PostChatGroupParams,
  PostChatParams,
  PostUserChatShareResult,
  PostUserChatSpanParams,
  PostUserChatSpanResult,
  PostUserPassword,
  PutChatGroupParams,
  PutChatParams,
  PutChatPresetParams,
  PutChatSpanParams,
  PutMoveChatGroupParams,
  PutResponseMessageEditAndSaveNewParams,
  PutResponseMessageEditAndSaveNewResult,
  PutResponseMessageEditInPlaceParams,
  SingInParams,
  SingInResult,
} from '@/types/clientApis';
import { SiteInfoConfig } from '@/types/config';
import { IChatGroup } from '@/types/group';
import { PageResult } from '@/types/page';
import { Prompt, PromptSlim } from '@/types/prompt';
import { SmsType } from '@/types/user';

export const changeUserPassword = (params: PostUserPassword) => {
  const fetchService = useFetch();
  return fetchService.put('/api/user/reset-password', {
    body: { ...params },
  });
};

export const getUserMessages = (chatId: string): Promise<IChatMessage[]> => {
  const fetchService = useFetch();
  return fetchService.get(`/api/messages/${chatId}`);
};

export const getChatsByPaging = (
  params: GetChatsParams,
): Promise<PageResult<ChatResult[]>> => {
  const { groupId, query, page, pageSize } = params;
  const fetchService = useFetch();
  return fetchService.get(
    `/api/user/chats?groupId=${
      groupId || ''
    }&page=${page}&pageSize=${pageSize}&query=${query || ''}`,
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

export const stopChat = (id: string) => {
  const fetchService = useFetch();
  return fetchService.post(`/api/chats/stop/${id}`);
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
  return fetchServer.get<AdminModelDto[]>(`/api/models`);
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
  return fetchServer.get<PromptSlim[]>('/api/prompts/brief');
};

export const getUserPromptDetail = (id: number) => {
  const fetchServer = useFetch();
  return fetchServer.get<Prompt>('/api/prompts/' + id);
};

export const getDefaultPrompt = () => {
  const fetchServer = useFetch();
  return fetchServer.get<Prompt>('/api/prompts/default');
};

export const postUserPrompts = (params: PostPromptParams) => {
  const fetchServer = useFetch();
  return fetchServer.post<Prompt>('/api/prompts', { body: params });
};

export const putUserPrompts = (promptId: number, params: PostPromptParams) => {
  const fetchServer = useFetch();
  return fetchServer.put(`/api/prompts/${promptId}`, { body: params });
};

export const deleteUserPrompts = (id: number) => {
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

export const getLoginProviders = () => {
  const fetchServer = useFetch();
  return fetchServer.get<GetLoginProvidersResult[]>(
    '/api/public/login-providers',
  );
};

export const getSiteInfo = () => {
  const fetchServer = useFetch();
  return fetchServer.get<SiteInfoConfig>('/api/public/siteInfo');
};

export const putUserChatModel = (chatId: string, modelId: number) => {
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

export const getModelUsage = (modelId: number) => {
  const fetchServer = useFetch();
  return fetchServer.get<ModelUsageDto>('/api/models/' + modelId + '/usage');
};

export const postUserChatSpan = (
  chatId: string,
  params?: PostUserChatSpanParams,
) => {
  const fetchServer = useFetch();
  return fetchServer.post<PostUserChatSpanResult>(`/api/chat/${chatId}/span`, {
    body: params,
  });
};

export const putUserChatSpan = (
  chatId: string,
  spanId: number,
  params?: PostUserChatSpanParams,
) => {
  const fetchServer = useFetch();
  return fetchServer.put<PostUserChatSpanResult>(
    `/api/chat/${chatId}/span/${spanId}`,
    {
      body: params,
    },
  );
};

export const switchUserChatSpanModel = (
  chatId: string,
  spanId: number,
  modelId: number,
) => {
  const fetchServer = useFetch();
  return fetchServer.post<PostUserChatSpanResult>(
    `/api/chat/${chatId}/span/${spanId}/switch-model/${modelId}`,
    {},
  );
};

export const deleteUserChatSpan = (chatId: string, spanId: number) => {
  const fetchServer = useFetch();
  return fetchServer.delete(`/api/chat/${chatId}/span/${spanId}`);
};

export const getUserChatGroupWithMessages = (
  params: GetChatsParams,
): Promise<GetUserChatGroupWithChatsResult[]> => {
  const { query, page, pageSize } = params;
  const fetchServer = useFetch();
  return fetchServer.get(
    `/api/chat/group/with-chats?page=${page}&pageSize=${pageSize}&query=${
      query || ''
    }`,
  );
};

export const postChatGroup = (
  params: PostChatGroupParams,
): Promise<IChatGroup> => {
  const fetchServer = useFetch();
  return fetchServer.post(`/api/chat/group`, {
    body: params,
  });
};

export const putChatGroup = (params: PutChatGroupParams) => {
  const fetchServer = useFetch();
  return fetchServer.put(`/api/chat/group/${params.id}`, {
    body: params,
  });
};

export const deleteChatGroup = (id: string) => {
  const fetchServer = useFetch();
  return fetchServer.delete(`/api/chat/group/${id}`);
};

export const putMoveChatGroup = (params: PutMoveChatGroupParams) => {
  const fetchServer = useFetch();
  return fetchServer.put(`/api/chat/group/move`, {
    body: params,
  });
};

export const putMessageReactionUp = (messageId: string) => {
  const fetchServer = useFetch();
  return fetchServer.put(`/api/messages/${messageId}/reaction/up`);
};
export const putMessageReactionDown = (messageId: string) => {
  const fetchServer = useFetch();
  return fetchServer.put(`/api/messages/${messageId}/reaction/down`);
};
export const putMessageReactionClear = (messageId: string) => {
  const fetchServer = useFetch();
  return fetchServer.put(`/api/messages/${messageId}/reaction/clear`);
};

export const postUserChatShare = (
  encryptedChatId: string,
  validBefore: string,
) => {
  const fetchServer = useFetch();
  return fetchServer.post<PostUserChatShareResult>(
    `/api/user/chats/${encryptedChatId}/share?validBefore=${validBefore}`,
  );
};

export const getUserChatShare = (encryptedChatId: string) => {
  const fetchServer = useFetch();
  return fetchServer.get<PostUserChatShareResult[]>(
    `/api/user/chats/${encryptedChatId}/share`,
  );
};

export const deleteUserChatShare = (encryptedChatId: string) => {
  const fetchServer = useFetch();
  return fetchServer.delete(`/api/user/chats/${encryptedChatId}/share`);
};

export const putChatShare = (
  encryptedChatShareId: string,
  validBefore: string,
) => {
  const fetchServer = useFetch();
  return fetchServer.put(`/api/public/chat-share`, {
    body: { encryptedChatShareId, validBefore },
  });
};

export const getChatShare = (encryptedChatShareId: string) => {
  const fetchServer = useFetch();
  return fetchServer.get<GetChatShareResult>(
    `/api/public/chat-share/${encryptedChatShareId}`,
  );
};

export const putResponseMessageEditAndSaveNew = (
  params: PutResponseMessageEditAndSaveNewParams,
) => {
  const fetchServer = useFetch();
  return fetchServer.put<PutResponseMessageEditAndSaveNewResult>(
    `/api/messages/${params.messageId}/edit-and-save-new`,
    {
      body: params.content,
    },
  );
};
export const putResponseMessageEditInPlace = (
  params: PutResponseMessageEditInPlaceParams,
) => {
  const fetchServer = useFetch();
  return fetchServer.put(`/api/messages/${params.messageId}/edit-in-place`, {
    body: params.content,
  });
};

export const deleteMessage = (messageId: string, leafId: string) => {
  const fetchServer = useFetch();
  return fetchServer.delete(
    `/api/messages/${messageId}?encryptedLeafMessageId=${leafId}&recursive=true`,
  );
};

export const postChatEnableSpan = (spanId: number, encryptedChatId: string) => {
  const fetchServer = useFetch();
  return fetchServer.post(`/api/chat/${encryptedChatId}/span/${spanId}/enable`);
};

export const postChatDisableSpan = (
  spanId: number,
  encryptedChatId: string,
) => {
  const fetchServer = useFetch();
  return fetchServer.post(
    `/api/chat/${encryptedChatId}/span/${spanId}/disable`,
  );
};

export const putChatSpan = (
  spanId: number,
  encryptedChatId: string,
  params: PutChatSpanParams,
) => {
  const fetchServer = useFetch();
  return fetchServer.put(`/api/chat/${encryptedChatId}/span/${spanId}`, {
    body: params,
  });
};

export const getChatPreset = () => {
  const fetchServer = useFetch();
  return fetchServer.get<GetChatPresetResult[]>(`/api/chat-preset`);
};

export const postChatPreset = (params: PutChatPresetParams) => {
  const fetchServer = useFetch();
  return fetchServer.post<GetChatPresetResult>(`/api/chat-preset`, {
    body: params,
  });
};

export const putChatPreset = (id: string, params: PutChatPresetParams) => {
  const fetchServer = useFetch();
  return fetchServer.put<GetChatPresetResult>(`/api/chat-preset/${id}`, {
    body: params,
  });
};

export const deleteChatPreset = (id: string) => {
  const fetchServer = useFetch();
  return fetchServer.delete<GetChatPresetResult>(`/api/chat-preset/${id}`);
};

export const postCloneChatPreset = (id: string) => {
  const fetchServer = useFetch();
  return fetchServer.post(`/api/chat-preset/${id}/clone`);
};

export const postApplyChatPreset = (chatId: string, presetId: string) => {
  const fetchServer = useFetch();
  return fetchServer.post(`/api/chat/${chatId}/span/apply-preset/${presetId}`);
};
