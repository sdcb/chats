import { createFetchClient } from '@/hooks/createFetchClient';

import { getApiUrl } from '@/utils/common';
import { getUserSession } from '@/utils/user';

import {
  CommandStreamLine,
  ContainerSessionDto,
  CreateContainerSessionRequest,
  DirectoryListResponse,
  EnvironmentVariablesResponse,
  RunCommandRequest,
  SaveTextFileRequest,
  SaveUserEnvironmentVariablesRequest,
  TextFileResponse,
} from '@/types/containers';
import {
  ContainerResource,
  ContainerTemplate,
  CreateContainerResourceRequest,
} from '@/types/containers';

type FetchOptions = {
  signal?: AbortSignal;
};

async function* parseSseResponse<T>(res: Response): AsyncGenerator<T> {
  const data = res.body;
  if (!data) return;
  const reader = data.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      let boundaryIndex: number;
      while (
        (boundaryIndex = buffer.indexOf('\n\n')) >= 0 ||
        (boundaryIndex = buffer.indexOf('\r\n\r\n')) >= 0
      ) {
        const isDoubleCRLF = buffer[boundaryIndex] === '\r';
        const messageBlock = buffer.slice(0, boundaryIndex);
        buffer = buffer.slice(boundaryIndex + (isDoubleCRLF ? 4 : 2));

        if (!messageBlock.trim()) continue;

        const lines = messageBlock.split(/\r?\n/);
        const dataLines: string[] = [];

        for (const line of lines) {
          if (line.startsWith('data:')) {
            const content = line.slice(5).trimStart();
            dataLines.push(content);
          }
        }

        if (dataLines.length === 0) continue;
        const jsonString = dataLines.join('\n');

        try {
          yield JSON.parse(jsonString) as T;
        } catch (e) {
          console.error('Failed to parse SSE data:', jsonString, e);
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}

export const listChatContainers = (chatId: string) => {
  const fetchService = createFetchClient();
  return fetchService.get<ContainerSessionDto[]>(
    `/api/containers/for-chat/${encodeURIComponent(chatId)}`,
    { suppressDefaultToast: true },
  );
};

export const createChatContainer = (
  chatId: string,
  body: CreateContainerSessionRequest,
) => {
  const fetchService = createFetchClient();
  return fetchService.post<ContainerSessionDto>('/api/containers', {
    body: {
      name: body.name,
      isPermanent: body.isPermanent ?? false,
      templateId: body.templateId ?? 1,
      image: body.image,
      cpuCores: body.cpuCores,
      memoryBytes: body.memoryBytes,
      maxProcesses: body.maxProcesses,
      backendNetworkName: body.backendNetworkName,
      ownerChatId: chatId,
    },
    suppressDefaultToast: true,
  });
};

export const deleteChatContainer = (
  chatId: string,
  encryptedId: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.delete<void>(
    `/api/containers/${encodeURIComponent(encryptedId)}`,
    { suppressDefaultToast: true },
  );
};

export async function* streamRunContainerCommand(
  chatId: string,
  encryptedId: string,
  body: RunCommandRequest,
  options?: FetchOptions,
): AsyncGenerator<CommandStreamLine> {
  const res = await fetch(
    `${getApiUrl()}/api/containers/${encodeURIComponent(
      encryptedId,
    )}/run-command`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${getUserSession()}`,
      },
      body: JSON.stringify(body ?? {}),
      signal: options?.signal,
    },
  );

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(text || res.statusText);
  }

  yield* parseSseResponse<CommandStreamLine>(res);
}

export const listContainerDirectory = (
  chatId: string,
  encryptedId: string,
  path?: string | null,
) => {
  const fetchService = createFetchClient();
  return fetchService.get<DirectoryListResponse>(
    `/api/containers/${encodeURIComponent(encryptedId)}/files`,
    { params: { path: path ?? undefined }, suppressDefaultToast: true },
  );
};

export const uploadContainerFiles = async (
  chatId: string,
  encryptedId: string,
  dir: string,
  files: File[],
) => {
  const fetchService = createFetchClient();
  const form = new FormData();
  for (const f of files) {
    form.append('files', f);
  }

  await fetchService.post<void>(
    `/api/containers/${encodeURIComponent(
      encryptedId,
    )}/upload?dir=${encodeURIComponent(dir)}`,
    { body: form, suppressDefaultToast: true },
  );
};

export const getContainerFileDownloadUrl = (
  chatId: string,
  encryptedId: string,
  path: string,
): string => {
  const params = new URLSearchParams({
    path,
    token: getUserSession(),
  });
  return `${getApiUrl()}/api/containers/${encodeURIComponent(
    encryptedId,
  )}/download?${params.toString()}`;
};

export const deleteContainerFile = async (
  chatId: string,
  encryptedId: string,
  path: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.delete<void>(
    `/api/containers/${encodeURIComponent(encryptedId)}/file`,
    { body: { path }, suppressDefaultToast: true },
  );
};

export const mkdirContainerDirectory = async (
  chatId: string,
  encryptedId: string,
  path: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.post<void>(
    `/api/containers/${encodeURIComponent(encryptedId)}/mkdir`,
    { body: { path }, suppressDefaultToast: true },
  );
};

export const readContainerTextFile = (
  chatId: string,
  encryptedId: string,
  path: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.get<TextFileResponse>(
    `/api/containers/${encodeURIComponent(encryptedId)}/text-file`,
    { params: { path }, suppressDefaultToast: true },
  );
};

export const saveContainerTextFile = (
  chatId: string,
  encryptedId: string,
  body: SaveTextFileRequest,
) => {
  const fetchService = createFetchClient();
  return fetchService.put<void>(
    `/api/containers/${encodeURIComponent(encryptedId)}/text-file`,
    { body, suppressDefaultToast: true },
  );
};

export const getContainerEnvironmentVariables = (
  chatId: string,
  encryptedId: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.get<EnvironmentVariablesResponse>(
    `/api/containers/${encodeURIComponent(
      encryptedId,
    )}/environment-variables`,
    { suppressDefaultToast: true },
  );
};

export const saveContainerUserEnvironmentVariables = (
  chatId: string,
  encryptedId: string,
  body: SaveUserEnvironmentVariablesRequest,
) => {
  const fetchService = createFetchClient();
  return fetchService.put<void>(
    `/api/containers/${encodeURIComponent(
      encryptedId,
    )}/environment-variables`,
    { body, suppressDefaultToast: true },
  );
};

export const touchContainer = (chatId: string, encryptedId: string) => {
  const fetchService = createFetchClient();
  return fetchService.post<void>(
    `/api/containers/${encodeURIComponent(encryptedId)}/touch`,
    { suppressDefaultToast: true },
  );
};

export const listContainers = (includeDeleted = false) =>
  createFetchClient().get<ContainerResource[]>(
    `/api/containers?includeDeleted=${includeDeleted}`,
  );
export const listContainersForChat = (chatId: string) =>
  createFetchClient().get<ContainerResource[]>(
    `/api/containers/for-chat/${encodeURIComponent(chatId)}`,
  );
export const createContainer = (body: CreateContainerResourceRequest) =>
  createFetchClient().post<ContainerResource>('/api/containers', { body });
export const startContainer = (id: string) =>
  createFetchClient().post<void>(
    `/api/containers/${encodeURIComponent(id)}/start`,
  );
export const stopContainer = (id: string) =>
  createFetchClient().post<void>(
    `/api/containers/${encodeURIComponent(id)}/stop`,
  );
export const deleteContainer = (id: string) =>
  createFetchClient().delete<void>(`/api/containers/${encodeURIComponent(id)}`);
export const listContainerTemplates = () =>
  createFetchClient().get<ContainerTemplate[]>('/api/containers/templates');
export const updateContainer = (
  id: string,
  body: Partial<CreateContainerResourceRequest>,
) =>
  createFetchClient().patch<void>(`/api/containers/${encodeURIComponent(id)}`, {
    body,
  });
export const grantContainerToChat = (id: string, chatId: string) =>
  createFetchClient().post<void>(
    `/api/containers/${encodeURIComponent(id)}/chats/${encodeURIComponent(
      chatId,
    )}/grant`,
  );
export const revokeContainerFromChat = (id: string, chatId: string) =>
  createFetchClient().delete<void>(
    `/api/containers/${encodeURIComponent(id)}/chats/${encodeURIComponent(
      chatId,
    )}/grant`,
  );
export const listVolumes = () => createFetchClient().get<any[]>('/api/volumes');
export const createVolume = (body: {
  runtimeNodeId: number;
  name: string;
  backendVolumeId?: string | null;
  declaredBytes?: number | null;
}) => createFetchClient().post<any>('/api/volumes', { body });
export const mountVolume = (
  id: number,
  body: {
    encryptedContainerResourceId: string;
    containerPath: string;
    isReadOnly: boolean;
  },
) => createFetchClient().post<void>(`/api/volumes/${id}/mounts`, { body });
export const unmountVolume = (id: number, mountId: number) =>
  createFetchClient().delete<void>(`/api/volumes/${id}/mounts/${mountId}`);
export const deleteVolume = (id: number) =>
  createFetchClient().delete<void>(`/api/volumes/${id}`);


