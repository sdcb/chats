import { BaseError, Unauthorized } from '@/utils/error';
import { getSession } from '@/utils/session';
import { replacePassword } from '@/utils/user';

import { UserRole } from '@/types/admin';
import { GlobalConfigKeys, LogsConfig } from '@/types/config';
import { ChatsApiRequest, ChatsApiResponse } from '@/types/next-api';
import { Session } from '@/types/session';

import { ConfigsManager } from '@/managers';
import { CreateRequestLogs, RequestLogsManager } from '@/managers/requestLogs';
import { IncomingHttpHeaders } from 'http';
import requestIp from 'request-ip';

const modelApis = ['/api/chats'];

const publicApis = [
  '/api/public/messages',
  '/api/public/notify',
  '/api/public/login-provider',
  '/api/public/sms',
  '/api/public/account-login',
  '/api/public/phone-login',
  '/api/public/phone-register',
  '/api/public/invitation-code',
];
const adminApis = '/api/admin/';

async function authMiddleware(request: ChatsApiRequest) {
  const requestUrl = request.url!;
  let session: Session | null = null;
  if (publicApis.filter((url) => requestUrl.includes(url)).length === 0) {
    session = await getSession(request.headers);
    if (!session) {
      throw new Unauthorized();
    }
  }
  if (requestUrl.includes(adminApis)) {
    if (session?.role !== UserRole.admin) {
      throw new Unauthorized();
    }
  }
  request.session = session!;
}

function getHeadersLog(headers: IncomingHttpHeaders) {
  return JSON.stringify({
    ua: headers['sec-ch-ua'],
    platform: headers['sec-ch-platform'],
    mobile: headers['sec-ch-ua-mobile'] === '?0' ? 'Mobile' : 'Desktop',
    referer: headers['referer'],
  });
}

export function apiHandler(handler: any) {
  return async (request: ChatsApiRequest, response: ChatsApiResponse) => {
    const requestUrl = request.url!;
    let logs = {
      ip: requestIp.getClientIp(request),
      url: requestUrl,

      method: request.method!,
      statusCode: 200,
      headers: getHeadersLog(request.headers),
      request: replacePassword(JSON.stringify(request.body)),
      requestTime: new Date().getTime().toString(),
    } as CreateRequestLogs;
    const logConfig = await ConfigsManager.get<LogsConfig>(
      GlobalConfigKeys.logs,
    );
    try {
      await authMiddleware(request);
      logs.userId = request?.session?.userId;
      const data = await handler(request, response);
      if (modelApis.includes(request.url!)) {
        return response.write(Buffer.from(data || ''));
      }
      if (logConfig.success) {
        logs.response = JSON.stringify(data);
        logs.responseTime = new Date().getTime().toString();
        await RequestLogsManager.create(logs);
      }
      return data
        ? response.status(200).json(data)
        : response.status(200).end();
    } catch (error) {
      logs.response = `${error}`;
      logs.responseTime = new Date().getTime().toString();
      if (error instanceof BaseError && error.statusCode !== 500) {
        logs.statusCode = error.statusCode;
        logConfig.error && (await RequestLogsManager.create(logs));
        return response
          .status(error.statusCode)
          .json({ message: error.message });
      } else {
        console.log('ERROR: \n', error);
        logs.statusCode = 500;
        logConfig.error && (await RequestLogsManager.create(logs));
        return response
          .status(500)
          .json({ message: 'An unexpected error occurred' });
      }
    }
  };
}