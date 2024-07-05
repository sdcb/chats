import { InternalServerError } from '@/utils/error';

import {
  ChatModelConfig,
  ChatModelFileConfig,
  ChatModelPriceConfig,
  ModelProviders,
  ModelType,
  ModelVersions,
} from '@/types/model';

import prisma from '@/prisma/prisma';

interface CreateModel {
  name: string;
  modelProvider: ModelProviders;
  modelVersion: ModelVersions;
  enabled?: boolean;
  modelKeysId?: string;
  fileServiceId?: string;
  fileConfig?: string;
  modelConfig?: string;
  priceConfig?: string;
  remarks?: string;
}
interface UpdateModel {
  id: string;
  name: string;
  enabled?: boolean;
  modelKeysId?: string;
  fileServiceId?: string;
  fileConfig?: string;
  modelConfig: string;
  priceConfig: string;
  remarks: string;
}

export class ChatModelManager {
  static async findModels(findAll: boolean = false) {
    const where = { enabled: true };
    return await prisma.chatModels.findMany({
      where: findAll ? {} : where,
      orderBy: [{ enabled: 'desc' }, { rank: 'asc' }, { createdAt: 'asc' }],
    });
  }

  static async findModelById(id: string) {
    const model = await prisma.chatModels.findUnique({
      include: { ModelKeys: true },
      where: { id },
    });
    if (!model) {
      throw new InternalServerError('Model not found');
    }
    return {
      id: model.id,
      enabled: model.enabled,
      modelProvider: model.modelProvider as ModelProviders,
      modelVersion: model.modelVersion as ModelVersions,
      apiConfig: JSON.parse(model.ModelKeys?.configs || '{}'),
      fileConfig: JSON.parse(model.fileConfig || '{}') as ChatModelFileConfig,
      modelConfig: JSON.parse(model.modelConfig || '{}') as ChatModelConfig,
      priceConfig: JSON.parse(
        model.priceConfig || '{}',
      ) as ChatModelPriceConfig,
    };
  }

  static async findModelByModelKeyId(modelKeysId: string) {
    return await prisma.chatModels.findFirst({
      where: { modelKeysId },
    });
  }

  static async deleteModelById(id: string) {
    return await prisma.chatModels.delete({ where: { id } });
  }

  static async createModel(params: CreateModel) {
    return await prisma.chatModels.create({
      data: {
        ...params,
      },
    });
  }

  static async updateModel(params: UpdateModel) {
    return await prisma.chatModels.update({
      where: { id: params.id },
      data: {
        ...params,
      },
    });
  }
}