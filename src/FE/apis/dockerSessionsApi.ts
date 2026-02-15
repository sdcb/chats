import { createFetchClient } from '@/hooks/createFetchClient';
import { getApiUrl } from '@/utils/common';
import { getUserSession } from '@/utils/user';
import {
  CommandStreamLine,
  CreateDockerSessionRequest,
  DefaultImageResponse,
  DirectoryListResponse,
  ImageListResponse,
  MemoryLimitResponse,
  NetworkModesResponse,
  ResourceLimitResponse,
  RunCommandRequest,
  SaveTextFileRequest,
  DockerSessionDto,
  TextFileResponse,
  EnvironmentVariablesResponse,
  SaveUserEnvironmentVariablesRequest,
} from '@/types/dockerSessions';

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

export const getChatDockerSessions = (chatId: string) => {
  const fetchService = createFetchClient();
  return fetchService.get<DockerSessionDto[]>(
    `/api/chat/${chatId}/docker-sessions`,
    { suppressDefaultToast: true },
  );
};

export const createChatDockerSession = (
  chatId: string,
  body: CreateDockerSessionRequest,
) => {
  const fetchService = createFetchClient();
  return fetchService.post<DockerSessionDto>(`/api/chat/${chatId}/docker-sessions`, {
    body,
    suppressDefaultToast: true,
  });
};

export const deleteChatDockerSession = (
  chatId: string,
  encryptedSessionId: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.delete<void>(
    `/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}`,
    { suppressDefaultToast: true },
  );
};

export const getDockerDefaultImage = () => {
  const fetchService = createFetchClient();
  return fetchService.get<DefaultImageResponse>(`/api/docker-sessions/default-image`, {
    suppressDefaultToast: true,
  });
};

export const getDockerImages = () => {
  const fetchService = createFetchClient();
  return fetchService.get<ImageListResponse>(`/api/docker-sessions/images`, {
    suppressDefaultToast: true,
  });
};

export const getDockerCpuLimits = () => {
  const fetchService = createFetchClient();
  return fetchService.get<ResourceLimitResponse>(`/api/docker-sessions/cpu-limits`, {
    suppressDefaultToast: true,
  });
};

export const getDockerMemoryLimits = () => {
  const fetchService = createFetchClient();
  return fetchService.get<MemoryLimitResponse>(`/api/docker-sessions/memory-limits`, {
    suppressDefaultToast: true,
  });
};

export const getDockerNetworkModes = () => {
  const fetchService = createFetchClient();
  return fetchService.get<NetworkModesResponse>(`/api/docker-sessions/network-modes`, {
    suppressDefaultToast: true,
  });
};

export async function* streamRunDockerCommand(
  chatId: string,
  encryptedSessionId: string,
  body: RunCommandRequest,
  options?: FetchOptions,
): AsyncGenerator<CommandStreamLine> {
  const res = await fetch(
    `${getApiUrl()}/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/run-command`,
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

export const listDockerDirectory = (
  chatId: string,
  encryptedSessionId: string,
  path?: string | null,
) => {
  const fetchService = createFetchClient();
  return fetchService.get<DirectoryListResponse>(
    `/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/files`,
    { params: { path: path ?? undefined }, suppressDefaultToast: true },
  );
};

export const uploadDockerFiles = async (
  chatId: string,
  encryptedSessionId: string,
  dir: string,
  files: File[],
) => {
  const fetchService = createFetchClient();
  const form = new FormData();
  for (const f of files) {
    form.append('files', f);
  }

  await fetchService.post<void>(
    `/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/upload?dir=${encodeURIComponent(dir)}`,
    { body: form, suppressDefaultToast: true },
  );
};

export const getDockerFileDownloadUrl = (
  chatId: string,
  encryptedSessionId: string,
  path: string,
): string => {
  const params = new URLSearchParams({
    path,
    token: getUserSession(),
  });
  return `${getApiUrl()}/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/download?${params.toString()}`;
};

export const deleteDockerFile = async (
  chatId: string,
  encryptedSessionId: string,
  path: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.delete<void>(
    `/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/file`,
    { body: { path }, suppressDefaultToast: true },
  );
};

export const mkdirDockerDir = async (
  chatId: string,
  encryptedSessionId: string,
  path: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.post<void>(
    `/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/mkdir`,
    { body: { path }, suppressDefaultToast: true },
  );
};

export const readDockerTextFile = (
  chatId: string,
  encryptedSessionId: string,
  path: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.get<TextFileResponse>(
    `/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/text-file`,
    { params: { path }, suppressDefaultToast: true },
  );
};

export const saveDockerTextFile = (
  chatId: string,
  encryptedSessionId: string,
  body: SaveTextFileRequest,
) => {
  const fetchService = createFetchClient();
  return fetchService.put<void>(
    `/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/text-file`,
    { body, suppressDefaultToast: true },
  );
};

export const getDockerEnvironmentVariables = (
  chatId: string,
  encryptedSessionId: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.get<EnvironmentVariablesResponse>(
    `/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/environment-variables`,
    { suppressDefaultToast: true },
  );
};

export const saveDockerUserEnvironmentVariables = (
  chatId: string,
  encryptedSessionId: string,
  body: SaveUserEnvironmentVariablesRequest,
) => {
  const fetchService = createFetchClient();
  return fetchService.put<void>(
    `/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/environment-variables`,
    { body, suppressDefaultToast: true },
  );
};

export const touchDockerSession = (
  chatId: string,
  encryptedSessionId: string,
) => {
  const fetchService = createFetchClient();
  return fetchService.post<void>(
    `/api/chat/${chatId}/docker-sessions/${encodeURIComponent(encryptedSessionId)}/touch`,
    { suppressDefaultToast: true },
  );
};
