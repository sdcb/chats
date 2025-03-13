import { useContext } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatPrompt } from '@/utils/promptVariable';

import { AdminModelDto } from '@/types/adminApis';
import { DEFAULT_TEMPERATURE } from '@/types/chat';
import { ReasoningEffortType } from '@/types/model';
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

interface Props {
  spanId: number;
  isOpen: boolean;
  onClose: () => void;
  onRemove: (spanId: number) => void;
  onChangeModel: (spanId: number, modelId: number) => void;
}
const ChatModelSettingModal = (props: Props) => {
  const { spanId, isOpen, onRemove, onChangeModel, onClose } = props;
  const {
    state: { defaultPrompt, selectedChat, modelMap, prompts, models },
    hasModel,
    chatDispatch,
  } = useContext(HomeContext);
  const span = selectedChat.spans.find((x) => x.spanId === spanId);

  const { t } = useTranslation();

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
            prompt: text,
            temperature:
              promptTemperature != null ? promptTemperature : s.temperature,
          }
        : s,
    );
    chatDispatch(setSelectedChat({ ...selectedChat, spans }));
  };

  const onChangePromptText = (spanId: number, value: string) => {
    const spans = selectedChat.spans.map((s) =>
      s.spanId === spanId ? { ...s, prompt: value } : s,
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

  const onChangeReasoningEffort = (
    spanId: number,
    value: ReasoningEffortType,
  ) => {
    const spans = selectedChat.spans.map((s) =>
      s.spanId === spanId ? { ...s, reasoningEffort: value } : s,
    );
    chatDispatch(setSelectedChat({ ...selectedChat, spans }));
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
                    onChangeModel(spanId, model.modelId);
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
                    onChangePromptText(span.spanId, value);
                  }}
                  onChangePrompt={(prompt) => {
                    onChangePrompt(span.spanId, prompt, modelMap[span.modelId]);
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
                  value={span.reasoningEffort || 'medium'}
                  onValueChange={(value) => {
                    onChangeReasoningEffort(span.spanId, value);
                  }}
                />
              )}
            </div>
            <div className="flex gap-4 justify-end mt-5 items-center">
              <Switch />
              <Button
                variant="destructive"
                onClick={() => {
                  onRemove(spanId);
                  onClose();
                }}
              >
                {t('Remove')}
              </Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
};

export default ChatModelSettingModal;
