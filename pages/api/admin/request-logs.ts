import { ChatsApiRequest, ChatsApiResponse } from '@/types/next-api';

import { RequestLogsManager } from '@/managers';
import { apiHandler } from '@/middleware/api-handler';

export const config = {
  api: {
    bodyParser: {
      sizeLimit: '1mb',
    },
  },
  maxDuration: 5,
};

const handler = async (req: ChatsApiRequest, res: ChatsApiResponse) => {
  if (req.method === 'GET') {
    const { id } = req.query as {
      id: string;
    };
    const requestLog = await RequestLogsManager.findByRequestLogsId(id);
    return requestLog;
  } else if (req.method === 'POST') {
    const requestLogs = await RequestLogsManager.findRequestLogs(req.body);
    const rows = requestLogs.rows.map((x) => {
      return {
        id: x.id,
        ip: x.ip,
        method: x.method,
        url: x.url,
        statusCode: x.statusCode,
        username: x.user?.username,
        createdAt: x.createdAt,
      };
    });
    return { rows, count: requestLogs.count };
  }
};

export default apiHandler(handler);
