import { AdminModelDto } from '@/types/adminApis';

import {
  ModelAction,
  ModelActionTypes,
  SetModelsType,
} from '@/reducers/model.reducer';

export const setModels = (models: SetModelsType): ModelAction => ({
  type: ModelActionTypes.SET_MODELS,
  payload: models,
});

export const setModelMap = (models: SetModelsType): ModelAction => {
  const modelMap: Record<string, AdminModelDto> = {};
  models.forEach((x) => {
    modelMap[x.modelId] = x;
  });
  return {
    type: ModelActionTypes.SET_MODEL_MAP,
    payload: modelMap,
  };
};

export default function () {}
