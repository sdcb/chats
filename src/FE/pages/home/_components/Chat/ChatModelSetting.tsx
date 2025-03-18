import { useContext } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatPrompt } from '@/utils/promptVariable';

import { AdminModelDto } from '@/types/adminApis';
import { DEFAULT_TEMPERATURE } from '@/types/chat';
import { Prompt } from '@/types/prompt';

import ReasoningEffortRadio from '@/components/ReasoningEffortRadio/ReasoningEffortRadio';
import TemperatureSlider from '@/components/TemperatureSlider/TemperatureSlider';

import { setSelectedChat } from '../../_actions/chat.actions';
import HomeContext from '../../_contexts/home.context';
import ChatModelInfo from './ChatModelInfo';
import EnableNetworkSearch from './EnableNetworkSearch';
import SystemPrompt from './SystemPrompt';

import { cn } from '@/lib/utils';

const ChatModelSetting = () => {
  const { t } = useTranslation();
  const {
    state: { defaultPrompt, selectedChat, modelMap, prompts },
    hasModel,
    chatDispatch,
  } = useContext(HomeContext);
  const spanCount = selectedChat.spans.length;

  const onChangePrompt = (
    spanId: number,
    prompt: Prompt,
    model: AdminModelDto,
  ) => {
    const text = formatPrompt(prompt.content || '', { model });
    const promptTemperature = prompt.temperature;
    const spans = selectedChat.spans.map((s) =>
      s.spanId === spanId
        ? {
            ...s,
            systemPrompt: text,
            temperature:
              promptTemperature != null ? promptTemperature : s.temperature,
          }
        : s,
    );
    chatDispatch(setSelectedChat({ ...selectedChat, spans }));
  };
  const onChangePromptText = (spanId: number, value: string) => {
    const spans = selectedChat.spans.map((s) =>
      s.spanId === spanId ? { ...s, systemPrompt: value } : s,
    );
    chatDispatch(setSelectedChat({ ...selectedChat, spans }));
  };

  const onChangeTemperature = (spanId: number, value: number) => {
    const spans = selectedChat.spans.map((s) =>
      s.spanId === spanId ? { ...s, temperature: value } : s,
    );
    chatDispatch(setSelectedChat({ ...selectedChat, spans }));
  };

  const onChangeEnableSearch = (spanId: number, value: boolean) => {
    const spans = selectedChat.spans.map((s) =>
      s.spanId === spanId ? { ...s, enableSearch: value } : s,
    );
    chatDispatch(setSelectedChat({ ...selectedChat, spans }));
  };

  const onChangeReasoningEffort = (spanId: number, value: string) => {
    const spans = selectedChat.spans.map((s) =>
      s.spanId === spanId ? { ...s, reasoningEffort: Number(value) } : s,
    );
    chatDispatch(setSelectedChat({ ...selectedChat, spans }));
  };

  return (
    <div
      className={cn(
        'mx-auto px-0 md:px-8 pt-6 pb-32',
        spanCount === 1 && 'w-full md:w-1/2',
      )}
    >
      {hasModel() && (
        <div className="grid grid-cols-[repeat(auto-fit,minmax(375px,1fr))] gap-4">
          {selectedChat?.spans.map((span) => {
            return (
              <div
                key={'chat-model-' + span.spanId}
                className="space-y-4 rounded-lg p-4 border"
              >
                <ChatModelInfo modelId={span.modelId} />
                {modelMap[span.modelId]?.allowSystemPrompt && (
                  <SystemPrompt
                    currentPrompt={defaultPrompt?.content || null}
                    prompts={prompts}
                    model={modelMap[span.modelId]}
                    onChangePromptText={(value) => {
                      onChangePromptText(span.spanId, value);
                    }}
                    onChangePrompt={(prompt) => {
                      onChangePrompt(
                        span.spanId,
                        prompt,
                        modelMap[span.modelId],
                      );
                    }}
                  />
                )}
                {modelMap[span.modelId]?.allowTemperature && (
                  <TemperatureSlider
                    label={t('Temperature')}
                    min={0}
                    max={1}
                    defaultTemperature={span.temperature || DEFAULT_TEMPERATURE}
                    onChangeTemperature={(value) => {
                      onChangeTemperature(span.spanId, value);
                    }}
                  />
                )}
                {modelMap[span.modelId]?.allowSearch && (
                  <EnableNetworkSearch
                    label={t('Internet Search')}
                    enable={span.enableSearch}
                    onChange={(value) => {
                      onChangeEnableSearch(span.spanId, value);
                    }}
                  />
                )}
                {modelMap[span.modelId]?.allowReasoningEffort && (
                  <ReasoningEffortRadio
                    value={`${span.reasoningEffort || 0}`}
                    onValueChange={(value) => {
                      onChangeReasoningEffort(span.spanId, value);
                    }}
                  />
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};

export default ChatModelSetting;
