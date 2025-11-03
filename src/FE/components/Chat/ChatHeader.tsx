import { useContext, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { isMobile } from '@/utils/common';

import { ChatStatus, MAX_SELECT_MODEL_COUNT } from '@/types/chat';

import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import ChatModelDropdownMenu from '@/components/ChatModelDropdownMenu/ChatModelDropdownMenu';
import { IconDots, IconPlus, IconSettingsCog, IconX } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

import { setChats } from '@/actions/chat.actions';
import HomeContext from '@/contexts/home.context';
import ChatModelSettingModal from './ChatModelSettingsModal';
import ChatPresetResetDialog from './ChatPresetResetDialog';

import {
  deleteUserChatSpan,
  postChatDisableSpan,
  postChatEnableSpan,
  postUserChatSpan,
  switchUserChatSpanModel,
} from '@/apis/clientApis';
import { cn } from '@/lib/utils';

const ChatHeader = () => {
  const { t } = useTranslation();
  const {
    state: { models, defaultPrompt, showChatBar, chats },
    selectedChat,
    chatDispatch,
  } = useContext(HomeContext);

  const [selectedSpanId, setSelectedSpanId] = useState<number | null>(null);
  const [isResetDialogOpen, setIsResetDialogOpen] = useState(false);
  
  // 如果没有选中的聊天，返回空
  if (!selectedChat) {
    return null;
  }
  
  const notSetSpanDisabled =
    selectedChat.spans.filter((x) => x.enabled).length === 1;

  // 直接修改chats数组中的chat数据的辅助函数
  const updateChatInChats = (updatedChat: typeof selectedChat) => {
    const updatedChats = chats.map((chat) =>
      chat.id === selectedChat.id ? updatedChat : chat
    );
    chatDispatch(setChats(updatedChats));
  };

  const handleAddChatModel = async (modelId: number) => {
    await postUserChatSpan(selectedChat.id, { modelId }).then((spans) => {
      const updatedChat = {
        ...selectedChat,
        spans: spans.map(span => ({
          maxOutputTokens: span.maxOutputTokens,
          spanId: span.spanId,
          enabled: span.enabled,
          modelId: span.modelId,
          modelName: span.modelName,
          modelProviderId: span.modelProviderId,
          temperature: span.temperature,
          webSearchEnabled: span.webSearchEnabled,
          codeExecutionEnabled: span.codeExecutionEnabled,
          reasoningEffort: span?.reasoningEffort,
          systemPrompt: defaultPrompt?.content!,
          imageSize: span?.imageSize || null,
          mcps: span?.mcps || [],
        }))
      };
      updateChatInChats(updatedChat);
    });
  };

  const handleRemoveChatModel = async (spanId: number) => {
    await deleteUserChatSpan(selectedChat.id, spanId).then(() => {
      const updatedChat = {
        ...selectedChat,
        spans: selectedChat.spans.filter((s) => s.spanId !== spanId)
      };
      updateChatInChats(updatedChat);
    });
  };

  const handleUpdateChatModel = async (spanId: number, modelId: number) => {
    await switchUserChatSpanModel(selectedChat.id, spanId, modelId).then(
      (data) => {
        const updatedChat = {
          ...selectedChat,
          spans: selectedChat.spans.map((s) => {
            if (s.spanId === spanId) {
              return {
                ...s,
                enabled: data.enabled,
                modelId: data.modelId,
                modelName: data.modelName,
                modelProviderId: data.modelProviderId,
                temperature: data.temperature,
                webSearchEnabled: data.webSearchEnabled,
                codeExecutionEnabled: data.codeExecutionEnabled,
              };
            }
            return s;
          })
        };
        updateChatInChats(updatedChat);
      },
    );
  };

  const handleChangeChatSpan = (spanId: number, enable: boolean) => {
    if (notSetSpanDisabled && enable === false) {
      return;
    }
    if (enable) {
      postChatEnableSpan(spanId, selectedChat.id);
    } else {
      postChatDisableSpan(spanId, selectedChat.id);
    }
    const updatedChat = {
      ...selectedChat,
      spans: selectedChat.spans.map((s) => {
        if (s.spanId === spanId) {
          return {
            ...s,
            enabled: enable,
          };
        }
        return s;
      })
    };
    updateChatInChats(updatedChat);
  };

  const AddBtnRender = () => {
    if (selectedChat.spans.length >= MAX_SELECT_MODEL_COUNT) {
      return null;
    }

    return (
      <div className="flex items-center ml-2">
        <TooltipProvider delayDuration={0}>
          <Tooltip>
            <TooltipTrigger asChild>
              <div>
                <ChatModelDropdownMenu
                  className="p-0"
                  triggerClassName={'hover:bg-transparent p-2 bg-button hover:bg-accent'}
                  models={models}
                  content={<IconPlus size={16} />}
                  hideIcon={true}
                  onChangeModel={(model) => {
                    handleAddChatModel(model.modelId);
                  }}
                />
              </div>
            </TooltipTrigger>
            <TooltipContent>
              {t('Add Model')}
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      </div>
    );
  };

  const ResetBtnRender = () => {
    // 只在有消息时显示重设按钮
    const hasMessages = selectedChat.leafMessageId && selectedChat.leafMessageId.trim() !== '';
    
    if (!hasMessages) {
      return null;
    }

    return (
      <div className="flex items-center ml-2">
        <Tips
          trigger={
            <Button
              variant="ghost"
              className="p-2 h-auto hover:bg-accent"
              onClick={() => setIsResetDialogOpen(true)}
            >
              <IconSettingsCog size={16} />
            </Button>
          }
          content={t('Reset Models')}
        />
      </div>
    );
  };

  return (
    <>
      <div className="absolute top-0 left-0 w-full border-transparent bg-background">
        <div
          className={cn(
            'stretch mt-2 flex flex-row mx-4 rounded-md',
            !showChatBar && 'mx-2',
          )}
        >
          <div className="relative flex w-full flex-grow flex-col rounded-md bg-card shadow-[0_0_10px_rgba(0,0,0,0.10)] dark:shadow-[0_0_15px_rgba(0,0,0,0.10)] overflow-hidden">
            <div
              className={cn(
                'flex justify-between select-none items-center custom-scrollbar overflow-x-auto px-3',
              )}
            >
              <div
                className={cn(
                  'flex justify-start h-12 items-center',
                  !showChatBar && 'pl-16',
                )}
              >
                <div
                  className={cn(
                    'flex gap-2 items-center',
                    selectedChat.status === ChatStatus.Chatting &&
                      'pointer-events-none',
                  )}
                >
                  {selectedChat.spans.map((span) => (
                    <div
                      className="flex bg-card rounded-md h-10 flex-shrink-0"
                      key={'chat-header-' + span.spanId}
                    >
                      {isMobile() ? (
                        <Button
                          variant="ghost"
                          className={cn(
                            'h-auto p-1 m-0 gap-1',
                            !span.enabled && 'opacity-50',
                          )}
                          onClick={() => {
                            setSelectedSpanId(span.spanId);
                          }}
                        >
                          <ModelProviderIcon providerId={span.modelProviderId} />
                          <span className="font-mono">{span?.modelName}</span>
                        </Button>
                      ) : (
                        <div 
                          className="flex items-center bg-card rounded-md hover:bg-muted overflow-hidden transition-all duration-300 ease-in-out group"
                          onMouseEnter={(e) => {
                            const target = e.currentTarget;
                            target.style.width = 'auto';
                          }}
                          onMouseLeave={(e) => {
                            const target = e.currentTarget;
                            target.style.width = '';
                          }}
                        >
                          <ChatModelDropdownMenu
                            key={'change-model-' + span.modelId}
                            models={models}
                            modelName={span.modelName}
                            className="text-sm"
                            triggerClassName="flex items-center pl-2 hover:bg-transparent"
                            content={
                              <div
                                className={cn(
                                  'flex items-center gap-1',
                                  !span.enabled && 'opacity-50',
                                )}
                              >
                                <ModelProviderIcon providerId={span.modelProviderId} />
                                <span className="font-mono">{span?.modelName}</span>
                              </div>
                            }
                            hideIcon={true}
                            onChangeModel={(model) => {
                              handleUpdateChatModel(
                                span.spanId,
                                model.modelId,
                              );
                            }}
                          />
                          <div className="flex items-center">
                            <Button
                              variant="ghost"
                              className={cn(
                                'h-auto p-1 m-0 gap-2 hidden sm:flex',
                                !span.enabled && 'opacity-50',
                              )}
                              onClick={() => {
                                setSelectedSpanId(span.spanId);
                              }}
                            >
                              <div className="w-6 h-6 p-0 m-0 flex items-center justify-center cursor-pointer hover:bg-accent rounded-sm -ml-1">
                                <IconDots className="rotate-90" size={16} />
                              </div>
                            </Button>
                            <div className="flex items-center gap-2 max-w-0 group-hover:max-w-[200px] overflow-hidden transition-all duration-300 ease-in-out opacity-0 group-hover:opacity-100">
                              <Switch
                                onCheckedChange={(checked) => {
                                  handleChangeChatSpan(span.spanId, checked);
                                }}
                                checked={span.enabled}
                                className="ml-2"
                              ></Switch>
                              <Button
                                disabled={selectedChat.spans.length === 1}
                                onClick={() => {
                                  handleRemoveChatModel(span.spanId);
                                }}
                                variant="ghost"
                                className="h-6 w-6 m-0 p-0 hover:bg-accent"
                              >
                                <IconX size={16} />
                              </Button>
                            </div>
                          </div>
                        </div>
                      )}
                    </div>
                  ))}
                  <AddBtnRender />
                  <ResetBtnRender />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="z-10 text-sm bg-background"></div>
      <ChatModelSettingModal
        spanId={selectedSpanId!}
        isOpen={selectedSpanId !== null}
        onRemove={handleRemoveChatModel}
        notSetSpanDisabled={notSetSpanDisabled}
        onClose={() => {
          setSelectedSpanId(null);
        }}
      />
      <ChatPresetResetDialog
        isOpen={isResetDialogOpen}
        onClose={() => setIsResetDialogOpen(false)}
      />
    </>
  );
};

export default ChatHeader;
