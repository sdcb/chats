import { useContext, useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';
import { DEFAULT_TEMPERATURE } from '@/types/chat';
import { ChatSpanDto } from '@/types/clientApis';
import { Prompt } from '@/types/prompt';

import ChatIcon from '@/components/ChatIcon/ChatIcon';
import ChatModelDropdownMenu from '@/components/ChatModelDropdownMenu/ChatModelDropdownMenu';
import { IconChevronDown, IconChevronRight } from '@/components/Icons';
import ModelParams from '@/components/ModelParams/ModelParams';
import ReasoningEffortRadio from '@/components/ReasoningEffortRadio/ReasoningEffortRadio';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogTitle,
} from '@/components/ui/dialog';
import { Slider } from '@/components/ui/slider';
import { Switch } from '@/components/ui/switch';

import { setSelectedChat } from '../../_actions/chat.actions';
import HomeContext from '../../_contexts/home.context';
import ChatModelInfo from './ChatModelInfo';
import EnableNetworkSearch from './EnableNetworkSearch';
import SystemPrompt from './SystemPrompt';

import { putChatSpan } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

interface Props {
  spanId: number;
  notSetSpanDisabled: boolean;
  isOpen: boolean;
  onClose: () => void;
  onRemove: (spanId: number) => void;
}
const ChatModelSettingModal = (props: Props) => {
  const { spanId, notSetSpanDisabled, isOpen, onRemove, onClose } = props;
  const {
    state: { selectedChat, modelMap, prompts, models },
    hasModel,
    chatDispatch,
  } = useContext(HomeContext);
  const [span, setSpan] = useState<ChatSpanDto>();
  const [model, setModel] = useState<AdminModelDto>();
  const [isShowAdvParams, setIsShowAdvParams] = useState(false);

  useEffect(() => {
    const sp = selectedChat.spans.find((x) => x.spanId === spanId)!;
    setSpan(sp);
    setModel(modelMap[sp?.modelId]);
    setIsShowAdvParams(false);
  }, [isOpen]);

  const { t } = useTranslation();

  const onChangeModel = (model: AdminModelDto) => {
    setModel(modelMap[model?.modelId]);
    setSpan({
      ...span!,
      modelId: model.modelId,
      modelName: model.name,
      modelProviderId: model.modelProviderId,
    });
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

  const onChangeTemperature = (value: number | null) => {
    setSpan({ ...span!, temperature: value });
  };

  const onChangeEnableSearch = (value: boolean) => {
    setSpan({ ...span!, webSearchEnabled: value });
  };

  const onChangeReasoningEffort = (value: string) => {
    setSpan({ ...span!, reasoningEffort: Number(value) });
  };

  const onChangeMaxOutputTokens = (value: number | null) => {
    setSpan({ ...span!, maxOutputTokens: value });
  };

  const onChangeSpanEnable = (value: boolean) => {
    if (notSetSpanDisabled && value === false) {
      return;
    }
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
      reasoningEffort: span.reasoningEffort,
      webSearchEnabled: !!span.webSearchEnabled,
    }).then(() => {
      const spans = selectedChat.spans.map((s) =>
        s.spanId === spanId ? { ...span! } : s,
      );
      chatDispatch(setSelectedChat({ ...selectedChat, spans }));
    });
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="w-full sm:w-[560px] max-h-[90vh] flex flex-col p-0 gap-0 overflow-hidden">
        <DialogTitle></DialogTitle>
        {span && model && hasModel() && (
          <div className="flex-1 overflow-y-auto p-4 mt-5">
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
                    onChangeModel(model);
                  }}
                />
                <ChatModelInfo modelId={span.modelId} />
              </div>
              {modelMap[span.modelId]?.allowSystemPrompt && (
                <SystemPrompt
                  currentPrompt={span.systemPrompt || null}
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
              {model?.allowSearch && (
                <EnableNetworkSearch
                  label={t('Internet Search')}
                  enable={span.webSearchEnabled}
                  onChange={(value) => {
                    onChangeEnableSearch(value);
                  }}
                />
              )}
              {model?.allowReasoningEffort && (
                <ReasoningEffortRadio
                  value={`${span?.reasoningEffort}`}
                  onValueChange={(value) => {
                    onChangeReasoningEffort(value);
                  }}
                />
              )}
              <div className="flex flex-col gap-4">
                <div
                  className="flex justify-between"
                  onClick={() => {
                    setIsShowAdvParams(!isShowAdvParams);
                  }}
                >
                  <div>{t('Advanced Params')}</div>
                  <div>
                    {isShowAdvParams ? (
                      <IconChevronDown />
                    ) : (
                      <IconChevronRight />
                    )}
                  </div>
                </div>
                <div
                  className={cn(
                    'hidden',
                    isShowAdvParams && 'flex flex-col gap-2',
                  )}
                >
                  <ModelParams
                    label={t('Temperature')}
                    isExpand={span.temperature !== null}
                    hidden={!(model.minTemperature !== model.maxTemperature)}
                    value={span.temperature || DEFAULT_TEMPERATURE}
                    tool={
                      <Slider
                        className="cursor-pointer"
                        min={model.minTemperature}
                        max={model.maxTemperature}
                        step={0.01}
                        value={[span.temperature || DEFAULT_TEMPERATURE]}
                        onValueChange={(values) => {
                          onChangeTemperature(values[0]);
                        }}
                      />
                    }
                    onChangeToDefault={() => {
                      onChangeTemperature(null);
                    }}
                    onChangeToCustom={() => {
                      onChangeTemperature(DEFAULT_TEMPERATURE);
                    }}
                  />
                  <ModelParams
                    label={t('Max Tokens')}
                    isExpand={span.maxOutputTokens !== null}
                    value={span.maxOutputTokens || model.maxResponseTokens}
                    tool={
                      <Slider
                        className="cursor-pointer"
                        min={0}
                        max={model.maxResponseTokens}
                        step={1}
                        value={[
                          span.maxOutputTokens || model.maxResponseTokens,
                        ]}
                        onValueChange={(values) => {
                          onChangeMaxOutputTokens(values[0]);
                        }}
                      />
                    }
                    onChangeToDefault={() => {
                      onChangeMaxOutputTokens(null);
                    }}
                    onChangeToCustom={() => {
                      onChangeMaxOutputTokens(model.maxResponseTokens);
                    }}
                  />
                </div>
              </div>
            </div>
          </div>
        )}
        <DialogFooter className="px-4 py-3 border-t">
          <div className="flex gap-4 justify-end items-center">
            <Switch
              onCheckedChange={onChangeSpanEnable}
              checked={span?.enabled}
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
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default ChatModelSettingModal;
