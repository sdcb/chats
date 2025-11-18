import React from 'react';
import { AdminModelDto, GetModelKeysResult } from '@/types/adminApis';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import IconActionButton from '@/components/common/IconActionButton';
import { IconPlus, IconPencil, IconBolt, IconChartHistogram, IconKey } from '@/components/Icons';
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
  loading?: boolean; // 是否正在加载 models
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
  loading = false,
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
        className="flex items-center justify-between p-2 select-none"
        onClick={handleClick}
        {...attributes}
      >
        <div className="flex items-center gap-2">
          {/* 拖拽把手区域 */}
          <div
            className={cn(
              'flex-shrink-0 touch-none px-1 rounded hover:bg-muted/60 transition-colors cursor-grab active:cursor-grabbing'
            )}
            {...listeners}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="w-2.5 h-5 flex flex-col justify-center gap-0.5">
              <div className="w-full h-0.5 bg-muted-foreground/60 rounded-full" />
              <div className="w-full h-0.5 bg-muted-foreground/60 rounded-full" />
              <div className="w-full h-0.5 bg-muted-foreground/60 rounded-full" />
            </div>
          </div>
          
          {/* 钥匙图标 */}
          <IconKey size={18} className="text-muted-foreground flex-shrink-0" />
          
          {/* 标题 */}
          <span className="text-sm font-medium">{modelKey.name}</span>
        </div>
        <div className="flex gap-2">
          <IconActionButton
            label={t('View Usage Records')}
            icon={<IconChartHistogram size={16} />}
            className="h-5 w-5"
            onClick={() => onGoToUsage({ modelKey: modelKey.name })}
          />
          <IconActionButton
            label={t('Fast Add Models')}
            icon={<IconBolt size={16} />}
            className="h-5 w-5"
            onClick={() => onConfigModels(modelKey.id)}
          />
          <IconActionButton
            label={t('Add Model')}
            icon={<IconPlus size={16} />}
            className="h-5 w-5"
            onClick={() => onAddModel(modelKey.id)}
          />
          <IconActionButton
            label={t('Edit')}
            icon={<IconPencil size={16} />}
            className="h-5 w-5"
            onClick={() => onEdit(modelKey)}
          />
          {modelKey.totalModelCount === 0 && (
            <div onClick={(e) => e.stopPropagation()} title={t('Delete')}>
              <DeletePopover onDelete={() => onDelete(modelKey.id)} />
            </div>
          )}
        </div>
      </div>
      <CollapsiblePanel open={expanded}>
        <div className="pr-3 pb-3 pl-4 space-y-1">
          {loading ? (
            // 显示骨架屏，根据 modelKey.totalModelCount 显示对应数量的骨架
            <div className="space-y-1 py-2">
              {Array.from({ length: modelKey.totalModelCount }).map((_, index) => (
                <div key={`skeleton-${index}`} className="rounded border bg-background px-2 py-1">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2 flex-1">
                      <Skeleton className="h-4 w-32" />
                      <Skeleton className="h-3 w-20" />
                    </div>
                    <div className="flex items-center gap-1">
                      <Skeleton className="h-6 w-6 rounded" />
                      <Skeleton className="h-6 w-6 rounded" />
                      <Skeleton className="h-6 w-6 rounded" />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : models.length === 0 ? (
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