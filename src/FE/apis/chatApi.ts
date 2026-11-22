import { getApiUrl } from '@/utils/common';
import { getUserSession } from '@/utils/user';
import { RequestContent } from '@/types/chat';
import {
  ActiveChatRunDto,
  AskQuestionAnswer,
  ChatRunMode,
  SseEvent,
  SseResponseKind,
  SteeringDto,
} from '@/types/chatMessage';

export type ChatApiError = Error & {
  status?: number;
  body?: any;
};

type FetchOptions = {
  signal?: AbortSignal;
};

async function streamPost(path: string, body: unknown, options?: FetchOptions): Promise<Response> {
  return apiFetch(path, 'POST', body, options);
}

async function apiFetch(
  path: string,
  method: 'GET' | 'POST' | 'DELETE',
  body?: unknown,
  options?: FetchOptions,
): Promise<Response> {
  const res = await fetch(`${getApiUrl()}${path}`, {
    method,
    headers: {
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      Authorization: `Bearer ${getUserSession()}`,
    },
    body: body !== undefined ? JSON.stringify(body ?? {}) : undefined,
    signal: options?.signal,
  });

  if (!res.ok) {
    let parsed: any = undefined;
    let message = res.statusText;
    try {
      const ct = res.headers.get('content-type');
      if (ct && ct.includes('application/json')) {
        parsed = await res.json();
        message = parsed?.message || parsed?.errMessage || message;
      } else {
        parsed = await res.text();
        if (typeof parsed === 'string' && parsed.trim().length > 0) {
          message = parsed;
        }
      }
    } catch (e) {
      console.error('Failed to parse error response:', e);
    }

    const err: ChatApiError = Object.assign(new Error(message), {
      name: 'ChatApiError',
      status: res.status,
      body: parsed,
    });
    throw err;
  }

  return res;
}

export type PostGeneralChatBody = {
  chatId: string;
  timezoneOffset: number;
  parentAssistantMessageId: string | null;
  userMessage: RequestContent[];
  mode?: ChatRunMode;
};

function postGeneralChat(body: PostGeneralChatBody, options?: FetchOptions): Promise<Response> {
  return streamPost('/api/chats/general', body, options);
}

export type PostRegenerateAssistantBody = {
  chatId: string;
  spanId: number;
  modelId: number;
  parentUserMessageId: string | null;
  timezoneOffset: number;
  mode?: ChatRunMode;
};

function postRegenerateAssistant(
  body: PostRegenerateAssistantBody,
  options?: FetchOptions,
): Promise<Response> {
  return streamPost('/api/chats/regenerate-assistant-message', body, options);
}

export type PostRegenerateAllAssistantBody = {
  chatId: string;
  modelId: number;
  parentUserMessageId: string;
  timezoneOffset: number;
  mode?: ChatRunMode;
};

function postRegenerateAllAssistant(
  body: PostRegenerateAllAssistantBody,
  options?: FetchOptions,
): Promise<Response> {
  return streamPost('/api/chats/regenerate-all-assistant-message', body, options);
}

async function* parseSseResponse(res: Response): AsyncGenerator<SseEvent> {
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
      
      // 处理所有完整的消息（以双换行符分隔）
      let boundaryIndex: number;
      while ((boundaryIndex = buffer.indexOf('\n\n')) >= 0 || (boundaryIndex = buffer.indexOf('\r\n\r\n')) >= 0) {
        const isDoubleCRLF = buffer[boundaryIndex] === '\r';
        const messageBlock = buffer.slice(0, boundaryIndex);
        buffer = buffer.slice(boundaryIndex + (isDoubleCRLF ? 4 : 2));
        
        if (!messageBlock.trim()) continue; // 跳过空块
        
        // 收集所有 data: 行
        const lines = messageBlock.split(/\r?\n/);
        let dataLines: string[] = [];
        let seq: number | undefined = undefined;
        
        for (const line of lines) {
          if (line.startsWith('data:')) {
            // data: 后可能有空格，保留原始内容
            const content = line.slice(5).trimStart();
            dataLines.push(content);
          } else if (line.startsWith('id:')) {
            const parsed = Number.parseInt(line.slice(3).trim(), 10);
            if (Number.isFinite(parsed)) seq = parsed;
          } else if (line.startsWith(':')) {
            // 忽略注释行（keep-alive）
            continue;
          }
          // 其他字段（event:, retry:）目前忽略
        }
        
        if (dataLines.length === 0) continue;
        
        // 合并多行 data（SSE 规范：多个 data 行会用 \n 连接）
        const jsonString = dataLines.join('\n');
        
        try {
          const obj = JSON.parse(jsonString) as SseEvent;
          if (seq !== undefined) obj.seq = seq;
          yield obj;
        } catch (e) {
          console.error('Failed to parse SSE data:', jsonString, e);
        }
      }
    }
    
    // 流结束后，处理剩余的不完整数据
    if (buffer.trim()) {
      console.warn('SSE stream ended with incomplete data in buffer:', buffer);
    }
  } finally {
    reader.releaseLock();
  }
}

export async function* streamGeneralChat(
  body: PostGeneralChatBody,
  options?: FetchOptions,
): AsyncGenerator<SseEvent> {
  const res = await postGeneralChat(body, options);
  yield* withRunResume(parseSseResponse(res), options);
}

export async function* streamRegenerateAssistant(
  body: PostRegenerateAssistantBody,
  options?: FetchOptions,
): AsyncGenerator<SseEvent> {
  const res = await postRegenerateAssistant(body, options);
  yield* withRunResume(parseSseResponse(res), options);
}

export async function* streamRegenerateAllAssistant(
  body: PostRegenerateAllAssistantBody,
  options?: FetchOptions,
): AsyncGenerator<SseEvent> {
  const res = await postRegenerateAllAssistant(body, options);
  yield* withRunResume(parseSseResponse(res), options);
}

export type PostAnswerQuestionBody = {
  chatId: string;
  turnId: string;
  toolCallId: string;
  answers: AskQuestionAnswer[];
  skipped: boolean;
  timezoneOffset: number;
  mode?: ChatRunMode;
};

export async function* streamAnswerQuestion(
  body: PostAnswerQuestionBody,
  options?: FetchOptions,
): AsyncGenerator<SseEvent> {
  const res = await streamPost('/api/chats/answer-question', body, options);
  yield* withRunResume(parseSseResponse(res), options);
}

const MAX_RESUME_ATTEMPTS = 20;

/**
 * Wraps a run's SSE stream: if the connection drops before the run finishes, reconnect to
 * GET /api/chat-runs/{runId}/events?after={lastSeq} and continue where it left off.
 */
async function* withRunResume(
  source: AsyncGenerator<SseEvent>,
  options?: FetchOptions,
): AsyncGenerator<SseEvent> {
  let runId: string | null = null;
  let lastSeq = 0;
  let current: AsyncGenerator<SseEvent> = source;
  for (let attempt = 0; ; attempt++) {
    try {
      for await (const line of current) {
        if (line.seq !== undefined) {
          if (line.seq <= lastSeq) continue; // already delivered
          lastSeq = line.seq;
        }
        if (line.k === SseResponseKind.RunStarted) runId = line.r;
        yield line;
      }
      return; // server closed the stream normally: the run has finished
    } catch (e) {
      if (!runId || options?.signal?.aborted || attempt >= MAX_RESUME_ATTEMPTS) throw e;
      if ((e as ChatApiError)?.status === 404) throw e;
      console.warn('Run stream interrupted, resuming from', lastSeq, e);
      await new Promise((resolve) => setTimeout(resolve, Math.min(1000 * (attempt + 1), 5000)));
      current = streamRunEventsRaw(runId, lastSeq, options);
    }
  }
}

async function* streamRunEventsRaw(
  runId: string,
  after: number,
  options?: FetchOptions,
): AsyncGenerator<SseEvent> {
  const res = await apiFetch(
    `/api/chat-runs/${encodeURIComponent(runId)}/events?after=${after}`,
    'GET',
    undefined,
    options,
  );
  yield* parseSseResponse(res);
}

/** Replays a running run from the beginning (after a page refresh) and follows it until it ends. */
export async function* streamRunEvents(
  runId: string,
  after = 0,
  options?: FetchOptions,
): AsyncGenerator<SseEvent> {
  async function* withStart(): AsyncGenerator<SseEvent> {
    yield { k: SseResponseKind.RunStarted, r: runId };
    yield* streamRunEventsRaw(runId, after, options);
  }
  yield* withRunResume(withStart(), options);
}

async function jsonOrNull<T>(res: Response): Promise<T | null> {
  const text = await res.text();
  return text ? (JSON.parse(text) as T) : null;
}

export async function getActiveChatRun(chatId: string): Promise<ActiveChatRunDto | null> {
  const res = await apiFetch(`/api/chat-runs/active?chatId=${encodeURIComponent(chatId)}`, 'GET');
  return jsonOrNull<ActiveChatRunDto>(res);
}

export async function stopChatRun(runId: string): Promise<void> {
  await apiFetch(`/api/chat-runs/${encodeURIComponent(runId)}/stop`, 'POST', {});
}

export async function steerChatRun(runId: string, content: string): Promise<SteeringDto> {
  const res = await apiFetch(`/api/chat-runs/${encodeURIComponent(runId)}/steer`, 'POST', { content });
  return (await res.json()) as SteeringDto;
}

export async function cancelChatRunSteer(runId: string, steeringId: number): Promise<void> {
  await apiFetch(`/api/chat-runs/${encodeURIComponent(runId)}/steer/${steeringId}`, 'DELETE');
}
