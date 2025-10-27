import { getApiUrl } from '@/utils/common';
import { getUserSession } from '@/utils/user';
import { RequestContent } from '@/types/chat';
import { SseResponseLine } from '@/types/chatMessage';

export type ChatApiError = Error & {
  status?: number;
  body?: any;
};

type FetchOptions = {
  signal?: AbortSignal;
};

async function streamPost(path: string, body: unknown, options?: FetchOptions): Promise<Response> {
  const res = await fetch(`${getApiUrl()}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${getUserSession()}`,
    },
    body: JSON.stringify(body ?? {}),
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
};

function postRegenerateAllAssistant(
  body: PostRegenerateAllAssistantBody,
  options?: FetchOptions,
): Promise<Response> {
  return streamPost('/api/chats/regenerate-all-assistant-message', body, options);
}

async function* parseSseResponse(res: Response): AsyncGenerator<SseResponseLine> {
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
        
        for (const line of lines) {
          if (line.startsWith('data:')) {
            // data: 后可能有空格，保留原始内容
            const content = line.slice(5).trimStart();
            dataLines.push(content);
          } else if (line.startsWith(':')) {
            // 忽略注释行
            continue;
          }
          // 其他字段（event:, id:, retry:）目前忽略，因为后端只用 data
        }
        
        if (dataLines.length === 0) continue;
        
        // 合并多行 data（SSE 规范：多个 data 行会用 \n 连接）
        const jsonString = dataLines.join('\n');
        
        try {
          const obj = JSON.parse(jsonString) as SseResponseLine;
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
): AsyncGenerator<SseResponseLine> {
  const res = await postGeneralChat(body, options);
  yield* parseSseResponse(res);
}

export async function* streamRegenerateAssistant(
  body: PostRegenerateAssistantBody,
  options?: FetchOptions,
): AsyncGenerator<SseResponseLine> {
  const res = await postRegenerateAssistant(body, options);
  yield* parseSseResponse(res);
}

export async function* streamRegenerateAllAssistant(
  body: PostRegenerateAllAssistantBody,
  options?: FetchOptions,
): AsyncGenerator<SseResponseLine> {
  const res = await postRegenerateAllAssistant(body, options);
  yield* parseSseResponse(res);
}
