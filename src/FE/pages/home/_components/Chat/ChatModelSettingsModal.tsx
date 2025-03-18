import { useContext, useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatPrompt } from '@/utils/promptVariable';

import { AdminModelDto } from '@/types/adminApis';
import { DEFAULT_TEMPERATURE } from '@/types/chat';
import { ChatSpanDto } from '@/types/clientApis';
import { Prompt } from '@/types/prompt';

import ChatIcon from '@/components/ChatIcon/ChatIcon';
import ChatModelDropdownMenu from '@/components/ChatModelDropdownMenu/ChatModelDropdownMenu';
import ReasoningEffortRadio from '@/components/ReasoningEffortRadio/ReasoningEffortRadio';
import TemperatureSlider from '@/components/TemperatureSlider/TemperatureSlider';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent } from '@/components/ui/dialog';
import { Switch } from '@/components/ui/switch';

import { setSelectedChat } from '../../_actions/chat.actions';
import HomeContext from '../../_contexts/home.context';
import ChatModelInfo from './ChatModelInfo';
import EnableNetworkSearch from './EnableNetworkSearch';
import SystemPrompt from './SystemPrompt';

import { putChatSpan } from '@/apis/clientApis';

interface Props {
  spanId: number;
  isOpen: boolean;
  onClose: () => void;
  onRemove: (spanId: number) => void;
}
const ChatModelSettingModal = (props: Props) => {
  const { spanId, isOpen, onRemove, onClose } = props;
  const {
    state: { defaultPrompt, selectedChat, modelMap, prompts, models },
    hasModel,
    chatDispatch,
  } = useContext(HomeContext);
  const [span, setSpan] = useState<ChatSpanDto>();

  useEffect(() => {
    setSpan(selectedChat.spans.find((x) => x.spanId === spanId)!);
  }, [isOpen]);

  const { t } = useTranslation();

  const onChangeModel = (modelId: number, modelName: string) => {
    setSpan({ ...span!, modelId, modelName });
  };

  const onChangePrompt = (prompt: Prompt) => {
    const promptTemperature = prompt.temperature;
    setSpan({
      ...span!,
      systemPrompt: prompt.content,
      temperature:
        promptTemperature != null ? promptTemperature : span!.temperature,
    });
  };

  const onChangePromptText = (value: string) => {
    setSpan({ ...span!, systemPrompt: value });
  };

  const onChangeTemperature = (value: number) => {
    setSpan({ ...span!, temperature: value });
  };

  const onChangeEnableSearch = (value: boolean) => {
    setSpan({ ...span!, enableSearch: value });
  };

  const onChangeReasoningEffort = (value: string) => {
    setSpan({ ...span!, reasoningEffort: Number(value) });
  };

  const onChangeSpanEnable = (value: boolean) => {
    setSpan({ ...span!, enabled: value });
  };

  const handleSave = () => {
    if (!span) return;
    putChatSpan(span!.spanId, selectedChat.id, {
      enabled: span.enabled,
      modelId: span.modelId,
      systemPrompt: span.systemPrompt,
      maxOutputTokens: span?.maxOutputTokens || null,
      temperature: span?.temperature || null,
      reasoningEffort: span?.reasoningEffort || null,
      webSearchEnabled: !!span.enableSearch,
    }).then(() => {
      const spans = selectedChat.spans.map((s) =>
        s.spanId === spanId ? { ...span! } : s,
      );
      chatDispatch(setSelectedChat({ ...selectedChat, spans }));
    });
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="w-full sm:w-[560px]">
        {span && hasModel() && (
          <div className="grid grid-cols-[repeat(auto-fit,minmax(320px,1fr))] mt-5">
            <div className="space-y-4 rounded-lg">
              <div className="flex flex-col gap-1">
                <ChatModelDropdownMenu
                  className="p-0"
                  triggerClassName={
                    'hover:bg-transparent px-4 border w-full h-10'
                  }
                  groupClassName="overflow-y-scroll max-h-80 sm:max-h-full custom-scrollbar"
                  models={models}
                  content={
                    <div className="flex gap-2 items-center">
                      <ChatIcon providerId={span.modelProviderId} />
                      {span.modelName}
                    </div>
                  }
                  hideIcon={true}
                  onChangeModel={(model) => {
                    onChangeModel(model.modelId, model.name);
                  }}
                />
                <ChatModelInfo modelId={span.modelId} />
              </div>
              {modelMap[span.modelId]?.allowSystemPrompt && (
                <SystemPrompt
                  currentPrompt={defaultPrompt?.content || null}
                  prompts={prompts}
                  model={modelMap[span.modelId]}
                  onChangePromptText={(value) => {
                    onChangePromptText(value);
                  }}
                  onChangePrompt={(prompt) => {
                    onChangePrompt(prompt);
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
                    onChangeTemperature(value);
                  }}
                />
              )}
              {modelMap[span.modelId]?.allowSearch && (
                <EnableNetworkSearch
                  label={t('Internet Search')}
                  enable={span.enableSearch}
                  onChange={(value) => {
                    onChangeEnableSearch(value);
                  }}
                />
              )}
              {modelMap[span.modelId]?.allowReasoningEffort && (
                <ReasoningEffortRadio
                  value={`${span.reasoningEffort || 0}`}
                  onValueChange={(value) => {
                    onChangeReasoningEffort(value);
                  }}
                />
              )}
            </div>
            <div className="flex gap-4 justify-end mt-5 items-center">
              <Switch
                onCheckedChange={onChangeSpanEnable}
                checked={span.enabled}
              />
              <Button
                variant="destructive"
                onClick={() => {
                  onRemove(spanId);
                  onClose();
                }}
              >
                {t('Remove')}
              </Button>
              <Button
                variant="default"
                onClick={() => {
                  handleSave();
                  onClose();
                }}
              >
                {t('Save')}
              </Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
};

export default ChatModelSettingModal;
