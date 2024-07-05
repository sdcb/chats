import { addAsterisk, checkKey } from '@/utils/common';
import { BadRequest } from '@/utils/error';

import { ChatsApiRequest } from '@/types/next-api';

import { ModelKeysManager } from '@/managers';
import { apiHandler } from '@/middleware/api-handler';

export const config = {
  api: {
    bodyParser: {
      sizeLimit: '1mb',
    },
  },
  maxDuration: 5,
};

const handler = async (req: ChatsApiRequest) => {
  if (req.method === 'GET') {
    const data = await ModelKeysManager.findAll();
    return data.map((x) => {
      const configs = JSON.parse(x?.configs || '{}');
      return {
        id: x.id,
        type: x.type,
        name: x.name,
        configs: {
          host: configs.host,
          apiKey: addAsterisk(configs.apiKey),
          secret: addAsterisk(configs.secret),
          type: configs.type,
        },
        createdAt: x.createdAt,
      };
    });
  } else if (req.method === 'PUT') {
    const { id, name, configs: configsJSON } = req.body;
    let modelKey = await ModelKeysManager.findById(id);
    if (!modelKey) {
      throw new BadRequest('Key not found');
    }

    let configs = JSON.parse(configsJSON);
    configs.apiKey = checkKey(modelKey.configs.apiKey, configs.apiKey);
    configs.secret = checkKey(modelKey.configs.secret, configs.secret);

    await ModelKeysManager.update({
      id,
      name,
      configs: JSON.stringify(configs),
    });
  } else if (req.method === 'POST') {
    const { name, type, configs } = req.body;
    return await ModelKeysManager.create({
      type,
      name,
      configs,
    });
  } else if (req.method === 'DELETE') {
    const { id } = req.query as { id: string };
    await ModelKeysManager.delete(id);
  }
};

export default apiHandler(handler);