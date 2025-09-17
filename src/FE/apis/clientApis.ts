import { useFetch } from '@/hooks/useFetch';

import { AdminModelDto, PostPromptParams } from '@/types/adminApis';
import {
  FileDef,
  MessageContentType,
  RequestContent,
  ResponseContent,
} from '@/types/chat';
import { IChatMessage } from '@/types/chatMessage';
import {
  ChatResult,
  ChatPresetReorderRequest,
  GetBalance7DaysUsageResult,
  GetChatPresetResult,
  GetChatShareResult,
  GetChatsParams,
  GetLoginProvidersResult,
  GetUsageParams,
  GetUsageResult,
  GetUsageStatResult,
  GetUserApiKeyResult,
  GetUserBalanceResult,
  GetUserChatGroupWithMessagesResult as GetUserChatGroupWithChatsResult,
  GetUserFilesResult,
  GetUserFilesParams,
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
  PutResponseMessageEditInPlaceParams,
  SingInParams,
  SingInResult,
  McpServerListItemDto,
  McpServerDetailsDto,
  UpdateMcpServerRequest,
  FetchToolsRequest,
  McpToolBasicInfo,
  McpServerListManagementItemDto,
  AssignUsersToMcpRequest,
  UnassignedUserDto,
  AssignedUserDetailsDto,
  AssignedUserNameDto,
} from '@/types/clientApis';
import { SiteInfoConfig } from '@/types/config';
import { IChatGroup } from '@/types/group';
import { PageResult } from '@/types/page';
import { Prompt, PromptSlim } from '@/types/prompt';
import { SmsType } from '@/types/user';
import { getTz } from '@/utils/date';

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
  const groupIdQuery = groupId ? `groupId=${groupId}` : '';
  return fetchService.get(
    `/api/user/chats?${groupIdQuery}&page=${page}&pageSize=${pageSize}&query=${query || ''}`,
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
    `/api/user/7-days-usage?timezoneOffset=${getTz()}`,
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
  return fetchServer.patch<IChatMessage>(
    `/api/messages/${params.messageId}/${params.contentId}/text-and-save-new`,
    {
      body: { c: params.c },
    },
  );
};
export const putResponseMessageEditInPlace = (
  params: PutResponseMessageEditInPlaceParams,
) => {
  const fetchServer = useFetch();
  return fetchServer.patch(
    `/api/messages/${params.messageId}/${params.contentId}/text`,
    {
      body: { c: params.c },
    },
  );
};

export const deleteMessage = (messageId: string, leafId: string | null) => {
  const fetchServer = useFetch();
  const encryptedLeafMessageId = leafId || '';
  return fetchServer.delete(
    `/api/messages/${messageId}?encryptedLeafMessageId=${encryptedLeafMessageId}&recursive=true`,
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

export const reorderChatPresets = (params: ChatPresetReorderRequest) => {
  const fetchServer = useFetch();
  return fetchServer.put('/api/chat-preset/reorder', {
    body: params,
  });
};

export const responseContentToRequest = (
  responseContent: ResponseContent[],
) => {
  const requestContent: RequestContent[] = responseContent
    .filter(
      (x) =>
        x.$type === MessageContentType.text ||
        x.$type === MessageContentType.fileId,
    )
    .map((x) => {
      if (x.$type === MessageContentType.text) {
        return { $type: MessageContentType.text, c: x.c };
      } else if (x.$type === MessageContentType.fileId) {
        return {
          $type: MessageContentType.fileId,
          c: typeof x.c === 'string' ? x.c : (x.c as FileDef).id,
        };
      } else {
        throw new Error('Invalid message content type');
      }
    });
  return requestContent;
};

export const getUsage = (params: GetUsageParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<PageResult<GetUsageResult[]>>('/api/usage', {
    params: params,
  });
};

export const getUsageStat = (params: GetUsageParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<GetUsageStatResult>('/api/usage/stat', {
    params: params,
  });
};

export const getUserFiles = (params: GetUserFilesParams) => {
  const fetchServer = useFetch();
  return fetchServer.get<PageResult<GetUserFilesResult[]>>('/api/file', {
    params: params,
  });
};

// MCP APIs
export const getMcpServers = (): Promise<McpServerListItemDto[]> => {
  const fetchService = useFetch();
  return fetchService.get('/api/mcp');
};

export const getMcpServersForManagement = (): Promise<McpServerListManagementItemDto[]> => {
  const fetchService = useFetch();
  return fetchService.get('/api/mcp/management');
};

export const getMcpServerDetails = (mcpId: number): Promise<McpServerDetailsDto> => {
  const fetchService = useFetch();
  return fetchService.get(`/api/mcp/${mcpId}`);
};

export const createMcpServer = (params: UpdateMcpServerRequest): Promise<McpServerDetailsDto> => {
  const fetchService = useFetch();
  return fetchService.post('/api/mcp', { body: params });
};

export const updateMcpServer = (mcpId: number, params: UpdateMcpServerRequest): Promise<McpServerDetailsDto> => {
  const fetchService = useFetch();
  return fetchService.put(`/api/mcp/${mcpId}`, { body: params });
};

export const deleteMcpServer = (mcpId: number) => {
  const fetchService = useFetch();
  return fetchService.delete(`/api/mcp/${mcpId}`);
};

export const fetchMcpTools = (params: FetchToolsRequest): Promise<McpToolBasicInfo[]> => {
  const fetchService = useFetch();
  return fetchService.post('/api/mcp/fetch-tools', { body: params });
};

// MCP用户分配相关API
export const assignUsersToMcp = (mcpId: number, params: AssignUsersToMcpRequest): Promise<void> => {
  const fetchService = useFetch();
  return fetchService.post(`/api/mcp/${mcpId}/assign-to-users`, { body: params });
};

export const getUnassignedUsers = (mcpId: number, search?: string, limit: number = 10): Promise<UnassignedUserDto[]> => {
  const fetchService = useFetch();
  const params = new URLSearchParams();
  if (search) {
    params.append('search', search);
  }
  params.append('limit', limit.toString());
  const queryString = params.toString();
  return fetchService.get(`/api/mcp/${mcpId}/get-unassigned-users${queryString ? `?${queryString}` : ''}`);
};

export const getAssignedUserDetails = (mcpId: number): Promise<AssignedUserDetailsDto[]> => {
  const fetchService = useFetch();
  return fetchService.get(`/api/mcp/${mcpId}/assigned-user-details`);
};

export const getAssignedUserNames = (mcpId: number): Promise<AssignedUserNameDto[]> => {
  const fetchService = useFetch();
  return fetchService.get(`/api/mcp/${mcpId}/assigned-user-names`);
};

