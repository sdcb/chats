import { useContext, useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';
import { DEFAULT_TEMPERATURE, MAX_SELECT_MODEL_COUNT } from '@/types/chat';
import { ChatSpanDto, GetChatPresetResult } from '@/types/clientApis';
import { Prompt } from '@/types/prompt';

import ChatIcon from '@/components/ChatIcon/ChatIcon';
import ChatModelDropdownMenu from '@/components/ChatModelDropdownMenu/ChatModelDropdownMenu';
import {
  IconChevronDown,
  IconChevronRight,
  IconPlus,
} from '@/components/Icons';
import ModelParams from '@/components/ModelParams/ModelParams';
import ReasoningEffortRadio from '@/components/ReasoningEffortRadio/ReasoningEffortRadio';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Slider } from '@/components/ui/slider';
import { Switch } from '@/components/ui/switch';

import HomeContext from '../../_contexts/home.context';
import ChatModelInfo from './ChatModelInfo';
import EnableNetworkSearch from './EnableNetworkSearch';
import SystemPrompt from './SystemPrompt';

import { postChatPreset, putChatPreset } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

interface Props {
  chatPreset?: GetChatPresetResult;
  isOpen: boolean;
  onClose: () => void;
}

const ChatPresetModal = (props: Props) => {
  const { chatPreset, isOpen, onClose } = props;
  const {
    state: { selectedChat, modelMap, prompts, models, defaultPrompt },
  } = useContext(HomeContext);
  const [spans, setSpans] = useState<ChatSpanDto[]>([]);
  const [selectedSpan, setSelectedSpan] = useState<ChatSpanDto>();
  const [name, setName] = useState(chatPreset?.name);
  const [isShowAdvParams, setIsShowAdvParams] = useState(false);
  const [presetSpanCount, setPresetSpanCount] = useState(0);

  useEffect(() => {
    if (chatPreset) {
      setName(chatPreset.name);
      setSpans(chatPreset.spans);
      if (chatPreset.spans.length > 0) {
        setSelectedSpan(chatPreset.spans[0]);
      }
    } else {
      setName(t('New preset model group'));
      setSpans([]);
      setSelectedSpan(undefined);
    }
    setPresetSpanCount(chatPreset?.spans.length || 0);
    setIsShowAdvParams(false);
  }, [isOpen]);

  const { t } = useTranslation();

  const onChangeModel = (model: AdminModelDto) => {
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            modelId: model.modelId,
            modelName: model.name,
            modelProviderId: model.modelProviderId,
          };
          setSelectedSpan({
            ...s,
            maxOutputTokens: null,
            temperature: null,
            reasoningEffort: 0,
            webSearchEnabled: false,
          });
          return s;
        }
        return span;
      });
    });
  };

  const handleSave = () => {
    if (!name?.trim()) {
      toast.error(t('Please enter a name'));
      return;
    }
    const params = {
      name: name!,
      spans: spans.map((span) => ({
        enabled: span.enabled,
        modelId: span.modelId,
        systemPrompt: span.systemPrompt,
        maxOutputTokens: span?.maxOutputTokens || null,
        temperature: span?.temperature || null,
        reasoningEffort: span.reasoningEffort,
        webSearchEnabled: !!span.webSearchEnabled,
      })),
    };
    if (chatPreset) {
      putChatPreset(chatPreset.id, params).then(() => {
        onClose();
      });
    } else {
      postChatPreset(params).then(() => {
        onClose();
      });
    }
  };

  const handleAddChatModel = async (modelId: number) => {
    const m = modelMap[modelId];
    const count = presetSpanCount + 1;
    setPresetSpanCount(count);
    const span = {
      spanId: count,
      enabled: true,
      modelId: m.modelId,
      modelName: m.name,
      modelProviderId: m.modelProviderId,
      systemPrompt: defaultPrompt?.content || '',
      maxOutputTokens: null,
      temperature: null,
      reasoningEffort: 0,
      webSearchEnabled: false,
    };
    setSpans([...spans, span]);
    setSelectedSpan(span);
  };

  const onChangePrompt = (prompt: Prompt) => {
    const promptTemperature = prompt.temperature;
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            systemPrompt: prompt.content,
            temperature:
              promptTemperature != null ? promptTemperature : span!.temperature,
          };
          setSelectedSpan({
            ...s,
          });
          return s;
        }
        return span;
      });
    });
  };

  const onChangePromptText = (value: string) => {
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            systemPrompt: value,
          };
          setSelectedSpan({
            ...s,
          });
          return s;
        }
        return span;
      });
    });
  };

  const onChangeTemperature = (value: number | null) => {
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            temperature: value,
          };
          setSelectedSpan({
            ...s,
          });
          return s;
        }
        return span;
      });
    });
  };

  const onChangeEnableSearch = (value: boolean) => {
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            webSearchEnabled: value,
          };
          setSelectedSpan({
            ...s,
          });
          return s;
        }
        return span;
      });
    });
  };

  const onChangeReasoningEffort = (value: string) => {
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            reasoningEffort: Number(value),
          };
          setSelectedSpan({
            ...s,
          });
          return s;
        }
        return span;
      });
    });
  };

  const onChangeMaxOutputTokens = (value: number | null) => {
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            maxOutputTokens: value,
          };
          setSelectedSpan({
            ...s,
          });
          return s;
        }
        return span;
      });
    });
  };

  const onChangeSpanEnable = (value: boolean) => {
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            enabled: value,
          };
          setSelectedSpan({
            ...s,
          });
          return s;
        }
        return span;
      });
    });
  };

  const onRemoveSpan = () => {
    setSpans((prev) => {
      const spanList = prev.filter((x) => x.spanId !== selectedSpan!.spanId);
      const spanCount = spanList.length;
      setSelectedSpan(
        spanList.length > 0 ? spanList[spanCount - 1] : undefined,
      );
      return spanList;
    });
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogTitle></DialogTitle>
      <DialogContent className="w-full sm:w-[560px] max-h-[90vh] flex flex-col p-0 gap-0 overflow-hidden">
        <div className="flex-1 overflow-y-auto p-4">
          <div className="mt-5">
            <Input
              value={name}
              placeholder={t('Please enter a name')}
              onChange={(e) => {
                setName(e.target.value);
              }}
            ></Input>
          </div>
          {spans?.length === 0 ? (
            <div className="flex items-center w-full justify-center h-96">
              <ChatModelDropdownMenu
                className="p-0"
                triggerClassName={'hover:bg-transparent p-0 h-10'}
                groupClassName="overflow-y-scroll max-h-60 sm:max-h-full custom-scrollbar"
                models={models}
                content={
                  <Button
                    variant="ghost"
                    className="bg-transparent w-52 h-32 border"
                  >
                    <IconPlus />
                    {t('Add Model')}
                  </Button>
                }
                hideIcon={true}
                onChangeModel={(model) => {
                  handleAddChatModel(model.modelId);
                }}
              />
            </div>
          ) : (
            <>
              <div className="flex overflow-x-auto custom-scrollbar gap-2 items-center mt-4 mb-2 pb-2">
                {spans.map((span) => (
                  <div
                    key={'chat-preset-' + span.spanId}
                    className={cn(
                      'flex flex-shrink-0 flex-nowrap items-center gap-2 rounded-sm px-2 h-10 cursor-pointer bg-transparent border',
                      selectedSpan?.spanId === span.spanId ? 'bg-muted' : '',
                      !modelMap[span.modelId] && 'grayscale',
                    )}
                    onClick={() => {
                      setSelectedSpan(span);
                    }}
                  >
                    <ChatIcon providerId={span.modelProviderId} />
                    {span.modelName}
                  </div>
                ))}
                {selectedChat.spans.length < MAX_SELECT_MODEL_COUNT && (
                  <ChatModelDropdownMenu
                    className="p-0"
                    triggerClassName={'hover:bg-transparent p-0 h-10'}
                    groupClassName="overflow-y-scroll max-h-60 sm:max-h-full custom-scrollbar"
                    models={models}
                    content={
                      <Button variant="ghost" className="bg-muted">
                        <IconPlus />
                      </Button>
                    }
                    hideIcon={true}
                    onChangeModel={(model) => {
                      handleAddChatModel(model.modelId);
                    }}
                  />
                )}
              </div>
              <div className="flex flex-col">
                {selectedSpan && (
                  <div className="flex w-full flex-col gap-2">
                    <div className="flex-col justify-between items-center w-full h-16">
                      <div className="flex gap-2 w-full items-center">
                        <ChatModelDropdownMenu
                          className="p-0"
                          triggerClassName={
                            'hover:bg-transparent px-2 border w-full h-10'
                          }
                          groupClassName="overflow-y-scroll max-h-60 sm:max-h-full custom-scrollbar"
                          models={models}
                          content={
                            <div className="flex gap-2 items-center">
                              <ChatIcon
                                providerId={selectedSpan.modelProviderId}
                              />
                              {selectedSpan.modelName}
                            </div>
                          }
                          hideIcon={true}
                          onChangeModel={(model) => {
                            onChangeModel(model);
                          }}
                        />
                        <Switch
                          onCheckedChange={onChangeSpanEnable}
                          checked={selectedSpan.enabled}
                        />
                        <Button
                          variant="destructive"
                          onClick={() => {
                            onRemoveSpan();
                          }}
                        >
                          {t('Remove')}
                        </Button>
                      </div>
                      <div className="h-5">
                        <ChatModelInfo modelId={selectedSpan.modelId} />
                      </div>
                    </div>
                    {modelMap[selectedSpan.modelId]?.allowSystemPrompt && (
                      <SystemPrompt
                        currentPrompt={selectedSpan.systemPrompt || null}
                        prompts={prompts}
                        model={modelMap[selectedSpan.modelId]}
                        onChangePromptText={(value) => {
                          onChangePromptText(value);
                        }}
                        onChangePrompt={(prompt) => {
                          onChangePrompt(prompt);
                        }}
                      />
                    )}
                    {modelMap[selectedSpan.modelId]?.allowSearch && (
                      <EnableNetworkSearch
                        label={t('Internet Search')}
                        enable={selectedSpan.webSearchEnabled}
                        onChange={(value) => {
                          onChangeEnableSearch(value);
                        }}
                      />
                    )}
                    {modelMap[selectedSpan.modelId]?.allowReasoningEffort && (
                      <ReasoningEffortRadio
                        value={`${selectedSpan?.reasoningEffort}`}
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
                          isExpand={selectedSpan.temperature !== null}
                          hidden={
                            !(
                              modelMap[selectedSpan.modelId]?.minTemperature !==
                              modelMap[selectedSpan.modelId]?.maxTemperature
                            )
                          }
                          value={selectedSpan.temperature || DEFAULT_TEMPERATURE}
                          tool={
                            <Slider
                              className="cursor-pointer"
                              min={modelMap[selectedSpan.modelId]?.minTemperature}
                              max={modelMap[selectedSpan.modelId]?.maxTemperature}
                              step={0.01}
                              value={[
                                selectedSpan.temperature || DEFAULT_TEMPERATURE,
                              ]}
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
                          isExpand={selectedSpan.maxOutputTokens !== null}
                          value={
                            selectedSpan.maxOutputTokens ||
                            modelMap[selectedSpan.modelId]?.maxResponseTokens
                          }
                          tool={
                            <Slider
                              className="cursor-pointer"
                              min={0}
                              max={
                                modelMap[selectedSpan.modelId]?.maxResponseTokens
                              }
                              step={1}
                              value={[
                                selectedSpan.maxOutputTokens ||
                                  modelMap[selectedSpan.modelId]
                                    ?.maxResponseTokens,
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
                            onChangeMaxOutputTokens(
                              modelMap[selectedSpan.modelId].maxResponseTokens,
                            );
                          }}
                        />
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </>
          )}
        </div>
        <DialogFooter className="px-4 py-3 border-t">
          <div className="flex gap-4 justify-end items-center">
            <Button
              variant="default"
              disabled={presetSpanCount === 0}
              onClick={() => {
                handleSave();
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

export default ChatPresetModal;
