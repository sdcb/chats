import React from 'react';
import { Control, UseFormSetValue } from 'react-hook-form';
import useTranslation from '@/hooks/useTranslation';
import { FormField } from '@/components/ui/form';
import FormInput from '@/components/ui/form/input';
import { LabelSwitch } from '@/components/ui/label-switch';
import OptionButtonGroup from './OptionButtonGroup';

interface ChatResponseConfigProps {
  control: Control<any>;
  setValue: UseFormSetValue<any>;
}

/**
 * Chat/Response API 配置组件 (apiType=0/1)
 */
const ChatResponseConfig: React.FC<ChatResponseConfigProps> = ({ control, setValue }) => {
  const { t } = useTranslation();

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
              />
            )}
          />
          <FormField
            control={control}
            name="allowSearch"
            render={({ field }) => (
              <LabelSwitch
                checked={field.value}
                onCheckedChange={field.onChange}
                label={t('Allow Search')!}
              />
            )}
          />
          <FormField
            control={control}
            name="allowSystemPrompt"
            render={({ field }) => (
              <LabelSwitch
                checked={field.value}
                onCheckedChange={field.onChange}
                label={t('Allow System Prompt')!}
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
              />
            )}
          />
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
                label={t('Min Temperature (0-2)')!}
                field={field}
              />
            )}
          />
          <FormField
            control={control}
            name="maxTemperature"
            render={({ field }) => (
              <FormInput
                type="number"
                label={t('Max Temperature (0-2)')!}
                field={field}
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

      {/* 推理级别 */}
      <div className="border-t pt-4">
        <FormField
          control={control}
          name="reasoningEffortOptions"
          render={({ field }) => {
            const isReasoningModel = field.value && field.value.trim() !== '';
            
            const handleToggleReasoning = (enabled: boolean) => {
              if (enabled) {
                // 启用推理模型，默认选中低中高（2, 3, 4）
                field.onChange('2, 3, 4');
                // 设置温度为 1
                setValue('minTemperature', 1.0);
                setValue('maxTemperature', 1.0);
              } else {
                // 关闭推理模型，清空选项
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
      </div>

      {/* Token 字段命名 */}
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
    </div>
  );
};

export default ChatResponseConfig;
