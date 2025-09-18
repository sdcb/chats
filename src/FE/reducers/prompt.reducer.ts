import { Prompt, PromptSlim } from '@/types/prompt';

export type SetDefaultPromptType = Prompt;
export type SetPromptsType = PromptSlim[];

interface PromptInitialState {
  defaultPrompt: SetDefaultPromptType | null;
  prompts: SetPromptsType;
}

export const promptInitialState: PromptInitialState = {
  defaultPrompt: null,
  prompts: [],
};

export enum PromptActionTypes {
  SET_DEFAULT_PROMPT = 'SET_DEFAULT_PROMPT',
  SET_PROMPTS = 'SET_PROMPTS',
}

export type PromptAction =
  | {
      type: PromptActionTypes.SET_DEFAULT_PROMPT;
      payload: SetDefaultPromptType;
    }
  | {
      type: PromptActionTypes.SET_PROMPTS;
      payload: SetPromptsType;
    };
export default function promptReducer(
  state: PromptInitialState,
  action: PromptAction,
): PromptInitialState {
  switch (action.type) {
    case PromptActionTypes.SET_DEFAULT_PROMPT:
      return { ...state, defaultPrompt: action.payload };
    case PromptActionTypes.SET_PROMPTS:
      return { ...state, prompts: action.payload };
    default:
      return state;
  }
}
