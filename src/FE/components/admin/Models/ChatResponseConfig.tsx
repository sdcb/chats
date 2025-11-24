import React from 'react';
import { Control, UseFormSetValue, UseFormWatch } from 'react-hook-form';
import useTranslation from '@/hooks/useTranslation';
import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import FormInput from '@/components/ui/form/input';
import { LabelSwitch } from '@/components/ui/label-switch';
import OptionButtonGroup from './OptionButtonGroup';
import { Input } from '@/components/ui/input';
import Tips from '@/components/Tips/Tips';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';

interface ChatResponseConfigProps {
  control: Control<any>;
  setValue: UseFormSetValue<any>;
  watch: UseFormWatch<any>;
  apiType: number; // 0: ChatCompletion, 1: Response, 3: AnthropicMessages
}

/**
 * Chat/Response/AnthropicMessages API 配置组件 (apiType=0/1/3)
 */
const ChatResponseConfig: React.FC<ChatResponseConfigProps> = ({ control, setValue, watch, apiType }) => {
  const { t } = useTranslation();
  const maxResponseTokens = watch('maxResponseTokens');
  const allowVision = watch('allowVision');

  return (
    <div className="space-y-4">
      {/* 功能开关 */}
      <div className="border-t pt-4">
        <div className="grid grid-cols-2 gap-4">
          <FormField
            control={control}
            name="allowVision"
            render={({ field }) => (
              <LabelSwitch
                checked={field.value}
                onCheckedChange={field.onChange}
                label={t('Allow Vision')!}
                tooltip={t('Enable this option to allow the model to accept images as input.')}
              />
            )}
          />
          {allowVision && (
            <FormField
              control={control}
              name="supportsVisionLink"
              render={({ field }) => (
                <LabelSwitch
                  checked={field.value}
                  onCheckedChange={field.onChange}
                  label={t('Prefer Image URL')!}
                  tooltip={t('When enabled, image URLs are sent directly to the model, saving server bandwidth and optimize latency. If the model cannot access image URLs (e.g., due to timeouts), disable this to use Base64 transmission.')}
                />
              )}
            />
          )}
          <FormField
            control={control}
            name="allowSearch"
            render={({ field }) => (
              <LabelSwitch
                checked={field.value}
                onCheckedChange={field.onChange}
                label={t('Allow Search')!}
                tooltip={t("Enables the model provider's built-in internet search capability (e.g., Qwen, Gemini, OpenRouter). This indicates the model possesses this tool and the system will invoke it. It is NOT a system-provided search tool.")}
              />
            )}
          />
          <FormField
            control={control}
            name="allowStreaming"
            render={({ field }) => (
              <LabelSwitch
                checked={field.value}
                onCheckedChange={field.onChange}
                label={t('Allow Streaming')!}
                tooltip={t('Allows the model to stream responses token by token. Usually enabled by default.')}
              />
            )}
          />
          <FormField
            control={control}
            name="allowCodeExecution"
            render={({ field }) => (
              <LabelSwitch
                checked={field.value}
                onCheckedChange={field.onChange}
                label={t('Allow Code Execution')!}
                tooltip={t("Enables the model provider's built-in code execution capability (e.g., Gemini). It is NOT a system-provided tool.")}
              />
            )}
          />
          <FormField
            control={control}
            name="allowToolCall"
            render={({ field }) => (
              <LabelSwitch
                checked={field.value}
                onCheckedChange={field.onChange}
                label={t('Allow Tool Call')!}
                tooltip={t('Enables the model to call external tools. Chats provides MCP tools; only models with this enabled will show the MCP options list.')}
              />
            )}
          />
          <FormField
            control={control}
            name="thinkTagParserEnabled"
            render={({ field }) => (
              <LabelSwitch
                checked={field.value}
                onCheckedChange={field.onChange}
                label={t('<think> Tag Parser Enabled')!}
                tooltip={t("Previously, many DeepSeek models returned thinking content mixed with non-thinking content using <think>...</think> tags. Enabling this parser extracts the thinking content into a <ThinkingMessage> for a better user experience. This also affects the API response, outputting via 'reasoning_content' instead of direct output. While many providers now handle this natively, this option is retained for exceptions.")}
              />
            )}
          />
          {apiType === 1 && (
            <FormField
              control={control}
              name="useAsyncApi"
              render={({ field }) => (
                <LabelSwitch
                  checked={field.value}
                  onCheckedChange={field.onChange}
                  label={t('Use Async API')!}
                  tooltip={t('Designed for heavy reasoning models (e.g., gpt-5-pro) to prevent gateway timeouts. It sends a request and polls for status, avoiding long-held connections. Note that this increases first-token latency. Enable only if necessary.')}
                />
              )}
            />
          )}
        </div>
      </div>

      {/* 温度范围 */}
      <div className="border-t pt-4">
        <div className="grid grid-cols-2 gap-4">
          <FormField
            control={control}
            name="minTemperature"
            render={({ field }) => (
              <FormInput
                type="number"
                label={t('Min Temperature')!}
                field={field}
                options={{ placeholder: '0-2' }}
              />
            )}
          />
          <FormField
            control={control}
            name="maxTemperature"
            render={({ field }) => (
              <FormInput
                type="number"
                label={t('Max Temperature')!}
                field={field}
                options={{ placeholder: '0-2' }}
              />
            )}
          />
        </div>
      </div>

      {/* Token 配置 */}
      <div className="border-t pt-4">
        <div className="grid grid-cols-2 gap-4">
          <FormField
            control={control}
            name="contextWindow"
            render={({ field }) => (
              <FormInput
                type="number"
                label={t('Context Window')!}
                field={field}
              />
            )}
          />
          <FormField
            control={control}
            name="maxResponseTokens"
            render={({ field }) => (
              <FormInput
                type="number"
                label={t('Max Response Tokens')!}
                field={field}
              />
            )}
          />
        </div>
      </div>

      {/* 思考模式 */}
      <div className="border-t pt-4">
        {apiType === 0 && (
          // ChatCompletion API: 显示三种模式选择
          <FormField
            control={control}
            name="reasoningEffortOptions"
            render={({ field: effortField }) => (
              <FormField
                control={control}
                name="maxThinkingBudget"
                render={({ field: budgetField }) => {
                  // 判断当前思考模式
                  const hasEffort = effortField.value && effortField.value.trim() !== '';
                  const hasBudget = budgetField.value !== null && budgetField.value !== undefined;
                  
                  let thinkingMode = 'none';
                  if (hasBudget) {
                    thinkingMode = 'budget';
                  } else if (hasEffort) {
                    thinkingMode = 'effort';
                  }
                  
                  const handleModeChange = (mode: string) => {
                    if (mode === 'none') {
                      effortField.onChange('');
                      budgetField.onChange(null);
                    } else if (mode === 'effort') {
                      effortField.onChange('2, 3, 4');
                      budgetField.onChange(null);
                      setValue('minTemperature', 1.0);
                      setValue('maxTemperature', 1.0);
                    } else if (mode === 'budget') {
                      effortField.onChange('');
                      const defaultBudget = Math.max(1, (maxResponseTokens || 8192) - 1);
                      budgetField.onChange(defaultBudget);
                    }
                  };
                  
                  return (
                    <div className="space-y-4">
                      <div className="space-y-2">
                        <Label>{t('Thinking Mode')}</Label>
                        <div className="flex gap-2">
                          <Tips
                            trigger={
                              <Button
                                type="button"
                                variant={thinkingMode === 'none' ? 'default' : 'outline'}
                                onClick={() => handleModeChange('none')}
                                className="flex-1"
                              >
                                {t('Automatic')}
                              </Button>
                            }
                            content={t('Suitable for models that handle reasoning automatically (e.g. DeepSeek-R1) or standard models')}
                          />
                          <Tips
                            trigger={
                              <Button
                                type="button"
                                variant={thinkingMode === 'effort' ? 'default' : 'outline'}
                                onClick={() => handleModeChange('effort')}
                                className="flex-1"
                              >
                                {t('Reasoning Effort')}
                              </Button>
                            }
                            content={t('Suitable for OpenAI models (e.g. gpt-5)')}
                          />
                          <Tips
                            trigger={
                              <Button
                                type="button"
                                variant={thinkingMode === 'budget' ? 'default' : 'outline'}
                                onClick={() => handleModeChange('budget')}
                                className="flex-1"
                              >
                                {t('Thinking Budget')}
                              </Button>
                            }
                            content={t('Suitable for models from Anthropic, Gemini and Qwen')}
                          />
                        </div>
                      </div>
                      
                      {thinkingMode === 'effort' && (
                        <OptionButtonGroup
                          label={t('Reasoning Effort Options')!}
                          options={[
                            { label: t('Minimal')!, value: '1' },
                            { label: t('Low')!, value: '2' },
                            { label: t('Medium')!, value: '3' },
                            { label: t('High')!, value: '4' },
                          ]}
                          value={effortField.value || ''}
                          onChange={effortField.onChange}
                        />
                      )}
                      
                      {thinkingMode === 'budget' && (
                        <FormItem className="py-1">
                          <FormLabel>{t('Max Thinking Budget')}</FormLabel>
                          <FormControl>
                            <Input
                              type="number"
                              min={1}
                              max={Math.max(1, (maxResponseTokens || 8192) - 1)}
                              value={budgetField.value ?? ''}
                              onChange={(event) => {
                                const raw = event.target.value;
                                budgetField.onChange(raw === '' ? null : Number(raw));
                              }}
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    </div>
                  );
                }}
              />
            )}
          />
        )}

        {apiType === 1 && (
          // Response API: 是否为推理模型 + 推理努力选项
          <FormField
            control={control}
            name="reasoningEffortOptions"
            render={({ field }) => {
              const isReasoningModel = field.value && field.value.trim() !== '';
              
              const handleToggleReasoning = (enabled: boolean) => {
                if (enabled) {
                  field.onChange('2, 3, 4');
                  setValue('minTemperature', 1.0);
                  setValue('maxTemperature', 1.0);
                } else {
                  field.onChange('');
                }
              };
              
              return (
                <div className="space-y-3">
                  <LabelSwitch
                    checked={isReasoningModel}
                    onCheckedChange={handleToggleReasoning}
                    label={t('Is Reasoning Model')!}
                  />
                  
                  {isReasoningModel && (
                    <OptionButtonGroup
                      label={t('Reasoning Effort Options')!}
                      options={[
                        { label: t('Minimal')!, value: '1' },
                        { label: t('Low')!, value: '2' },
                        { label: t('Medium')!, value: '3' },
                        { label: t('High')!, value: '4' },
                      ]}
                      value={field.value || ''}
                      onChange={field.onChange}
                    />
                  )}
                </div>
              );
            }}
          />
        )}

        {apiType === 3 && (
          // AnthropicMessages API: 是否为推理模型 + 最大思考预算
          <FormField
            control={control}
            name="maxThinkingBudget"
            render={({ field }) => {
              const isReasoningModel = field.value !== null && field.value !== undefined;
              
              const handleToggleReasoning = (enabled: boolean) => {
                if (enabled) {
                  const defaultBudget = Math.max(1, (maxResponseTokens || 8192) - 1);
                  field.onChange(defaultBudget);
                } else {
                  field.onChange(null);
                }
              };
              
              return (
                <div className="space-y-3">
                  <LabelSwitch
                    checked={isReasoningModel}
                    onCheckedChange={handleToggleReasoning}
                    label={t('Is Reasoning Model')!}
                  />
                  
                  {isReasoningModel && (
                    <FormItem className="py-1">
                      <FormLabel>{t('Max Thinking Budget')}</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          min={1}
                          max={Math.max(1, (maxResponseTokens || 8192) - 1)}
                          value={field.value ?? ''}
                          onChange={(event) => {
                            const raw = event.target.value;
                            field.onChange(raw === '' ? null : Number(raw));
                          }}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                </div>
              );
            }}
          />
        )}
      </div>

      {/* Token 字段命名 */}
      {apiType === 0 && (
        <div className="border-t pt-4">
          <FormField
            control={control}
            name="useMaxCompletionTokens"
            render={({ field }) => (
              <LabelSwitch
                checked={field.value}
                onCheckedChange={field.onChange}
                label={t('Use Max Completion Tokens')!}
              />
            )}
          />
        </div>
      )}
    </div>
  );
};

export default ChatResponseConfig;
