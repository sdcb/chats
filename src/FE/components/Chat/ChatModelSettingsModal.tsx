import { useCallback, useContext, useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';
import { DEFAULT_TEMPERATURE } from '@/types/chat';
import { ChatSpanDto, ChatSpanMcp } from '@/types/clientApis';
import { Prompt } from '@/types/prompt';

import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import ChatModelDropdownMenu from '@/components/ChatModelDropdownMenu/ChatModelDropdownMenu';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogTitle,
} from '@/components/ui/dialog';
import { Switch } from '@/components/ui/switch';

import { setChats } from '@/actions/chat.actions';
import HomeContext from '@/contexts/home.context';
import ChatModelInfo from './ChatModelInfo';
import ChatResponsePresetConfig from './ChatResponsePresetConfig';
import ImageGenerationPresetConfig from './ImageGenerationPresetConfig';

import { putChatSpan } from '@/apis/clientApis';

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
    state: { modelMap, prompts, models, chats },
    selectedChat,
    hasModel,
    chatDispatch,
  } = useContext(HomeContext);
  const [span, setSpan] = useState<ChatSpanDto>();
  const [model, setModel] = useState<AdminModelDto>();
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
  const shouldLoadMcpOnInit = (currentSpan?: ChatSpanDto) => {
    if (!currentSpan) return false;
    return currentSpan.mcps && currentSpan.mcps.length > 0;
  };

  // 加载MCP服务器数据
  const loadMcpServers = useCallback(() => {
    if (mcpServersLoaded || mcpLoadingTriggered) return;
    setMcpLoadingTriggered(true);
    setMcpServersLoaded(true);
  }, [mcpLoadingTriggered, mcpServersLoaded]);

  useEffect(() => {
    if (!selectedChat || !isOpen) return;
    
    const originalSpan = selectedChat.spans.find((x) => x.spanId === spanId);
    if (!originalSpan) {
      return;
    }
    const normalizedSpan: ChatSpanDto = {
      ...originalSpan,
      mcps: originalSpan.mcps || [],
      thinkingBudget: originalSpan.thinkingBudget ?? null,
    };
    setSpan(normalizedSpan);
    setModel(modelMap[normalizedSpan.modelId]);
    
    // 只在以下情况加载MCP服务器数据：
    // A. 当前span拥有至少一个MCP时
    if (shouldLoadMcpOnInit(normalizedSpan)) {
      loadMcpServers();
    }
  }, [isOpen, loadMcpServers, modelMap, selectedChat, spanId]);

  useEffect(() => {
    if (!isOpen) {
      setMcpServersLoaded(false);
      setMcpLoadingTriggered(false);
      return;
    }
    setMcpServersLoaded(false);
    setMcpLoadingTriggered(false);
  }, [isOpen, spanId]);

  const { t } = useTranslation();

  const onChangeModel = (model: AdminModelDto) => {
    setModel(modelMap[model?.modelId]);
    const nextThinkingBudget = (() => {
      if (!span) return null;
      if (model.maxThinkingBudget === null) {
        return null;
      }
      if (span.thinkingBudget === null) {
        return null;
      }
      return Math.min(span.thinkingBudget, model.maxThinkingBudget);
    })();
    setSpan({
      ...span!,
      modelId: model.modelId,
      modelName: model.name,
      modelProviderId: model.modelProviderId,
      thinkingBudget: nextThinkingBudget,
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

  const onChangeCodeExecution = (value: boolean) => {
    setSpan({ ...span!, codeExecutionEnabled: value });
  };

  const onChangeReasoningEffort = (value: string) => {
    setSpan({ ...span!, reasoningEffort: Number(value) });
  };

  const onChangeImageQuality = (value: string) => {
    setSpan({ ...span!, reasoningEffort: Number(value) });
  };

  const onChangeImageSize = (value: string | null) => {
    setSpan({ ...span!, imageSize: value });
  };

  const onChangeThinkingBudget = (value: number | null) => {
    setSpan({ ...span!, thinkingBudget: value });
  };

  const onChangeMcps = (mcps: ChatSpanMcp[]) => {
    setSpan({ ...span!, mcps });
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

  const handleSave = async () => {
    if (!span || !selectedChat) return;

    // 验证MCP工具设置
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

    setIsLoading(true);
    try {
      await putChatSpan(span.spanId, selectedChat.id, {
        enabled: span.enabled,
        modelId: span.modelId,
        systemPrompt: span.systemPrompt,
        maxOutputTokens: span?.maxOutputTokens ?? null,
        temperature: span?.temperature ?? null,
        reasoningEffort: span.reasoningEffort,
        webSearchEnabled: !!span.webSearchEnabled,
        codeExecutionEnabled: !!span.codeExecutionEnabled,
        imageSize: span.imageSize ?? null,
        thinkingBudget: span.thinkingBudget ?? null,
        mcps: span.mcps,
      });
      
      const updatedSpans = selectedChat.spans.map((s) =>
        s.spanId === spanId ? { ...span } : s,
      );
      const updatedChat = { ...selectedChat, spans: updatedSpans };
      const updatedChats = chats.map((chat) =>
        chat.id === selectedChat.id ? updatedChat : chat
      );
      
      chatDispatch(setChats(updatedChats));
      onClose();
    } catch (error) {
      console.error('Failed to save chat span:', error);
      toast.error(t('Failed to save settings'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="w-full sm:w-[560px] max-h-[90vh] flex flex-col p-0 gap-0 overflow-hidden">
        <DialogTitle></DialogTitle>
        {span && model && hasModel() && (
          <div className="flex-1 overflow-y-auto p-4 mt-5">
            <div className="space-y-3 rounded-lg">
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
                      <ModelProviderIcon providerId={span.modelProviderId} />
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
              
              {/* 根据模型的 API 类型显示不同的配置组件 */}
              {model && (
                <>
                  {/* Chat/Response/AnthropicMessages API 配置 (apiType=0/1/3) */}
                  {(model.apiType === 0 || model.apiType === 1 || model.apiType === 3) && (
                    <ChatResponsePresetConfig
                      model={model}
                      systemPrompt={span.systemPrompt}
                      prompts={prompts}
                      webSearchEnabled={span.webSearchEnabled}
                      codeExecutionEnabled={span.codeExecutionEnabled}
                      reasoningEffort={span.reasoningEffort}
                      thinkingBudget={span.thinkingBudget}
                      mcps={span.mcps || []}
                      temperature={span.temperature}
                      maxOutputTokens={span.maxOutputTokens}
                      mcpServersLoaded={mcpServersLoaded}
                      onChangePromptText={onChangePromptText}
                      onChangePrompt={onChangePrompt}
                      onChangeEnableSearch={onChangeEnableSearch}
                      onChangeCodeExecution={onChangeCodeExecution}
                      onChangeReasoningEffort={onChangeReasoningEffort}
                      onChangeThinkingBudget={onChangeThinkingBudget}
                      onChangeMcps={onChangeMcps}
                      onChangeTemperature={onChangeTemperature}
                      onChangeMaxOutputTokens={onChangeMaxOutputTokens}
                      onRequestMcpLoad={loadMcpServers}
                    />
                  )}
                  
                  {/* ImageGeneration API 配置 (apiType=2) */}
                  {model.apiType === 2 && (
                    <ImageGenerationPresetConfig
                      model={model}
                      imageSize={span.imageSize}
                      reasoningEffort={span.reasoningEffort}
                      maxOutputTokens={span.maxOutputTokens}
                      onChangeImageSize={onChangeImageSize}
                      onChangeImageQuality={onChangeImageQuality}
                      onChangeMaxOutputTokens={onChangeMaxOutputTokens}
                    />
                  )}
                </>
              )}
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
              disabled={isLoading}
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

export default ChatModelSettingModal;
