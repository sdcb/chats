import { useContext, useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { MAX_CREATE_PRESET_CHAT_COUNT } from '@/types/chat';
import { GetChatPresetResult } from '@/types/clientApis';

import ChatIcon from '@/components/ChatIcon/ChatIcon';
import {
  IconCopy,
  IconDots,
  IconPencil,
  IconPlus,
  IconTrash,
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

import { setChats, setSelectedChat } from '../../_actions/chat.actions';
import HomeContext from '../../_contexts/home.context';
import ChatPresetModal from './ChatPresetModal';

import {
  deleteChatPreset,
  getChatPreset,
  postApplyChatPreset,
  postCloneChatPreset,
} from '@/apis/clientApis';
import { cn } from '@/lib/utils';

const ChatPresetList = () => {
  const {
    hasModel,
    chatDispatch,
    state: { selectedChat, chats, modelMap },
  } = useContext(HomeContext);
  const [chatPresets, setChatPresets] = useState<GetChatPresetResult[]>([]);
  const [chatPreset, setChatPreset] = useState<GetChatPresetResult>();
  const [selectedChatPresetId, setSelectedChatPresetId] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const { t } = useTranslation();

  const getChatPresetList = () => {
    getChatPreset().then((data) => {
      setChatPresets(data);
    });
  };

  useEffect(() => {
    getChatPresetList();
  }, []);

  const handleCreateChatPreset = () => {
    setChatPreset(undefined);
    setIsOpen(true);
  };

  const handleDeleteChatPreset = (id: string) => {
    deleteChatPreset(id).then(() => {
      getChatPresetList();
    });
  };

  const handleCloneChatPreset = (id: string) => {
    postCloneChatPreset(id).then(() => {
      getChatPresetList();
    });
  };

  const handleSelectChatPreset = (item: GetChatPresetResult) => {
    if (item.spans.length > 0) {
      setSelectedChatPresetId(item.id);
      postApplyChatPreset(selectedChat.id, item.id).then(() => {
        chatDispatch(
          setSelectedChat({
            ...selectedChat,
            spans: item.spans,
          }),
        );
        const chatList = chats.map((c) => {
          if (c.id === selectedChat.id) {
            c.spans = item.spans;
          }
          return c;
        });
        chatDispatch(setChats(chatList));
      });
    }
  };

  return (
    <div className={cn('px-0 md:px-8 pt-6')}>
      {hasModel() && (
        <div className="grid grid-cols-[repeat(auto-fit,minmax(144px,320px))] place-content-center gap-4 w-full">
          {chatPresets?.map((item) => {
            return (
              <div
                key={'chat-preset' + item.id}
                className={cn(
                  'rounded-sm p-4 h-24 md:h-32 hover:bg-muted cursor-pointer shadow-sm bg-card',
                  selectedChatPresetId === item.id && 'bg-muted',
                )}
                onClick={() => {
                  handleSelectChatPreset(item);
                }}
              >
                <div className="flex justify-between">
                  <span className="text-ellipsis whitespace-nowrap overflow-hidden">
                    {item.name}
                  </span>
                  <span>
                    <DropdownMenu>
                      <DropdownMenuTrigger className="focus:outline-none p-[6px]">
                        <IconDots className="hover:opacity-50" size={16} />
                      </DropdownMenuTrigger>
                      <DropdownMenuContent className="w-42 border-none">
                        <DropdownMenuItem
                          className="flex justify-start gap-3"
                          onClick={(e) => {
                            setChatPreset(item);
                            e.stopPropagation();
                            setIsOpen(true);
                          }}
                        >
                          <IconPencil size={18} />
                          {t('Edit')}
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          className="flex justify-start gap-3"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleCloneChatPreset(item.id);
                          }}
                        >
                          <IconCopy size={18} />
                          {t('Clone')}
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          className="flex justify-start gap-3"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleDeleteChatPreset(item.id);
                          }}
                        >
                          <IconTrash size={18} />
                          {t('Delete')}
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </span>
                </div>
                <div className="">
                  <div className="flex justify-end h-8 md:h-16 items-end">
                    {item.spans.map((s) => (
                      <TooltipProvider
                        delayDuration={100}
                        key={'span-tooltip-' + s.spanId}
                      >
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <Button className="bg-transparent p-0 m-0 h-auto hover:bg-transparent">
                              <ChatIcon
                                className={cn(
                                  'cursor-pointer border border-1 border-muted-foreground bg-white',
                                  !modelMap[s.modelId] && 'grayscale',
                                )}
                                key={'chat-icon-' + s.spanId}
                                providerId={s.modelProviderId}
                              />
                            </Button>
                          </TooltipTrigger>
                          <TooltipContent>{s.modelName}</TooltipContent>
                        </Tooltip>
                      </TooltipProvider>
                    ))}
                  </div>
                </div>
              </div>
            );
          })}
          {chatPresets.length < MAX_CREATE_PRESET_CHAT_COUNT && (
            <div
              className="rounded-sm px-4 flex justify-center items-center h-24 md:h-32 cursor-pointer hover:bg-muted bg-card"
              onClick={handleCreateChatPreset}
            >
              <IconPlus size={20} />
              {t('Add a preset model group')}
            </div>
          )}
        </div>
      )}
      <ChatPresetModal
        chatPreset={chatPreset}
        isOpen={isOpen}
        onClose={() => {
          getChatPresetList();
          setIsOpen(false);
        }}
      />
    </div>
  );
};

export default ChatPresetList;
