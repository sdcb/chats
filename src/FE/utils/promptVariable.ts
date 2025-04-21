import { formatDate, formatDateTime } from '@/utils/date';

import { AdminModelDto } from '@/types/adminApis';

interface PromptParams {
  model: AdminModelDto;
}

export const PromptVariables = {
  '{{CURRENT_DATE}}': () => formatDate(new Date().toLocaleDateString()),
  '{{CURRENT_TIME}}': () => formatDateTime(new Date().toLocaleString()),
  '{{MODEL_NAME}}': (params?: PromptParams) =>
    params?.model?.modelReferenceShortName ||
    params?.model?.modelReferenceName ||
    '',
};

export function formatPrompt(prompt: string, params?: PromptParams) {
  Object.keys(PromptVariables).forEach((k) => {
    const key = k as keyof typeof PromptVariables;
    prompt = prompt?.replaceAll(key, PromptVariables[key](params));
  });
  return prompt;
}
