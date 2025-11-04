import React from 'react';
import { AdminModelDto } from '@/types/adminApis';
import { Prompt, PromptSlim } from '@/types/prompt';
import { ChatSpanMcp } from '@/types/clientApis';
import { DEFAULT_TEMPERATURE } from '@/types/chat';
import useTranslation from '@/hooks/useTranslation';

import {
  IconCode,
  IconTemperature,
  IconTokens,
  IconWorld
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Slider } from '@/components/ui/slider';
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
  mcps: ChatSpanMcp[];
  temperature: number | null;
  maxOutputTokens: number | null;
  mcpServersLoaded: boolean;
  onChangePromptText: (value: string) => void;
  onChangePrompt: (prompt: Prompt) => void;
  onChangeEnableSearch: (value: boolean) => void;
  onChangeCodeExecution: (value: boolean) => void;
  onChangeReasoningEffort: (value: string) => void;
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
  mcps,
  temperature,
  maxOutputTokens,
  mcpServersLoaded,
  onChangePromptText,
  onChangePrompt,
  onChangeEnableSearch,
  onChangeCodeExecution,
  onChangeReasoningEffort,
  onChangeMcps,
  onChangeTemperature,
  onChangeMaxOutputTokens,
  onRequestMcpLoad,
}) => {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-2">
      {/* System Prompt */}
      {model.allowSystemPrompt && (
        <SystemPrompt
          currentPrompt={systemPrompt}
          prompts={prompts}
          model={model}
          onChangePromptText={onChangePromptText}
          onChangePrompt={onChangePrompt}
        />
      )}

      {/* Internet Search */}
      {model.allowSearch && (
        <FeatureToggle
          label={t('Internet Search')}
          enable={webSearchEnabled}
          icon={<IconWorld size={16} />}
          onChange={onChangeEnableSearch}
        />
      )}

      {/* Code Execution */}
      {model.allowCodeExecution && (
        <FeatureToggle
          label={t('Code Execution')}
          enable={codeExecutionEnabled}
          icon={<IconCode size={16} />}
          onChange={onChangeCodeExecution}
        />
      )}

      {/* Reasoning Effort */}
      {model.reasoningEffortOptions && 
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
              <IconTemperature size={16} />
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
              className="h-6 px-2 text-xs"
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
            <IconTokens size={16} />
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
            className="h-6 px-2 text-xs"
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
    </div>
  );
};

export default ChatResponsePresetConfig;
