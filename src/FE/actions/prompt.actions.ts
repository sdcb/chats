import {
  PromptAction,
  PromptActionTypes,
  SetDefaultPromptType,
  SetPromptsType,
} from '@/reducers/prompt.reducer';

export const setDefaultPrompt = (
  prompt: SetDefaultPromptType,
): PromptAction => ({
  type: PromptActionTypes.SET_DEFAULT_PROMPT,
  payload: prompt,
});

export const setPrompts = (prompts: SetPromptsType): PromptAction => ({
  type: PromptActionTypes.SET_PROMPTS,
  payload: prompts,
});

export default function () {}
