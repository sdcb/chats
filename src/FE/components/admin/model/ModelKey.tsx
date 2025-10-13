import React from 'react';
import { AdminModelDto, GetModelKeysResult } from '@/types/adminApis';
import { Button } from '@/components/ui/button';
import IconActionButton from '@/components/common/IconActionButton';
import { IconPlus, IconPencil, IconBolt, IconChartHistogram } from '@/components/Icons';
import DeletePopover from '@/components/Popover/DeletePopover';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import ModelItem from './ModelItem';
import CollapsiblePanel from './CollapsiblePanel';
// dnd-kit
import { useSortable, SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

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
  onGoToUsage: (params: {
    provider?: string;
    modelKey?: string;
    model?: string;
  }) => void;
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
  onGoToUsage,
}: ModelKeyProps) {
  const { t } = useTranslation();
  const sortable = useSortable({ id: `key-${modelKey.id}` });
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = sortable;
  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  const handleClick = (e: React.MouseEvent<HTMLDivElement>) => {
    // 如果正在拖拽，不处理点击事件
    if (isDragging) {
      e.preventDefault();
      return;
    }
    
    // 如果点击的是按钮区域，不触发展开/收起
    const target = e.target as HTMLElement;
    const btn = target.closest('button');
    // 允许带 data-allow-toggle 的按钮点击触发展开/收起（标题把手点击）
    if (btn && !btn.getAttribute('data-allow-toggle')) {
      return;
    }
    
    onToggleExpand();
  };

  // dnd-kit 管理拖拽，无需本地 dragStart/dragEnd

  return (
    <div 
      className={cn(
        "rounded-md border bg-background/30 transition-all duration-200 touch-pan-y",
        isDragging && "opacity-60"
      )}
      ref={setNodeRef}
      style={style}
    >
      <div
        className="flex items-center justify-between p-3 select-none"
        onClick={handleClick}
        {...attributes}
      >
        <div className="flex items-center gap-2">
          {/* 标题作为拖拽把手：只在标题上绑定 listeners，避免与滚动冲突；点击非拖拽时允许展开 */}
          <button
            className="p-0 m-0 bg-transparent border-0 inline-flex items-center font-medium touch-none cursor-grab active:cursor-grabbing"
            aria-label={t('Drag to reorder')}
            data-allow-toggle
            onClick={(e) => {
              if (isDragging) {
                e.preventDefault();
                e.stopPropagation();
              }
            }}
            {...listeners}
          >
            {modelKey.name}
          </button>
        </div>
        <div className="flex gap-2">
          <IconActionButton
            label={t('View Usage Records')}
            icon={<IconChartHistogram size={18} />}
            onClick={() => onGoToUsage({ modelKey: modelKey.name })}
          />
          <IconActionButton
            label={t('Fast Add Models')}
            icon={<IconBolt size={18} />}
            onClick={() => onConfigModels(modelKey.id)}
          />
          <IconActionButton
            label={t('Add Model')}
            icon={<IconPlus size={18} />}
            onClick={() => onAddModel(modelKey.id)}
          />
          <IconActionButton
            label={t('Edit')}
            icon={<IconPencil size={18} />}
            onClick={() => onEdit(modelKey)}
          />
          {models.length === 0 && (
            <div onClick={(e) => e.stopPropagation()} title={t('Delete')}>
              <DeletePopover onDelete={() => onDelete(modelKey.id)} />
            </div>
          )}
        </div>
      </div>
      <CollapsiblePanel open={expanded}>
        <div className="pr-3 pb-3 pl-4 space-y-1">
          {models.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-6 text-center">
              <div className="text-sm text-muted-foreground">{t('No models under this key')}</div>
              <div className="text-xs text-muted-foreground mt-1">{t('Add models to start using this key')}</div>
            </div>
          ) : (
            <SortableContext items={models.map(m => `model-${m.modelId}`)} strategy={verticalListSortingStrategy}>
              {models.map((model) => (
                <ModelItem
                  key={model.modelId}
                  model={model}
                  onEdit={onEditModel}
                  onDelete={onDeleteModel}
                  onGoToUsage={onGoToUsage}
                />
              ))}
            </SortableContext>
          )}
        </div>
      </CollapsiblePanel>
    </div>
  );
}