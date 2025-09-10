import React from 'react';
import { AdminModelDto, GetModelKeysResult } from '@/types/adminApis';
import { Button } from '@/components/ui/button';
import { IconPlus, IconPencil, IconBolt } from '@/components/Icons';
import DeletePopover from '@/pages/home/_components/Popover/DeletePopover';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import ModelItem from './ModelItem';
import CollapsiblePanel from './CollapsiblePanel';

interface ModelKeyProps {
  modelKey: GetModelKeysResult;
  models: AdminModelDto[];
  expanded: boolean;
  onToggleExpand: () => void;
  onEdit: (key: GetModelKeysResult) => void;
  onDelete: (keyId: number) => void;
  onConfigModels: (keyId: number) => void;
  onAddModel: (keyId: number) => void;
  onEditModel: (model: AdminModelDto) => void;
  onDeleteModel: (modelId: number) => void;
  // Drag state
  isDragging?: boolean;
  onDragStart?: (e: React.DragEvent<HTMLDivElement>) => void;
  onDragEnd?: (e: React.DragEvent<HTMLDivElement>) => void;
  // Droppable handlers (optional, when used as drop target)
  onDragOver?: (e: React.DragEvent<HTMLDivElement>) => void;
  onDragEnter?: (e: React.DragEvent<HTMLDivElement>) => void;
  onDrop?: (e: React.DragEvent<HTMLDivElement>) => void;
}

export default function ModelKey({
  modelKey,
  models,
  expanded,
  onToggleExpand,
  onEdit,
  onDelete,
  onConfigModels,
  onAddModel,
  onEditModel,
  onDeleteModel,
  isDragging = false,
  onDragStart,
  onDragEnd,
  onDragOver,
  onDragEnter,
  onDrop,
}: ModelKeyProps) {
  const { t } = useTranslation();

  const handleClick = (e: React.MouseEvent<HTMLDivElement>) => {
    // 如果正在拖拽，不处理点击事件
    if (isDragging) {
      e.preventDefault();
      return;
    }
    
    // 如果点击的是按钮区域，不触发展开/收起
    const target = e.target as HTMLElement;
    if (target.closest('button')) {
      return;
    }
    
    onToggleExpand();
  };

  const handleDragStart = (e: React.DragEvent<HTMLDivElement>) => {
    // 如果拖拽开始于按钮区域，阻止拖拽
    const target = e.target as HTMLElement;
    if (target.closest('button')) {
      e.preventDefault();
      return;
    }
    
    // 确保浏览器识别为“可拖拽移动”操作
    try {
      e.dataTransfer.effectAllowed = 'move';
      // 一些浏览器（如 Firefox）要求必须设置数据才会触发 drop
      e.dataTransfer.setData('text/plain', String(modelKey.id));
    } catch {}

    if (onDragStart) {
      onDragStart(e);
    }

  // 防止冒泡到 Provider 导致 Provider 级拖拽被触发
  e.stopPropagation();
  };

  const handleDragEnd = (e: React.DragEvent<HTMLDivElement>) => {
    // 结束拖拽时确保状态被清理
    onDragEnd?.(e as any);
  };

  return (
    <div 
      className={cn(
        "rounded-md border bg-background/30 transition-all duration-200 cursor-move",
        isDragging && "opacity-50 transform scale-95"
      )}
      draggable={true}
  onDragStart={handleDragStart}
  onDragEnd={handleDragEnd}
  onDragOver={onDragOver}
  onDragEnter={onDragEnter}
  onDrop={onDrop}
    >
      <div
        className="flex items-center justify-between p-3 cursor-pointer select-none"
        onClick={handleClick}
      >
        <div className="flex items-center gap-2">
          <span className="font-medium">{modelKey.name}</span>
        </div>
        <div className="flex gap-2">
          <Button
            variant="secondary"
            size="sm"
            onClick={(e) => {
              e.stopPropagation();
              onConfigModels(modelKey.id);
            }}
            title={t('Fast Add Models')}
          >
            <IconBolt size={16} />
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={(e) => {
              e.stopPropagation();
              onAddModel(modelKey.id);
            }}
            title={t('Add Model')}
          >
            <IconPlus size={16} />
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={(e) => {
              e.stopPropagation();
              onEdit(modelKey);
            }}
            title={t('Edit')}
          >
            <IconPencil size={16} />
          </Button>
          {models.length === 0 && (
            <div onClick={(e) => e.stopPropagation()} title={t('Delete')}>
              <DeletePopover onDelete={() => onDelete(modelKey.id)} />
            </div>
          )}
        </div>
      </div>
      <CollapsiblePanel open={expanded}>
        <div className="pr-3 pb-3 pl-4 space-y-1">
          {models.map((model) => (
            <ModelItem
              key={model.modelId}
              model={model}
              onEdit={onEditModel}
              onDelete={onDeleteModel}
            />
          ))}
        </div>
      </CollapsiblePanel>
    </div>
  );
}