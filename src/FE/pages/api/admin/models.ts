import { BadRequest } from '@/utils/error';
import { conversionModelPriceToSave } from '@/utils/model';

import { ChatsApiRequest } from '@/types/next-api';

import { ChatModelManager } from '@/managers';
import { apiHandler } from '@/middleware/api-handler';
import { ChatModels } from '@prisma/client';

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
    const { all } = req.query;
    const models = await ChatModelManager.findModels(!!all);
    const data = models.map((x: ChatModels) => {
      return {
        rank: x.rank,
        modelId: x.id,
        modelProvider: x.modelProvider,
        modelVersion: x.modelVersion,
        name: x.name,
        enabled: x.enabled,
        remarks: x.remarks,
        modelKeysId: x.modelKeysId,
        fileServiceId: x.fileServiceId,
        fileConfig: x.fileConfig,
        modelConfig: x.modelConfig,
        priceConfig: JSON.parse(x.priceConfig || '{}'),
      };
    });
    return data;
  } else if (req.method === 'PUT') {
    const {
      modelId,
      name,
      enabled,
      modelKeysId,
      fileServiceId,
      fileConfig,
      modelConfig,
      priceConfig,
      remarks,
    } = req.body;
    const model = await ChatModelManager.findModelById(modelId);
    if (!model) {
      throw new BadRequest('Model is not Found');
    }

    const data = await ChatModelManager.updateModel({
      id: modelId,
      name,
      enabled,
      modelKeysId,
      fileServiceId,
      fileConfig,
      modelConfig,
      priceConfig: conversionModelPriceToSave(priceConfig),
      remarks,
    });
    return data;
  } else if (req.method === 'POST') {
    const {
      modelProvider,
      modelVersion,
      name,
      enabled,
      modelKeysId,
      fileServiceId,
      priceConfig,
      modelConfig,
      fileConfig,
      remarks,
    } = req.body;

    const data = await ChatModelManager.createModel({
      modelProvider,
      modelVersion,
      name,
      enabled,
      modelKeysId,
      fileServiceId,
      fileConfig,
      modelConfig,
      priceConfig: conversionModelPriceToSave(priceConfig),
      remarks,
    });
    return data;
  } else if (req.method === 'DELETE') {
    const { id } = req.query as { id: string };
    const model = await ChatModelManager.findModelById(id);
    if (model) {
      await ChatModelManager.deleteModelById(id);
    } else throw new BadRequest('Model is not Found!');
  }
};

export default apiHandler(handler);