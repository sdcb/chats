import React, { useState } from 'react';
import { AdminModelDto } from '@/types/adminApis';
import { formatNumberAsMoney } from '@/utils/common';
import IconActionButton from '@/components/common/IconActionButton';
import { IconPencil, IconChartHistogram, IconMessage, IconMessageStar, IconPhoto, IconLoader, IconDots, IconTrash } from '@/components/Icons';
import DeletePopover from '@/components/Popover/DeletePopover';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import { TriStateCheckbox, TriStateCheckboxState } from '@/components/ui/tristate-checkbox';
import ModelUserList from '@/components/admin/user-models/ModelUserList';
import { putModels } from '@/apis/adminApis';
import toast from 'react-hot-toast';
// dnd-kit
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';

interface ModelItemProps {
  model: AdminModelDto;
  onEditClick: (model: AdminModelDto) => void;
  onDeleteClick: (modelId: number) => void;
  onGoToUsage: (params: { model: string }) => void;
  onAssignedUserCountChange?: (modelId: number, count: number) => void;
  onUpdated?: () => void | Promise<void>;
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

export default function ModelItem({ model, onEditClick, onDeleteClick, onGoToUsage, onAssignedUserCountChange, onUpdated }: ModelItemProps) {
  const { t } = useTranslation();
  const [userListExpanded, setUserListExpanded] = useState(false);
  const [checkState, setCheckState] = useState<'checked' | 'unchecked' | 'indeterminate' | 'hidden'>('hidden');
  const [batchPending, setBatchPending] = useState(false);
  const [triggerBatchToggle, setTriggerBatchToggle] = useState(0);
  const [isToggling, setIsToggling] = useState(false);

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

  const handleToggleEnabled = async (checked: boolean) => {
    setIsToggling(true);
    try {
      await putModels(String(model.modelId), {
        ...model,
        enabled: checked,
      });
      // 更新成功后触发父组件回调来刷新数据（与编辑对话框保存逻辑一致）
      if (onUpdated) {
        await onUpdated();
      }
    } catch (error) {
      toast.error(t('Update failed'));
      console.error('Failed to toggle model enabled status:', error);
    } finally {
      setIsToggling(false);
    }
  };

  const freshPrice = model.inputFreshTokenPrice1M;
  const cachedPrice = model.inputCachedTokenPrice1M;
  const priceBreakdown = cachedPrice > 0
    ? [freshPrice, cachedPrice, model.outputTokenPrice1M]
    : [freshPrice, model.outputTokenPrice1M];
  const formattedPrice = priceBreakdown.map((value) => formatNumberAsMoney(value)).join('/');

  return (
    <div>
      <div
        className={cn(
          'flex items-center gap-2 px-2 py-1 rounded hover:bg-muted/40 transition-all duration-200 relative touch-pan-y cursor-pointer',
          isDragging ? 'opacity-60' : ''
        )}
        ref={setNodeRef}
        style={style}
        {...attributes}
        onClick={handleRowClick}
      >
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
                <IconLoader size={18} className="animate-spin text-muted-foreground" />
              ) : (
                <TriStateCheckbox
                  state={checkState as TriStateCheckboxState}
                  size="lg"
                  disabled={!model.enabled || batchPending}
                />
              )}
            </button>
          </div>
        </div>

        {/* 拖拽把手区域 - 增大尺寸使其更明显 */}
        <div
          data-no-expand
          className={cn(
            'flex-shrink-0 touch-none px-1 rounded hover:bg-muted/60 transition-colors',
            model.enabled ? 'cursor-grab active:cursor-grabbing' : 'cursor-default opacity-40'
          )}
          {...(model.enabled ? listeners : {})}
          onClick={(e) => e.stopPropagation()}
        >
          <div className="w-2.5 h-4 flex flex-col justify-center gap-0.5">
            <div className="w-full h-0.5 bg-muted-foreground/60 rounded-full" />
            <div className="w-full h-0.5 bg-muted-foreground/60 rounded-full" />
            <div className="w-full h-0.5 bg-muted-foreground/60 rounded-full" />
          </div>
        </div>

        <div
          className={cn(
            'flex-1 min-w-0 flex items-center gap-2',
            !model.enabled && 'text-muted-foreground'
          )}
        >
          <span className="truncate flex items-center gap-1.5 text-sm" title={getApiTypeName(model.apiType)}>
            <span className="hidden sm:inline">{getApiTypeIcon(model.apiType)}</span>
            <span className={cn('truncate', !model.enabled && 'line-through')}>{model.name}</span>
          </span>
          <span
            className={cn(
              'text-xs truncate hidden sm:inline',
              model.enabled ? 'text-blue-600' : 'text-muted-foreground'
            )}
          >
            {formattedPrice}
          </span>
        </div>
        <div className="flex items-center gap-2 ml-3" data-no-expand>
          {/* Toggle switch with loading state */}
          <div className="flex items-center gap-1.5">
            {isToggling && <IconLoader size={14} className="animate-spin text-muted-foreground" />}
            <button
              type="button"
              role="switch"
              aria-checked={model.enabled}
              disabled={isToggling}
              onClick={(e) => {
                e.stopPropagation();
                handleToggleEnabled(!model.enabled);
              }}
              className={cn(
                'relative inline-flex h-3.5 w-7 shrink-0 cursor-pointer items-center rounded-full transition-colors',
                'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring focus-visible:ring-offset-1',
                model.enabled 
                  ? 'bg-green-600/80 dark:bg-green-700/70' 
                  : 'bg-gray-400/60 dark:bg-gray-600/50',
                isToggling && 'opacity-50 cursor-not-allowed'
              )}
            >
              <span
                className={cn(
                  'pointer-events-none block h-2.5 w-2.5 rounded-full bg-white dark:bg-gray-100 shadow-sm transition-transform',
                  model.enabled ? 'translate-x-[16px]' : 'translate-x-[2px]'
                )}
              />
            </button>
          </div>
          
          {/* 桌面端：显示独立按钮 */}
          <div className="hidden sm:flex items-center gap-2">
            <IconActionButton
              label={t('View Usage Records')}
              icon={<IconChartHistogram size={16} />}
              className="h-5 w-5"
              onClick={() => onGoToUsage({ model: model.name })}
              disabled={!model.enabled}
            />
            <IconActionButton
              label={t('Edit Model')}
              icon={<IconPencil size={16} />}
              className="h-5 w-5"
              onClick={() => onEditClick(model)}
            />
            <DeletePopover 
              onDelete={() => onDeleteClick(model.modelId)}
              tooltip={t('Delete Model')}
              className="h-5 w-5"
              iconSize={16}
            />
          </div>

          {/* 移动端：下拉菜单 */}
          <div className="sm:hidden">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6"
                  onClick={(e) => e.stopPropagation()}
                >
                  <IconDots size={16} />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-48">
                <DropdownMenuItem
                  onClick={(e) => {
                    e.stopPropagation();
                    onGoToUsage({ model: model.name });
                  }}
                  disabled={!model.enabled}
                  className="gap-2"
                >
                  <IconChartHistogram size={16} />
                  <span>{t('View Usage Records')}</span>
                </DropdownMenuItem>
                <DropdownMenuItem
                  onClick={(e) => {
                    e.stopPropagation();
                    onEditClick(model);
                  }}
                  className="gap-2"
                >
                  <IconPencil size={16} />
                  <span>{t('Edit Model')}</span>
                </DropdownMenuItem>
                <DropdownMenuItem
                  onClick={(e) => {
                    e.stopPropagation();
                    onDeleteClick(model.modelId);
                  }}
                  className="gap-2 text-destructive focus:text-destructive"
                >
                  <IconTrash size={16} />
                  <span>{t('Delete')}</span>
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
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
