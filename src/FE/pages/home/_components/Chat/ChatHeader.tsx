import { useContext, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

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
  putUserChatSpan,
} from '@/apis/clientApis';
import { cn } from '@/lib/utils';

const ChatHeader = () => {
  const { t } = useTranslation();
  const MAX_SELECT_MODEL_COUNT = 10;
  const {
    state: {
      selectedChat,
      models,

      defaultPrompt,
      showChatBar,
    },
    chatDispatch,
  } = useContext(HomeContext);

  const [selectedSpanId, setSelectedSpanId] = useState<number | null>(null);

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
    await putUserChatSpan(selectedChat.id, spanId, { modelId }).then((data) => {
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
    });
  };

  const AddBtnRender = () => (
    <div className="flex items-center">
      {selectedChat.spans.length < MAX_SELECT_MODEL_COUNT && (
        <ChatModelDropdownMenu
          className="p-0"
          triggerClassName={'hover:bg-transparent p-0'}
          models={models}
          content={
            <Button variant="ghost" className="bg-muted">
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
      <div className="sticky top-0 z-10 text-sm bg-background right-0">
        <div className="flex justify-between items-center w-full">
          <div
            className={cn(
              'flex justify-start ml-24 h-12 items-center',
              showChatBar && 'ml-6',
            )}
          >
            <div className="flex gap-2 items-center overflow-x-auto max-w-[calc(100vw-98px)]">
              {selectedChat.spans.map((span) => (
                <div
                  className="flex bg-muted rounded-md h-10"
                  key={'chat-header-' + span.spanId}
                >
                  <TooltipProvider>
                    <Tooltip delayDuration={100}>
                      <TooltipTrigger asChild>
                        <Button
                          variant="ghost"
                          className={cn('h-auto p-4 sm:p-2 m-0 gap-2', !span.enabled && 'opacity-50')}
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
                      </TooltipTrigger>
                      <TooltipContent
                        sideOffset={-48}
                        side="right"
                        className="flex items-center border-none gap-2"
                      >
                        <Button variant="ghost" className="w-6 h-6 p-0 m-0">
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
              ))}
              <AddBtnRender />
            </div>
          </div>
        </div>
      </div>
      <ChatModelSettingModal
        spanId={selectedSpanId!}
        isOpen={selectedSpanId !== null}
        onRemove={handleRemoveChatModel}
        onClose={() => {
          setSelectedSpanId(null);
        }}
      />
    </>
  );
};

export default ChatHeader;
