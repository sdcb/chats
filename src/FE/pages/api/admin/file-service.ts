import { addAsterisk, checkKey } from '@/utils/common';
import { BadRequest } from '@/utils/error';

import { ChatsApiRequest } from '@/types/next-api';

import { FileServiceManager } from '@/managers';
import { apiHandler } from '@/middleware/api-handler';
import { FileServices } from '@prisma/client';

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
    const { select } = req.query;
    const servers = await FileServiceManager.findFileServices();
    let data = [];
    if (select) {
      data = servers
        .filter((x: FileServices) => x.enabled)
        .map((x: FileServices) => {
          return { id: x.id, name: x.name };
        });
    } else {
      data = servers.map((x: FileServices) => {
        const configs = JSON.parse(x?.configs || '{}');
        return {
          id: x.id,
          name: x.name,
          type: x.type,
          configs: {
            endpoint: configs.endpoint,
            bucketName: configs.bucketName,
            region: configs.region,
            accessKey: addAsterisk(configs.accessKey),
            storageFolderName: configs.storageFolderName,
            accessSecret: addAsterisk(configs.accessSecret),
          },
          enabled: x.enabled,
          createdAt: x.createdAt,
        };
      });
    }
    return data;
  } else if (req.method === 'PUT') {
    const { id, type, name, enabled, configs: configsJSON } = req.body;
    let fileServer = await FileServiceManager.findById(id);
    if (!fileServer) {
      throw new BadRequest('File service not found');
    }

    let configs = JSON.parse(configsJSON);
    configs.accessKey = checkKey(
      fileServer.configs.accessKey,
      configs.accessKey,
    );
    configs.accessSecret = checkKey(
      fileServer.configs.accessSecret,
      configs.accessSecret,
    );

    await FileServiceManager.updateFileServices({
      id,
      name,
      type,
      configs: JSON.stringify(configs),
      enabled,
    });
    await FileServiceManager.initFileServer(type, configs);
  } else {
    const { type, name, enabled, configs } = req.body;
    let isFound = await FileServiceManager.findByName(name);
    if (isFound) {
      throw new BadRequest('Name existed');
    }
    const fileServer = await FileServiceManager.createFileServices({
      type,
      name,
      enabled,
      configs,
    });
    await FileServiceManager.initFileServer(type, configs);
    return fileServer;
  }
};

export default apiHandler(handler);