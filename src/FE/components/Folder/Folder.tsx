import {
  DragEvent,
  KeyboardEvent,
  ReactElement,
  useEffect,
  useState,
} from 'react';

import useTranslation from '@/hooks/useTranslation';

import { UngroupedChatName } from '@/types/chat';
import { IChatGroup } from '@/types/group';

import SidebarActionButton from '../Button/SidebarActionButton';
import {
  IconCheck,

  IconChevronRight,
  IconDots,
  IconPencil,
  IconPlus,
  IconTrash,
  IconX,
} from '../Icons';
import { Button } from '../ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '../ui/dropdown-menu';
import { Input } from '../ui/input';

interface Props {
  currentFolder: IChatGroup;
  defaultOpen?: boolean;
  showActions?: boolean;
  folderComponent: ReactElement | undefined;
  onRenameGroup?: (id: string, value: string) => void;
  onDeleteGroup?: (id: string) => void;
  onClickGroup?: (group: IChatGroup) => void;
  onDragStart?: (e: DragEvent<HTMLButtonElement>, group: IChatGroup) => void;
  onNewGroupChat?: (groupId: string) => void;
}

const Folder = ({
  currentFolder,
  showActions = true,
  defaultOpen = false,
  folderComponent,
  onRenameGroup,
  onDeleteGroup,
  onClickGroup,
  onDragStart,
  onNewGroupChat,
}: Props) => {
  const { t } = useTranslation();

  const [isDeleting, setIsDeleting] = useState(false);
  const [isRenaming, setIsRenaming] = useState(false);
  const [renameValue, setRenameValue] = useState('');
  const [isOpen, setIsOpen] = useState(defaultOpen);

  useEffect(() => {
    setIsOpen(defaultOpen);
  }, [defaultOpen]);

  useEffect(() => {
    if (isRenaming) {
      setIsDeleting(false);
    } else if (isDeleting) {
      setIsRenaming(false);
    }
  }, [isRenaming, isDeleting]);

  const handleEnterDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      handleRename();
    }
  };

  const handleRename = () => {
    onRenameGroup && onRenameGroup(currentFolder.id, renameValue);
    setRenameValue('');
    setIsRenaming(false);
  };

  const handleClickFolder = () => {
    setIsOpen(!isOpen);
    onClickGroup && onClickGroup(currentFolder);
  };

  const handleDragStart = (e: DragEvent<HTMLButtonElement>) => {
    onDragStart && onDragStart(e, currentFolder);
  };

  return (
    <>
      <div className="relative flex items-center">
        {isRenaming ? (
          <div className="flex h-10 w-full items-center gap-2 rounded-lg bg-background p-3">
            <Input
              className="mr-12 flex-1 overflow-hidden overflow-ellipsis border-neutral-400 bg-transparent text-left text-[12.5px] leading-3 outline-none border-none text-black dark:text-white"
              type="text"
              value={renameValue}
              onChange={(e) => setRenameValue(e.target.value)}
              onKeyDown={handleEnterDown}
              autoFocus
            />
          </div>
        ) : (
          <Button
            draggable
            onDragStart={(e) => handleDragStart(e)}
            variant="ghost"
            className="flex w-full gap-3 rounded-lg p-2 pr-11"
            onClick={handleClickFolder}
          >
            <div 
              className="transition-transform duration-300 ease-in-out"
              style={{ transform: isOpen ? 'rotate(90deg)' : 'rotate(0deg)' }}
            >
              <IconChevronRight size={18} stroke="#6b7280" />
            </div>

            <div className="relative flex-1 overflow-hidden text-ellipsis whitespace-nowrap break-all text-left text-[12.5px] text-gray-500">
              {currentFolder.name === UngroupedChatName
                ? t('All chats')
                : currentFolder.name}
            </div>
          </Button>
        )}

        {(isDeleting || isRenaming) && (
          <div className="absolute right-1 z-10 flex text-gray-300">
            <SidebarActionButton
              handleClick={(e) => {
                e.stopPropagation();

                if (isDeleting) {
                  onDeleteGroup && onDeleteGroup(currentFolder.id);
                } else if (isRenaming) {
                  handleRename();
                }

                setIsDeleting(false);
                setIsRenaming(false);
              }}
            >
              <IconCheck size={18} />
            </SidebarActionButton>
            <SidebarActionButton
              handleClick={(e) => {
                e.stopPropagation();
                setIsDeleting(false);
                setIsRenaming(false);
              }}
            >
              <IconX size={18} />
            </SidebarActionButton>
          </div>
        )}

        {!isDeleting && !isRenaming && showActions && (
          <div className="absolute right-[0.6rem] z-10 flex text-gray-300">
            <DropdownMenu>
              <DropdownMenuTrigger className="focus:outline-none p-[6px]">
                <IconDots
                  className="hover:opacity-50"
                  size={16}
                  stroke="#6b7280"
                />
              </DropdownMenuTrigger>
              <DropdownMenuContent className="w-42 border-none">
                <DropdownMenuItem
                  className="flex justify-start gap-3"
                  onClick={() => {
                    onNewGroupChat && onNewGroupChat(currentFolder.id);
                  }}
                >
                  <IconPlus size={18} />
                  {t('New chat')}
                </DropdownMenuItem>
                <DropdownMenuItem
                  className="flex justify-start gap-3"
                  onClick={(e) => {
                    e.stopPropagation();
                    setIsRenaming(true);
                    setRenameValue(currentFolder.name);
                  }}
                >
                  <IconPencil size={18} />
                  {t('Edit')}
                </DropdownMenuItem>
                <DropdownMenuItem
                  className="flex justify-start gap-3"
                  onClick={(e) => {
                    e.stopPropagation();
                    setIsDeleting(true);
                  }}
                >
                  <IconTrash size={18} />
                  {t('Delete')}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        )}
      </div>

      {isOpen ? folderComponent : null}
    </>
  );
};

export default Folder;
