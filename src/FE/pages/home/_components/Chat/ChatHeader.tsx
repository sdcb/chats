import { useContext, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { isMobile } from '@/utils/common';

import { ChatStatus, MAX_SELECT_MODEL_COUNT } from '@/types/chat';

import ChatIcon from '@/components/ChatIcon/ChatIcon';
import ChatModelDropdownMenu from '@/components/ChatModelDropdownMenu/ChatModelDropdownMenu';
import { IconDots, IconPlus, IconX } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

import { setSelectedChat } from '../../_actions/chat.actions';
import HomeContext from '../../_contexts/home.context';
import ChatModelSettingModal from './ChatModelSettingsModal';

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
    state: { selectedChat, models, defaultPrompt },
    chatDispatch,
  } = useContext(HomeContext);

  const [selectedSpanId, setSelectedSpanId] = useState<number | null>(null);
  const notSetSpanDisabled =
    selectedChat.spans.filter((x) => x.enabled).length === 1;

  const handleAddChatModel = async (modelId: number) => {
    await postUserChatSpan(selectedChat.id, { modelId }).then((data) => {
      selectedChat.spans.push({
        maxOutputTokens: data.maxOutputTokens,
        spanId: data.spanId,
        enabled: data.enabled,
        modelId: data.modelId,
        modelName: data.modelName,
        modelProviderId: data.modelProviderId,
        temperature: data.temperature,
        enableSearch: data.enableSearch,
        reasoningEffort: data?.reasoningEffort,
        systemPrompt: defaultPrompt?.content!,
      });
      chatDispatch(setSelectedChat(selectedChat));
    });
  };

  const handleRemoveChatModel = async (spanId: number) => {
    await deleteUserChatSpan(selectedChat.id, spanId).then(() => {
      selectedChat.spans = selectedChat.spans.filter(
        (s) => s.spanId !== spanId,
      );
      chatDispatch(setSelectedChat(selectedChat));
    });
  };

  const handleUpdateChatModel = async (spanId: number, modelId: number) => {
    await switchUserChatSpanModel(selectedChat.id, spanId, modelId).then(
      (data) => {
        selectedChat.spans = selectedChat.spans.map((s) => {
          if (s.spanId === spanId) {
            return {
              ...s,
              enabled: data.enabled,
              modelId: data.modelId,
              modelName: data.modelName,
              modelProviderId: data.modelProviderId,
              temperature: data.temperature,
              enableSearch: data.enableSearch,
            };
          }
          return s;
        });
        chatDispatch(setSelectedChat(selectedChat));
      },
    );
  };

  const AddBtnRender = () => (
    <div className="flex items-center">
      {selectedChat.spans.length < MAX_SELECT_MODEL_COUNT && (
        <ChatModelDropdownMenu
          className="p-0"
          triggerClassName={'hover:bg-transparent p-0'}
          models={models}
          content={
            <Button variant="ghost" className="bg-button">
              <IconPlus />
              {t('Add Model')}
            </Button>
          }
          hideIcon={true}
          onChangeModel={(model) => {
            handleAddChatModel(model.modelId);
          }}
        />
      )}
    </div>
  );

  const handleChangeChatSpan = (spanId: number, enable: boolean) => {
    if (notSetSpanDisabled && enable === false) {
      return;
    }
    if (enable) {
      postChatEnableSpan(spanId, selectedChat.id);
    } else {
      postChatDisableSpan(spanId, selectedChat.id);
    }
    selectedChat.spans = selectedChat.spans.map((s) => {
      if (s.spanId === spanId) {
        return {
          ...s,
          enabled: enable,
        };
      }
      return s;
    });
    chatDispatch(setSelectedChat(selectedChat));
  };

  return (
    <>
      <div className="absolute top-0 left-0 w-full border-transparent bg-background">
        <div className="stretch mt-2 flex flex-row mx-4 rounded-md">
          <div className="relative flex w-full flex-grow flex-col rounded-md bg-card shadow-[0_0_10px_rgba(0,0,0,0.10)] dark:shadow-[0_0_15px_rgba(0,0,0,0.10)]">
            <div
              className={cn(
                'flex justify-between select-none items-center custom-scrollbar overflow-x-auto',
              )}
            >
              <div className={cn('flex justify-start h-12 items-center')}>
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
                            'h-auto p-4 sm:p-2 m-0 gap-2',
                            !span.enabled && 'opacity-50',
                          )}
                          onClick={() => {
                            setSelectedSpanId(span.spanId);
                          }}
                        >
                          <ChatIcon providerId={span.modelProviderId} />
                          <span>{span?.modelName}</span>
                          <Button
                            variant="ghost"
                            className="w-6 h-6 p-0 m-0 hidden sm:block"
                          >
                            <IconDots className="rotate-90" size={16} />
                          </Button>
                        </Button>
                      ) : (
                        <div className="flex items-center bg-card rounded-md hover:bg-muted">
                          <div
                            className={cn(
                              'flex items-center pl-2',
                              !span.enabled && 'opacity-50',
                            )}
                          >
                            <ChatIcon providerId={span.modelProviderId} />
                            <ChatModelDropdownMenu
                              key={'change-model-' + span.modelId}
                              models={models}
                              modelName={span.modelName}
                              className="text-sm"
                              content={span?.modelName}
                              hideIcon={true}
                              onChangeModel={(model) => {
                                handleUpdateChatModel(
                                  span.spanId,
                                  model.modelId,
                                );
                              }}
                            />
                          </div>
                          <TooltipProvider>
                            <Tooltip delayDuration={100}>
                              <TooltipTrigger asChild>
                                <Button
                                  variant="ghost"
                                  className={cn(
                                    'h-auto p-4 sm:p-1 m-0 gap-2',
                                    !span.enabled && 'opacity-50',
                                  )}
                                  onClick={() => {
                                    setSelectedSpanId(span.spanId);
                                  }}
                                >
                                  <Button
                                    variant="ghost"
                                    className="w-6 h-6 p-0 m-0 hidden sm:block"
                                  >
                                    <IconDots className="rotate-90" size={16} />
                                  </Button>
                                </Button>
                              </TooltipTrigger>
                              <TooltipContent
                                sideOffset={-44}
                                side="right"
                                className="flex items-center border-none gap-2"
                              >
                                <Button
                                  variant="ghost"
                                  className="w-6 h-6 p-0 m-0"
                                >
                                  <IconDots
                                    className="rotate-90"
                                    size={16}
                                    onClick={() => {
                                      setSelectedSpanId(span.spanId);
                                    }}
                                  />
                                </Button>
                                <Switch
                                  onCheckedChange={(checked) => {
                                    handleChangeChatSpan(span.spanId, checked);
                                  }}
                                  checked={span.enabled}
                                ></Switch>
                                <Button
                                  disabled={selectedChat.spans.length === 1}
                                  onClick={() => {
                                    handleRemoveChatModel(span.spanId);
                                  }}
                                  variant="ghost"
                                  className="h-6 w-6 m-0 p-0 hover:bg-none"
                                >
                                  <IconX />
                                </Button>
                              </TooltipContent>
                            </Tooltip>
                          </TooltipProvider>
                        </div>
                      )}
                    </div>
                  ))}
                  <AddBtnRender />
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
    </>
  );
};

export default ChatHeader;
