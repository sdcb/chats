import { AdminModelDto } from '@/types/adminApis';

interface ModelInitialState {
  models: AdminModelDto[];
  modelMap: Record<string, AdminModelDto>;
}

export const modelInitialState: ModelInitialState = {
  models: [],
  modelMap: {},
};

export enum ModelActionTypes {
  SET_MODELS = 'SET_MODELS',
  SET_MODEL_MAP = 'SET_MODEL_MAP',
}

export type ModelAction =
  | { type: ModelActionTypes.SET_MODELS; payload: AdminModelDto[] }
  | { type: ModelActionTypes.SET_MODEL_MAP; payload: Record<string, AdminModelDto> };

export default function modelReducer(
  state: ModelInitialState,
  action: ModelAction,
): ModelInitialState {
  switch (action.type) {
    case ModelActionTypes.SET_MODELS:
      return { ...state, models: action.payload };
    case ModelActionTypes.SET_MODEL_MAP:
      return { ...state, modelMap: action.payload };
    default:
      return state;
  }
}
