import { useContext, useEffect, useState } from 'react';
import { DndContext, DragEndEvent, PointerSensor, useSensor, useSensors, closestCorners } from '@dnd-kit/core';
import { SortableContext, rectSortingStrategy, arrayMove, useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

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

import { setChats } from '@/actions/chat.actions';
import HomeContext from '@/contexts/home.context';
import ChatPresetModal from './ChatPresetModal';

import {
  deleteChatPreset,
  getChatPreset,
  postApplyChatPreset,
  postCloneChatPreset,
  reorderChatPresets,
} from '@/apis/clientApis';
import { cn } from '@/lib/utils';

interface SortableChatPresetItemProps {
  item: GetChatPresetResult;
  selectedChatPresetId: string;
  modelMap: any;
  onSelect: (item: GetChatPresetResult) => void;
  onEdit: (item: GetChatPresetResult) => void;
  onClone: (id: string) => void;
  onDelete: (id: string) => void;
  t: (key: string) => string;
}

const SortableChatPresetItem = ({
  item,
  selectedChatPresetId,
  modelMap,
  onSelect,
  onEdit,
  onClone,
  onDelete,
  t,
}: SortableChatPresetItemProps) => {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: item.id });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  return (
    <div
      ref={setNodeRef}
      style={style}
      {...attributes}
      {...listeners}
      className={cn(
        'rounded-sm p-4 h-24 md:h-32 hover:bg-muted cursor-grab active:cursor-grabbing shadow-sm bg-card',
        selectedChatPresetId === item.id && 'bg-muted',
        isDragging && 'opacity-50'
      )}
      onClick={() => onSelect(item)}
    >
      <div className="flex justify-between">
        <div className="flex items-center gap-2">
          <span className="text-ellipsis whitespace-nowrap overflow-hidden">
            {item.name}
          </span>
        </div>
        <span>
          <DropdownMenu>
            <DropdownMenuTrigger 
              className="focus:outline-none p-[6px]"
              onPointerDown={(e) => e.stopPropagation()}
              onClick={(e) => e.stopPropagation()}
            >
              <IconDots className="hover:opacity-50" size={16} />
            </DropdownMenuTrigger>
            <DropdownMenuContent className="w-42 border-none">
              <DropdownMenuItem
                className="flex justify-start gap-3"
                onClick={(e) => {
                  e.stopPropagation();
                  onEdit(item);
                }}
              >
                <IconPencil size={18} />
                {t('Edit')}
              </DropdownMenuItem>
              <DropdownMenuItem
                className="flex justify-start gap-3"
                onClick={(e) => {
                  e.stopPropagation();
                  onClone(item.id);
                }}
              >
                <IconCopy size={18} />
                {t('Clone')}
              </DropdownMenuItem>
              <DropdownMenuItem
                className="flex justify-start gap-3"
                onClick={(e) => {
                  e.stopPropagation();
                  onDelete(item.id);
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
};

const ChatPresetList = () => {
  const {
    hasModel,
    chatDispatch,
    state: { chats, modelMap },
    selectedChat,
  } = useContext(HomeContext);
  const [chatPresets, setChatPresets] = useState<GetChatPresetResult[]>([]);
  const [chatPreset, setChatPreset] = useState<GetChatPresetResult>();
  const [selectedChatPresetId, setSelectedChatPresetId] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const { t } = useTranslation();

  // dnd-kit sensors
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 5,
      },
    })
  );

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
    if (!selectedChat || item.spans.length === 0) return;
    
    setSelectedChatPresetId(item.id);
    postApplyChatPreset(selectedChat.id, item.id).then(() => {
      const updatedChats = chats.map((c) => {
        if (c.id === selectedChat.id) {
          return { ...c, spans: item.spans };
        }
        return c;
      });
      chatDispatch(setChats(updatedChats));
    });
  };

  const onDragEnd = async (event: DragEndEvent) => {
    const { active, over } = event;
    
    if (!over || active.id === over.id) {
      return;
    }

    const activeIndex = chatPresets.findIndex((preset) => preset.id === active.id);
    const overIndex = chatPresets.findIndex((preset) => preset.id === over.id);

    if (activeIndex !== -1 && overIndex !== -1) {
      try {
        // 1) 乐观更新，先本地移动，避免回弹闪烁
        const newPresets = arrayMove(chatPresets, activeIndex, overIndex);
        setChatPresets(newPresets);

        // 2) 基于新顺序计算前后邻居
        const movedId = active.id as string;
        const newIdx = newPresets.findIndex(p => p.id === movedId);
        const previousId = newIdx > 0 ? newPresets[newIdx - 1].id : null;
        const nextId = newIdx < newPresets.length - 1 ? newPresets[newIdx + 1].id : null;

        // 3) 通知后端；失败则回滚（重新拉取）
        await reorderChatPresets({ sourceId: movedId, previousId, nextId });
      } catch (error) {
        console.error('Failed to reorder chat presets:', error);
        // 如果后端调用失败，刷新列表以恢复正确顺序
        getChatPresetList();
      }
    }
  };

  return (
    <div className={cn('px-0 md:px-8 pt-6')}>
      {hasModel() && (
        <DndContext
          sensors={sensors}
          onDragEnd={onDragEnd}
          collisionDetection={closestCorners}
        >
          <div className="grid grid-cols-[repeat(auto-fit,minmax(120px,240px))] place-content-center gap-4 w-full">
            <SortableContext
              items={chatPresets.map(preset => preset.id)}
              strategy={rectSortingStrategy}
            >
              {chatPresets?.map((item) => (
                <SortableChatPresetItem
                  key={item.id}
                  item={item}
                  selectedChatPresetId={selectedChatPresetId}
                  modelMap={modelMap}
                  onSelect={handleSelectChatPreset}
                  onEdit={(item) => {
                    setChatPreset(item);
                    setIsOpen(true);
                  }}
                  onClone={handleCloneChatPreset}
                  onDelete={handleDeleteChatPreset}
                  t={t}
                />
              ))}
            </SortableContext>
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
        </DndContext>
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
