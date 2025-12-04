import { AdminModelDto } from '@/types/adminApis';

import {
  ModelAction,
  ModelActionTypes,
} from '@/reducers/model.reducer';

export const setModels = (models: AdminModelDto[]): ModelAction => ({
  type: ModelActionTypes.SET_MODELS,
  payload: models,
});

export const setModelMap = (models: AdminModelDto[]): ModelAction => {
  const modelMap: Record<string, AdminModelDto> = {};
  models.forEach((x) => {
    modelMap[x.modelId] = x;
  });
  return {
    type: ModelActionTypes.SET_MODEL_MAP,
    payload: modelMap,
  };
};

