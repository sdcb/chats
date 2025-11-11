import React, { useState } from 'react';
import { AdminModelDto } from '@/types/adminApis';
import { formatNumberAsMoney } from '@/utils/common';
import IconActionButton from '@/components/common/IconActionButton';
import { IconPencil, IconChartHistogram, IconEyeOff, IconMessage, IconMessageStar, IconPhoto, IconLoader } from '@/components/Icons';
import DeletePopover from '@/components/Popover/DeletePopover';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import ModelUserList from '@/components/admin/user-models/ModelUserList';
// dnd-kit
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

interface ModelItemProps {
  model: AdminModelDto;
  onEdit: (model: AdminModelDto) => void;
  onDelete: (modelId: number) => void;
  onGoToUsage: (params: { model: string }) => void;
  onAssignedUserCountChange?: (modelId: number, count: number) => void;
}

// API 类型图标映射
const getApiTypeIcon = (apiType: number) => {
  switch (apiType) {
    case 0: // ChatCompletion
      return <IconMessage size={14} className="flex-shrink-0" />;
    case 1: // Response
      return <IconMessageStar size={14} className="flex-shrink-0" />;
    case 2: // ImageGeneration
      return <IconPhoto size={14} className="flex-shrink-0" />;
    default:
      return <IconMessage size={14} className="flex-shrink-0" />;
  }
};

// API 类型名称映射
const getApiTypeName = (apiType: number) => {
  switch (apiType) {
    case 0:
      return 'ChatCompletion';
    case 1:
      return 'Response';
    case 2:
      return 'ImageGeneration';
    default:
      return 'Unknown';
  }
};

export default function ModelItem({ model, onEdit, onDelete, onGoToUsage, onAssignedUserCountChange }: ModelItemProps) {
  const { t } = useTranslation();
  const [userListExpanded, setUserListExpanded] = useState(false);
  const [checkState, setCheckState] = useState<'checked' | 'unchecked' | 'indeterminate' | 'hidden'>('hidden');
  const [batchPending, setBatchPending] = useState(false);
  const [triggerBatchToggle, setTriggerBatchToggle] = useState(0);
  
  const sortable = useSortable({ id: `model-${model.modelId}` });
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = sortable;
  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  const handleRowClick = (e: React.MouseEvent<HTMLDivElement>) => {
    // 如果正在拖拽，不触发展开
    if (isDragging) {
      e.preventDefault();
      return;
    }
    // 如果点击的是按钮、checkbox 或其他交互元素，不触发展开
    const target = e.target as HTMLElement;
    if (target.closest('button') || target.closest('[role="checkbox"]') || target.closest('[data-no-expand]')) {
      return;
    }
    // 切换展开状态
    setUserListExpanded(!userListExpanded);
  };

  const handleBatchClick = () => {
    setBatchPending(true);
    // 触发批量操作
    setTriggerBatchToggle(prev => prev + 1);
  };

  return (
    <div>
      <div
        className={cn(
          'flex items-center gap-2 px-2 py-1 rounded hover:bg-muted/40 transition-all duration-200 relative touch-pan-y cursor-pointer',
          isDragging ? 'opacity-60 cursor-grabbing' : '',
          !model.enabled && 'opacity-60'
        )}
        ref={setNodeRef}
        style={style}
        {...attributes}
        onClick={handleRowClick}
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

        {/* 批量选择图标 - 在最前面（hidden状态时不显示，带动画展开）*/}
        <div 
          data-no-expand 
          className={cn(
            "overflow-hidden transition-all duration-300 ease-in-out",
            checkState !== 'hidden' ? "w-5 opacity-100" : "w-0 opacity-0"
          )} 
          onClick={(e) => e.stopPropagation()}
        >
          <div className="flex-shrink-0">
            <button
              type="button"
              className="flex items-center justify-center"
              onClick={handleBatchClick}
              disabled={!model.enabled || batchPending}
            >
              {batchPending ? (
                <IconLoader size={20} className="animate-spin text-muted-foreground" />
              ) : checkState === 'checked' ? (
                <div className="w-5 h-5 rounded-full bg-green-500 flex items-center justify-center hover:bg-green-600 transition-colors">
                  <span className="text-white text-xs">✓</span>
                </div>
              ) : checkState === 'indeterminate' ? (
                <div className="w-5 h-5 rounded-full bg-blue-500 flex items-center justify-center hover:bg-blue-600 transition-colors">
                  <span className="text-white text-xs">−</span>
                </div>
              ) : (
                <div className="w-5 h-5 rounded-full border-2 border-muted-foreground/30 hover:border-primary transition-colors" />
              )}
            </button>
          </div>
        </div>

        {/* 拖拽把手区域 */}
        <div
          className={cn(
            'flex-shrink-0 touch-none',
            model.enabled ? 'cursor-grab active:cursor-grabbing' : 'cursor-default'
          )}
          {...(model.enabled ? listeners : {})}
          onClick={(e) => e.stopPropagation()}
        >
          <div className="w-1 h-4 flex flex-col justify-center gap-0.5">
            <div className="w-full h-0.5 bg-muted-foreground/40 rounded-full" />
            <div className="w-full h-0.5 bg-muted-foreground/40 rounded-full" />
            <div className="w-full h-0.5 bg-muted-foreground/40 rounded-full" />
          </div>
        </div>

        <div
          className={cn(
            'flex-1 min-w-0',
            !model.enabled && 'text-muted-foreground'
          )}
        >
          <div className="truncate flex items-center gap-2">
            {!model.enabled && <IconEyeOff size={14} className="text-muted-foreground flex-shrink-0" />}
            <span className="truncate flex items-center gap-1.5" title={getApiTypeName(model.apiType)}>
              {getApiTypeIcon(model.apiType)}
              <span className={cn('truncate', !model.enabled && 'line-through')}>{model.name}</span>
            </span>
          </div>
          <div
            className={cn(
              'text-xs truncate',
              model.enabled ? 'text-blue-600' : 'text-muted-foreground'
            )}
          >
            {'￥' + formatNumberAsMoney(model.inputTokenPrice1M) + '/' + formatNumberAsMoney(model.outputTokenPrice1M)}
          </div>
        </div>
        <div className="flex gap-2 ml-3" data-no-expand>
          <IconActionButton
            label={t('View Usage Records')}
            icon={<IconChartHistogram size={18} />}
            onClick={() => onGoToUsage({ model: model.name })}
            disabled={!model.enabled}
          />
          <IconActionButton
            label={t('Edit Model')}
            icon={<IconPencil size={18} />}
            onClick={() => onEdit(model)}
          />
          <div
            title={t('Delete Model')}
            onClick={(e) => e.stopPropagation()}
          >
            <DeletePopover onDelete={() => onDelete(model.modelId)} />
          </div>
        </div>
      </div>
      
      {model.enabled && (
        <ModelUserList
          model={model}
          isExpanded={userListExpanded}
          onToggle={() => setUserListExpanded(!userListExpanded)}
          onAssignedUserCountChange={onAssignedUserCountChange}
          onCheckStateChange={(state) => setCheckState(state)}
          onBatchToggleComplete={() => setBatchPending(false)}
          batchPending={batchPending}
          triggerBatchToggle={triggerBatchToggle}
        />
      )}
    </div>
  );
}