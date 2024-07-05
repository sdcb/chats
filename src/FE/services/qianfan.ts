import { QianFanMessage } from '@/types/chat';
import { ChatModels } from '@/types/chatModel';

import {
  ParsedEvent,
  ReconnectInterval,
  createParser,
} from 'eventsource-parser';

async function getAccessTokenAsync(
  apiHost: string,
  apiKey: string,
  apiSecret: string,
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
  parameters: any,
) => {
  const {
    apiConfig: { host, apiKey, secret },
    modelConfig: { model },
  } = chatModel;
  const systemMessage = messages.shift();
  const accessToken = await getAccessTokenAsync(host!, apiKey!, secret!);
  const url = `${host}/rpc/2.0/ai_custom/v1/wenxinworkshop/chat/${model}?access_token=${accessToken}`;
  const body = {
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    method: 'POST',
    body: JSON.stringify({
      system: systemMessage!.content,
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
            const { prompt_tokens, completion_tokens, total_tokens } =
              json?.usage || {};
            controller.enqueue(
              JSON.stringify({
                text,
                usage: {
                  inputTokens: prompt_tokens,
                  outputTokens: completion_tokens,
                  totalTokens: total_tokens,
                },
              }),
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