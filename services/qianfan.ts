import { QianFanMessage } from '@/types/chat';

import {
  ParsedEvent,
  ReconnectInterval,
  createParser,
} from 'eventsource-parser';
import { ModelVersions } from '@/types/model';
import { ChatModels } from '@/dbs';

export const ModelEndpoint: { [key in ModelVersions]?: string } = {
  [ModelVersions.ERNIE_Bot_4]: 'completions_pro',
  [ModelVersions.ERNIE_Bot_8K]: 'ernie_bot_8k',
};

export interface SteamResult {
  text: string;
  usage: {
    prompt_tokens: number;
    completion_tokens: number;
    total_tokens: number;
  };
}

async function getAccessTokenAsync(
  apiHost: string,
  apiKey: string,
  apiSecret: string
) {
  let url = `${apiHost}/oauth/2.0/token?grant_type=client_credentials&client_id=${apiKey}&client_secret=${apiSecret}`;
  const resp = await fetch(url, {
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
  });
  const result = await resp.json();
  if (resp.status === 200) {
    return result?.access_token;
  } else {
    throw new Error(result);
  }
}

export const QianFanStream = async (
  chatModel: ChatModels,
  messages: QianFanMessage[],
  parameters: any
) => {
  const {
    modelVersion,
    apiConfig: { host, apiKey, secret },
  } = chatModel;
  const accessToken = await getAccessTokenAsync(host!, apiKey!, secret!);
  const url = `${host}/rpc/2.0/ai_custom/v1/wenxinworkshop/chat/${ModelEndpoint[modelVersion]}?access_token=${accessToken}`;
  const body = {
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    method: 'POST',
    body: JSON.stringify({
      model: modelVersion,
      messages: [...messages],
      stream: true,
      ...parameters,
    }),
  };
  const res = await fetch(url, body);
  const decoder = new TextDecoder();
  if (res.status !== 200) {
    let errors = {} as any;
    errors = await res.json();
    throw new Error(JSON.stringify(errors));
  }

  const stream = new ReadableStream({
    async start(controller) {
      const onParse = (event: ParsedEvent | ReconnectInterval) => {
        if (event.type === 'event') {
          const data = event.data;
          try {
            const json = JSON.parse(data);
            const text = json.result;
            controller.enqueue(
              JSON.stringify({
                text,
                usage: json.usage,
              })
            );
            if (json.is_end) {
              controller.close();
              return;
            }
          } catch (e) {
            controller.error(e);
          }
        }
      };

      const parser = createParser(onParse);
      for await (const chunk of res.body as any) {
        parser.feed(decoder.decode(chunk));
      }
    },
  });

  return stream;
};

export const Tokenizer = async (chatModel: ChatModels, messages: any[]) => {
  const {
    apiConfig: { host, apiKey, secret },
  } = chatModel;
  const accessToken = await getAccessTokenAsync(host!, apiKey!, secret!);
  let url = `${host}/rpc/2.0/ai_custom/v1/wenxinworkshop/tokenizer/erniebot?access_token=${accessToken}`;
  const body = {
    headers: {
      'Content-Type': 'application/json',
    },
    method: 'POST',
    body: JSON.stringify({
      messages: [...messages],
    }),
  };
  const res = await fetch(url, body);
  if (res.status === 200) {
    const result = await res.json();
    if (result?.error_code) {
      throw new Error(JSON.stringify(result));
    }
    return result.usage.total_tokens;
  } else {
    let errors = {} as any;
    errors = await res.json();
    throw new Error(JSON.stringify(errors));
  }
};
