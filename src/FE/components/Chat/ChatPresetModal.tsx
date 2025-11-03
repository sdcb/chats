import { useContext, useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';
import { DEFAULT_TEMPERATURE, MAX_SELECT_MODEL_COUNT } from '@/types/chat';
import { ChatSpanDto, ChatSpanMcp, GetChatPresetResult } from '@/types/clientApis';
import { Prompt } from '@/types/prompt';

import ChatModelDropdownMenu from '@/components/ChatModelDropdownMenu/ChatModelDropdownMenu';
import {
  IconPlus,
} from '@/components/Icons';
import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';

import HomeContext from '@/contexts/home.context';
import ChatModelInfo from './ChatModelInfo';
import ChatResponsePresetConfig from './ChatResponsePresetConfig';
import ImageGenerationPresetConfig from './ImageGenerationPresetConfig';

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
    state: { modelMap, prompts, models, defaultPrompt },
    selectedChat,
  } = useContext(HomeContext);
  const [spans, setSpans] = useState<ChatSpanDto[]>([]);
  const [selectedSpan, setSelectedSpan] = useState<ChatSpanDto>();
  const [name, setName] = useState(chatPreset?.name);
  const [presetSpanCount, setPresetSpanCount] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [mcpServersLoaded, setMcpServersLoaded] = useState(false);
  const [mcpLoadingTriggered, setMcpLoadingTriggered] = useState(false);

  // JSON 验证函数
  const validateJSON = (jsonString: string): boolean => {
    if (!jsonString.trim()) return true; // 空字符串认为是有效的
    try {
      JSON.parse(jsonString);
      return true;
    } catch {
      return false;
    }
  };

  // 检查是否需要在初始化时加载MCP
  const shouldLoadMcpOnInit = (preset?: GetChatPresetResult) => {
    if (!preset) return false;
    return preset.spans.some(span => span.mcps && span.mcps.length > 0);
  };

  // 加载MCP服务器数据
  const loadMcpServers = async () => {
    if (mcpServersLoaded || mcpLoadingTriggered) return;
    
    setMcpLoadingTriggered(true);
    // 仅通知需要加载，由子组件实际发起请求
    setMcpServersLoaded(true);
  };

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
    setMcpServersLoaded(false);
    setMcpLoadingTriggered(false);
    
    // 只在以下情况加载MCP服务器数据：
    // A. 当前ChatPreset拥有至少一个MCP时
    if (isOpen && shouldLoadMcpOnInit(chatPreset)) {
      loadMcpServers();
    }
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
            codeExecutionEnabled: false,
            imageSize: null,
            mcps: [],
          });
          return s;
        }
        return span;
      });
    });
  };

  const handleSave = async () => {
    if (!name?.trim()) {
      toast.error(t('Please enter a name'));
      return;
    }

    // 验证所有spans的MCP工具设置
    for (const span of spans) {
      if (span.mcps && span.mcps.length > 0) {
        for (const mcp of span.mcps) {
          // 验证工具名是否为空
          if (!mcp.id || mcp.id === 0) {
            toast.error(t('All MCP tools must have a valid tool name'));
            return;
          }
          
          // 验证自定义headers是否为有效的JSON
          if (mcp.customHeaders && !validateJSON(mcp.customHeaders)) {
            toast.error(t('Invalid JSON format in MCP custom headers'));
            return;
          }
        }
      }
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
        codeExecutionEnabled: !!span.codeExecutionEnabled,
        imageSize: span.imageSize,
        mcps: span.mcps,
      })),
    };

    setIsLoading(true);
    try {
      if (chatPreset) {
        await putChatPreset(chatPreset.id, params);
      } else {
        await postChatPreset(params);
      }
      onClose();
    } catch (error) {
      console.error('Failed to save chat preset:', error);
      toast.error(t('Failed to save preset'));
    } finally {
      setIsLoading(false);
    }
  };

  const handleAddChatModel = async (modelId: number) => {
    const m = modelMap[modelId];
    const count = presetSpanCount + 1;
    setPresetSpanCount(count);
    const span: ChatSpanDto = {
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
      codeExecutionEnabled: false,
      imageSize: null,
      mcps: [],
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

  const onChangeCodeExecution = (value: boolean) => {
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            codeExecutionEnabled: value,
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

  const onChangeImageQuality = (value: string) => {
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

  const onChangeImageSize = (value: string | null) => {
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            imageSize: value,
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

  const onChangeMcps = (mcps: ChatSpanMcp[]) => {
    setSpans((prev) => {
      return prev.map((span) => {
        if (selectedSpan?.spanId === span.spanId) {
          const s = {
            ...span!,
            mcps,
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
                    <ModelProviderIcon providerId={span.modelProviderId} />
                    {span.modelName}
                  </div>
                ))}
                {selectedChat && selectedChat.spans.length < MAX_SELECT_MODEL_COUNT && (
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
                              <ModelProviderIcon
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
                    
                    {/* 根据模型的 API 类型显示不同的配置组件 */}
                    {modelMap[selectedSpan.modelId] && (
                      <>
                        {/* Chat/Response API 配置 (apiType=0/1) */}
                        {(modelMap[selectedSpan.modelId].apiType === 0 || 
                          modelMap[selectedSpan.modelId].apiType === 1) && (
                          <ChatResponsePresetConfig
                            model={modelMap[selectedSpan.modelId]}
                            systemPrompt={selectedSpan.systemPrompt}
                            prompts={prompts}
                            webSearchEnabled={selectedSpan.webSearchEnabled}
                            codeExecutionEnabled={selectedSpan.codeExecutionEnabled}
                            reasoningEffort={selectedSpan.reasoningEffort}
                            mcps={selectedSpan.mcps || []}
                            temperature={selectedSpan.temperature}
                            maxOutputTokens={selectedSpan.maxOutputTokens}
                            mcpServersLoaded={mcpServersLoaded}
                            onChangePromptText={onChangePromptText}
                            onChangePrompt={onChangePrompt}
                            onChangeEnableSearch={onChangeEnableSearch}
                            onChangeCodeExecution={onChangeCodeExecution}
                            onChangeReasoningEffort={onChangeReasoningEffort}
                            onChangeMcps={onChangeMcps}
                            onChangeTemperature={onChangeTemperature}
                            onChangeMaxOutputTokens={onChangeMaxOutputTokens}
                            onRequestMcpLoad={loadMcpServers}
                          />
                        )}
                        
                        {/* ImageGeneration API 配置 (apiType=2) */}
                        {modelMap[selectedSpan.modelId].apiType === 2 && (
                          <ImageGenerationPresetConfig
                            model={modelMap[selectedSpan.modelId]}
                            imageSize={selectedSpan.imageSize}
                            reasoningEffort={selectedSpan.reasoningEffort}
                            maxOutputTokens={selectedSpan.maxOutputTokens}
                            onChangeImageSize={onChangeImageSize}
                            onChangeImageQuality={onChangeImageQuality}
                            onChangeMaxOutputTokens={onChangeMaxOutputTokens}
                          />
                        )}
                      </>
                    )}
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
              disabled={presetSpanCount === 0 || isLoading}
              onClick={() => {
                handleSave();
              }}
            >
              {isLoading ? t('Saving...') : t('Save')}
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default ChatPresetModal;
