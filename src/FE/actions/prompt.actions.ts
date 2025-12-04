import { Prompt, PromptSlim } from '@/types/prompt';

import {
  PromptAction,
  PromptActionTypes,
} from '@/reducers/prompt.reducer';

export const setDefaultPrompt = (
  prompt: Prompt,
): PromptAction => ({
  type: PromptActionTypes.SET_DEFAULT_PROMPT,
  payload: prompt,
});

export const setPrompts = (prompts: PromptSlim[]): PromptAction => ({
  type: PromptActionTypes.SET_PROMPTS,
  payload: prompts,
});

