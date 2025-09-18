import React from 'react';
import { AdminModelDto } from '@/types/adminApis';
import { formatNumberAsMoney } from '@/utils/common';
import { Button } from '@/components/ui/button';
import { IconPencil, IconChartHistogram, IconEyeOff } from '@/components/Icons';
import DeletePopover from '@/components/Popover/DeletePopover';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
// dnd-kit
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

interface ModelItemProps {
  model: AdminModelDto;
  onEdit: (model: AdminModelDto) => void;
  onDelete: (modelId: number) => void;
  onGoToUsage: (params: {
    provider?: string;
    modelKey?: string;
    model?: string;
  }) => void;
}

export default function ModelItem({ model, onEdit, onDelete, onGoToUsage }: ModelItemProps) {
  const { t } = useTranslation();
  const sortable = useSortable({ id: `model-${model.modelId}` });
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = sortable;
  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  const handleContentClick = (e: React.MouseEvent) => {
    // 如果正在拖拽，不触发编辑
    if (isDragging) {
      e.preventDefault();
      return;
    }
    
    // 如果点击的是按钮区域，不触发编辑
    const target = e.target as HTMLElement;
    if (target.closest('button')) {
      return;
    }
    
    onEdit(model);
  };

  return (
    <div 
      className={cn(
        "flex items-center justify-between px-2 py-1 rounded hover:bg-muted/40 transition-all duration-200 relative",
        isDragging ? "opacity-60 cursor-grabbing" : "cursor-move",
        !model.enabled && "opacity-60"
      )}
      ref={setNodeRef}
      style={style}
      {...attributes}
      {...listeners}
    >
      {/* 禁用状态蒙版 */}
      {!model.enabled && (
        <div className="absolute inset-0 bg-muted/20 rounded flex items-center justify-center pointer-events-none">
          <div className="flex items-center gap-1 bg-muted/80 px-2 py-1 rounded text-xs text-muted-foreground">
            <IconEyeOff size={12} />
            <span>{t('Disabled')}</span>
          </div>
        </div>
      )}
      
      <div
        className={cn(
          "flex-1 min-w-0 cursor-pointer",
          !model.enabled && "text-muted-foreground"
        )}
        onClick={handleContentClick}
      >
        <div className="truncate flex items-center gap-2">
          {!model.enabled && <IconEyeOff size={14} className="text-muted-foreground flex-shrink-0" />}
          <span className={cn("truncate", !model.enabled && "line-through")}>{model.name}</span>
        </div>
        <div className={cn(
          "text-xs truncate",
          model.enabled ? "text-blue-600" : "text-muted-foreground"
        )}>
          {'￥' + formatNumberAsMoney(model.inputTokenPrice1M) + '/' + formatNumberAsMoney(model.outputTokenPrice1M)}
        </div>
      </div>
      <div className="flex gap-2 ml-3">
        <Button
          variant="secondary"
          size="sm"
          onClick={(e) => {
            e.stopPropagation();
            onGoToUsage({ model: model.name });
          }}
          title={t('View Usage Records')}
          disabled={!model.enabled}
        >
          <IconChartHistogram size={16} />
        </Button>
        <Button
          variant="secondary"
          size="sm"
          onClick={(e) => {
            e.stopPropagation();
            onEdit(model);
          }}
          title={t('Edit Model')}
        >
          <IconPencil size={16} />
        </Button>
        <div 
          title={t('Delete Model')}
          onClick={(e) => e.stopPropagation()}
        >
          <DeletePopover onDelete={() => onDelete(model.modelId)} />
        </div>
      </div>
    </div>
  );
}