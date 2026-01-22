import {
  DragEvent,
  KeyboardEvent,
  MouseEventHandler,
  useContext,
  useEffect,
  useState,
} from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { currentISODateString } from '@/utils/date';

import { CHATS_SELECT_TYPE, ChatStatus, IChat } from '@/types/chat';

import SidebarActionButton from '@/components/Button/SidebarActionButton';
import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import Tips from '@/components/Tips/Tips';
import {
  IconArchive,
  IconCheck,
  IconDots,
  IconLoader,
  IconPencil,
  IconPin,
  IconPinnedOff,
  IconShare,
  IconTrash,
  IconX,
} from '@/components/Icons/index';
import { Checkbox } from '@/components/ui/checkbox';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

import { setChats } from '@/actions/chat.actions';
import HomeContext from '@/contexts/home.context';
import SharedMessageModal from '../Chat/SharedMessageModal';
import ChatbarContext from '../Chatbar/Chatbar.context';

import { deleteChats, putChats } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

interface Props {
  chat: IChat;
  onDragItemStart?: (e: DragEvent<HTMLElement>, chat: IChat) => void;
}

const ChatListItem = ({ chat, onDragItemStart }: Props) => {
  const { t } = useTranslation();
  const {
    state: {
      chats,
      chatsSelectType,
      selectedChatId,
    },
    selectedChat,
    chatDispatch,
    handleSelectChat,
    handleUpdateChat,
  } = useContext(HomeContext);

  const selectChatId = selectedChatId;
  const status = selectedChat?.status;
  const chatting = status === ChatStatus.Chatting;

  const { handleDeleteChat } = useContext(ChatbarContext);

  const [title, setTitle] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [isChanging, setTitleChanging] = useState(false);
  const [isShare, setIsShare] = useState(false);
  const [isArchive, setIsArchive] = useState(false);
  const [isConfirming, setIsConfirming] = useState(false);

  const handleEnterDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      selectChatId && handleChangeTitle(selectChatId);
    }
  };

  const handleChangeTitle = (chatId: string) => {
    if (title.trim().length > 0) {
      putChats(chatId, { title }).then(() => {
        handleUpdateChat(chats, chatId, { title });
        toast.success(t('Save successful'));
        setTitle('');
        setTitleChanging(false);
      });
    }
  };

  const handleDragStart = (e: DragEvent<HTMLElement>, chat: IChat) => {
    onDragItemStart && onDragItemStart(e, chat);
  };

  const handleConfirm: MouseEventHandler<HTMLButtonElement> = async (e) => {
    e.stopPropagation();
    setIsConfirming(true);
    try {
      if (isDeleting) {
        await deleteChats(chat.id);
        handleDeleteChat([chat.id]);
      } else if (isChanging) {
        handleChangeTitle(chat.id);
      } else if (isArchive) {
        await putChats(chat.id, { isArchived: true });
        handleDeleteChat([chat.id]);
      }
    } finally {
      setIsConfirming(false);
      setIsDeleting(false);
      setTitleChanging(false);
      setIsArchive(false);
    }
  };

  const handleCancel: MouseEventHandler<HTMLButtonElement> = (e) => {
    e.stopPropagation();
    setIsDeleting(false);
    setTitleChanging(false);
    setIsArchive(false);
  };

  const handleOpenChangeTitleModal: MouseEventHandler<HTMLDivElement> = (e) => {
    e.stopPropagation();
    setTitleChanging(true);
    selectChatId && setTitle(chat.title);
  };
  const handleOpenDeleteModal: MouseEventHandler<HTMLDivElement> = (e) => {
    e.stopPropagation();
    setIsDeleting(true);
  };
  const handleOpenShareModal: MouseEventHandler<HTMLDivElement> = (e) => {
    e.stopPropagation();
    setIsShare(true);
  };

  const handleSharedMessage = (isShared: boolean) => {
    handleUpdateChat(chats, selectChatId!, { isShared });
  };

  const handleOpenArchiveModal: MouseEventHandler<HTMLDivElement> = (e) => {
    e.stopPropagation();
    setIsArchive(true);
  };

  const handleChangeChatPin = (chatId: string, isPin: boolean = false) => {
    putChats(chatId, { isTopMost: isPin }).then(() => {
      chats.map((x) => {
        if (x.id === chatId) {
          x.isTopMost = isPin;
          x.updatedAt = currentISODateString();
        }
        return x;
      });
      chatDispatch(setChats(chats));
    });
  };

  const handleSelectByDeleteChat = (checked: boolean) => {
    const chatList = chats.map((c) =>
      c.id === chat.id ? { ...c, selected: checked } : { ...c },
    );
    chatDispatch(setChats(chatList));
  };

  useEffect(() => {
    if (isChanging) {
      setIsDeleting(false);
    } else if (isDeleting) {
      setTitleChanging(false);
    }
  }, [isChanging, isDeleting]);

  return (
    <div className="relative flex items-center rounded-lg">
      {isChanging && selectChatId === chat.id ? (
        <div className="flex w-full h-11 items-center gap-2 rounded-lg bg-background p-3">
          <input
            className="mr-12 flex-1 overflow-hidden overflow-ellipsis border-neutral-400 bg-transparent text-left text-[12.5px] leading-3 outline-none text-black dark:text-white"
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            onKeyDown={handleEnterDown}
            autoFocus
          />
        </div>
      ) : (
        <a
          href={`#/${chat.id}`}
          className={`flex w-full h-11 cursor-pointer items-center gap-2 rounded-lg px-2 transition-colors duration-200 hover:bg-muted no-underline ${
            chatting ? 'pointer-events-none cursor-not-allowed opacity-60' : ''
          } ${selectChatId === chat.id ? 'bg-muted' : ''}`}
          onClick={(e) => {
            if (chatting) {
              e.preventDefault();
              return;
            }
            e.preventDefault();
            handleSelectChat(chat);
          }}
          draggable
          onDragStart={(e) => handleDragStart(e, chat)}
        >
          <div
            className={cn(
              'group relative overflow-hidden transition-all duration-300 max-w-[20px] hover:max-w-[240px]',
              chatsSelectType !== CHATS_SELECT_TYPE.NONE && 'max-w-[20px]'
            )}
          >
            <div className="flex overflow-hidden">
              {chatsSelectType !== CHATS_SELECT_TYPE.NONE ? (
                <Checkbox
                  key={'chats-batch-delete-' + chat.id}
                  defaultChecked={!!chat?.selected}
                  onCheckedChange={(checked: boolean) => {
                    handleSelectByDeleteChat(checked);
                  }}
                  onClick={(e) => {
                    e.stopPropagation();
                  }}
                />
              ) : (
                chat.spans.map((span, index) => (
                  <div
                    key={'chat-icon-wrapper-' + span.spanId}
                    className={cn(
                      "flex-shrink-0 relative transition-opacity duration-200",
                      index > 0 && "-ml-2.5 group-hover:ml-[2px] opacity-0 group-hover:opacity-100"
                    )}
                    style={{ zIndex: chat.spans.length - index }}
                  >
                    <Tips
                      trigger={
                        <div>
                          <ModelProviderIcon
                            key={'chat-icon-' + span.spanId}
                            providerId={span.modelProviderId}
                          />
                        </div>
                      }
                      side="bottom"
                      content={span.modelName}
                    />
                  </div>
                ))
              )}
            </div>
          </div>

          <div
            className={`relative max-h-5 flex-1 overflow-hidden text-ellipsis whitespace-nowrap text-sm break-all text-left text-[12.5px] leading-4 ${
              selectChatId === chat.id ? 'pr-12' : 'pr-1'
            }`}
          >
            {chat.title}
          </div>
        </a>
      )}

      {(isDeleting || isChanging || isArchive) && selectChatId === chat.id && (
        <div className="absolute right-1 z-10 flex text-gray-300">
          <SidebarActionButton handleClick={handleConfirm} disabled={isConfirming}>
            {isConfirming ? (
              <IconLoader size={18} className="animate-spin" />
            ) : (
              <IconCheck size={18} />
            )}
          </SidebarActionButton>
          <SidebarActionButton handleClick={handleCancel} disabled={isConfirming}>
            <IconX size={18} />
          </SidebarActionButton>
        </div>
      )}

      {selectChatId === chat.id && !isDeleting && !isChanging && !isArchive && (
        <div className="absolute right-[0.6rem] z-10 flex text-gray-300">
          <DropdownMenu>
            <DropdownMenuTrigger
              disabled={chatting}
              className="focus:outline-none p-[6px]"
            >
              <IconDots className="hover:opacity-50" size={16} />
            </DropdownMenuTrigger>
            <DropdownMenuContent className="w-42 border-none">
              {chat.isTopMost ? (
                <DropdownMenuItem
                  className="flex justify-start gap-3"
                  onClick={() => {
                    handleChangeChatPin(chat.id);
                  }}
                >
                  <IconPinnedOff size={18} />
                  {t('UnPin')}
                </DropdownMenuItem>
              ) : (
                <DropdownMenuItem
                  hidden={chat.isTopMost}
                  className="flex justify-start gap-3"
                  onClick={() => {
                    handleChangeChatPin(chat.id, true);
                  }}
                >
                  <IconPin size={18} />
                  {t('Pin')}
                </DropdownMenuItem>
              )}
              <DropdownMenuItem
                className="flex justify-start gap-3"
                onClick={handleOpenChangeTitleModal}
              >
                <IconPencil size={18} />
                {t('Edit')}
              </DropdownMenuItem>
              <DropdownMenuItem
                className="flex justify-start gap-3"
                onClick={handleOpenShareModal}
              >
                <IconShare size={18} />
                {t('Share')}
              </DropdownMenuItem>
              {/* <DropdownMenuItem
                className="flex justify-start gap-3"
                onClick={handleOpenArchiveModal}
              >
                <IconArchive size={18} />
                {t('Archive')}
              </DropdownMenuItem> */}
              <DropdownMenuItem
                className="flex justify-start gap-3"
                onClick={handleOpenDeleteModal}
              >
                <IconTrash size={18} />
                {t('Delete')}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      )}

      {isShare && (
        <SharedMessageModal
          isOpen={isShare}
          onClose={() => {
            setIsShare(false);
          }}
          chat={chats.find((x) => x.id === selectChatId)!}
          onShareChange={handleSharedMessage}
        />
      )}
    </div>
  );
};

export default ChatListItem;
