import React from 'react';
import { AdminModelDto } from '@/types/adminApis';
import { Prompt, PromptSlim } from '@/types/prompt';
import { ChatSpanMcp } from '@/types/clientApis';
import { DEFAULT_TEMPERATURE } from '@/types/chat';
import useTranslation from '@/hooks/useTranslation';

import {
  IconCode,
  IconReasoning,
  IconTemperature,
  IconTokens,
  IconWorld
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Slider } from '@/components/ui/slider';
import { Input } from '@/components/ui/input';
import SystemPrompt from './SystemPrompt';
import FeatureToggle from './FeatureToggle';
import ReasoningEffortRadio from '@/components/ReasoningEffortRadio/ReasoningEffortRadio';
import McpSelector from '@/components/McpSelector/McpSelector';

interface ChatResponsePresetConfigProps {
  model: AdminModelDto;
  systemPrompt: string | null;
  prompts: PromptSlim[];
  webSearchEnabled: boolean;
  codeExecutionEnabled: boolean;
  reasoningEffort: number;
  thinkingBudget: number | null;
  mcps: ChatSpanMcp[];
  temperature: number | null;
  maxOutputTokens: number | null;
  mcpServersLoaded: boolean;
  onChangePromptText: (value: string) => void;
  onChangePrompt: (prompt: Prompt) => void;
  onChangeEnableSearch: (value: boolean) => void;
  onChangeCodeExecution: (value: boolean) => void;
  onChangeReasoningEffort: (value: string) => void;
  onChangeThinkingBudget: (value: number | null) => void;
  onChangeMcps: (mcps: ChatSpanMcp[]) => void;
  onChangeTemperature: (value: number | null) => void;
  onChangeMaxOutputTokens: (value: number | null) => void;
  onRequestMcpLoad: () => Promise<void>;
}

/**
 * Chat/Response API 预设配置组件 (apiType=0/1)
 */
const ChatResponsePresetConfig: React.FC<ChatResponsePresetConfigProps> = ({
  model,
  systemPrompt,
  prompts,
  webSearchEnabled,
  codeExecutionEnabled,
  reasoningEffort,
  thinkingBudget,
  mcps,
  temperature,
  maxOutputTokens,
  mcpServersLoaded,
  onChangePromptText,
  onChangePrompt,
  onChangeEnableSearch,
  onChangeCodeExecution,
  onChangeReasoningEffort,
  onChangeThinkingBudget,
  onChangeMcps,
  onChangeTemperature,
  onChangeMaxOutputTokens,
  onRequestMcpLoad,
}) => {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-2">
      {/* System Prompt */}
      <SystemPrompt
        currentPrompt={systemPrompt}
        prompts={prompts}
        model={model}
        onChangePromptText={onChangePromptText}
        onChangePrompt={onChangePrompt}
      />

      {/* Internet Search */}
      {model.allowSearch && (
        <FeatureToggle
          label={t('Internet Search')}
          enable={webSearchEnabled}
          icon={<IconWorld size={20} />}
          onChange={onChangeEnableSearch}
        />
      )}

      {/* Code Execution */}
      {model.allowCodeExecution && (
        <FeatureToggle
          label={t('Code Execution')}
          enable={codeExecutionEnabled}
          icon={<IconCode size={20} />}
          onChange={onChangeCodeExecution}
        />
      )}

      {/* Reasoning Effort */}
      {Array.isArray(model.reasoningEffortOptions) &&
        model.reasoningEffortOptions.length > 0 && (
        <ReasoningEffortRadio
          value={`${reasoningEffort}`}
          availableOptions={model.reasoningEffortOptions}
          onValueChange={onChangeReasoningEffort}
        />
      )}

      {/* MCP Selector */}
      {model.allowToolCall && (
        <McpSelector
          value={mcps}
          onValueChange={onChangeMcps}
          onRequestMcpLoad={onRequestMcpLoad}
          mcpServersLoaded={mcpServersLoaded}
        />
      )}

      {/* Temperature */}
      {model.minTemperature !== model.maxTemperature && (
        <div className="flex flex-col gap-4">
          <div className="flex justify-between">
            <div className="flex gap-1 items-center text-neutral-700 dark:text-neutral-400">
              <IconTemperature size={20} />
              {t('Temperature')}
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                if (temperature === null) {
                  onChangeTemperature(DEFAULT_TEMPERATURE);
                } else {
                  onChangeTemperature(null);
                }
              }}
              className="h-6 px-2 text-sm"
            >
              {temperature === null ? t('Default') : t('Custom')}
            </Button>
          </div>
          {temperature !== null && (
            <div className="px-2">
              <Slider
                className="cursor-pointer"
                min={model.minTemperature}
                max={model.maxTemperature}
                step={0.01}
                value={[temperature || DEFAULT_TEMPERATURE]}
                onValueChange={(values) => {
                  onChangeTemperature(values[0]);
                }}
              />
              <div className="text-xs text-gray-500 mt-1">
                {temperature || DEFAULT_TEMPERATURE}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Max Output Tokens */}
      <div className="flex flex-col gap-4">
        <div className="flex justify-between">
          <div className="flex gap-1 items-center text-neutral-700 dark:text-neutral-400">
            <IconTokens size={20} />
            {t('Max Output Tokens')}
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              if (maxOutputTokens === null) {
                onChangeMaxOutputTokens(model.maxResponseTokens);
              } else {
                onChangeMaxOutputTokens(null);
              }
            }}
            className="h-6 px-2 text-sm"
          >
            {maxOutputTokens === null ? t('Default') : t('Custom')}
          </Button>
        </div>
        {maxOutputTokens !== null && (
          <div className="px-2">
            <Slider
              className="cursor-pointer"
              min={0}
              max={model.maxResponseTokens}
              step={1}
              value={[maxOutputTokens || model.maxResponseTokens]}
              onValueChange={(values) => {
                onChangeMaxOutputTokens(values[0]);
              }}
            />
            <div className="text-xs text-gray-500 mt-1">
              {maxOutputTokens || model.maxResponseTokens}
            </div>
          </div>
        )}
      </div>

      {/* Thinking Budget */}
      {model.maxThinkingBudget != null && (
        <div className="flex flex-col gap-4">
          <div className="flex justify-between">
            <div className="flex gap-1 items-center text-neutral-700 dark:text-neutral-400">
              <IconReasoning size={20} />
              {t('Thinking Budget')}
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                if (thinkingBudget === null) {
                  onChangeThinkingBudget(model.maxThinkingBudget!);
                } else {
                  onChangeThinkingBudget(null);
                }
              }}
              className="h-6 px-2 text-sm"
            >
              {thinkingBudget === null ? t('No Thinking') : t('Custom')}
            </Button>
          </div>
          {thinkingBudget !== null && (
            <div className="px-2">
              <Slider
                className="cursor-pointer"
                min={0}
                max={model.maxThinkingBudget}
                step={1}
                value={[thinkingBudget || 0]}
                onValueChange={(values) => {
                  onChangeThinkingBudget(values[0]);
                }}
              />
              <div className="text-sm text-gray-500 mt-1">
                {thinkingBudget || 0}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default ChatResponsePresetConfig;
