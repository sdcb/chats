import React from 'react';
import { useSortable, SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { AdminModelDto, GetModelKeysResult } from '@/types/adminApis';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import IconActionButton from '@/components/common/IconActionButton';
import { IconPlus, IconChartHistogram } from '@/components/Icons';
import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import { feModelProviders } from '@/types/model';
import ModelKey from './ModelKey';
import CollapsiblePanel from './CollapsiblePanel';

export type ProviderGroup = {
  providerId: number;
  providerName: string;
  keys: GetModelKeysResult[];
  keyCount: number;
  modelCount: number;
};

interface ModelProviderProps {
  provider: ProviderGroup;
  modelsByKey: Record<number, AdminModelDto[]>;
  expanded: boolean;
  expandedKeys: Record<number, boolean>;
  loading?: boolean; // 是否正在加载 keys
  loadingModels?: Record<number, boolean>; // 各个 key 的 models 加载状态
  onToggleExpand: () => void;
  onToggleKeyExpand: (keyId: number) => void;
  onAddKey: (providerId: number) => void;
  onEditKey: (key: GetModelKeysResult) => void;
  onDeleteKey: (keyId: number) => void;
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

export default function ModelProvider({
  provider,
  modelsByKey,
  expanded,
  expandedKeys,
  loading = false,
  loadingModels = {},
  onToggleExpand,
  onToggleKeyExpand,
  onAddKey,
  onEditKey,
  onDeleteKey,
  onConfigModels,
  onAddModel,
  onEditModel,
  onDeleteModel,
  onGoToUsage,
}: ModelProviderProps) {
  const { t } = useTranslation();
  const sortable = useSortable({ id: `provider-${provider.providerId}` });
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = sortable;
  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  const handleHeaderClick = (e: React.MouseEvent) => {
    // 如果正在拖拽，不触发展开/收起
    if (isDragging) {
      e.preventDefault();
      return;
    }
    
    // 如果点击的是按钮区域，不触发展开/收起
    const target = e.target as HTMLElement;
    const btn = target.closest('button');
    // 允许带有 data-allow-toggle 的按钮触发展开/收起（拖拽把手点击）
    if (btn && !btn.getAttribute('data-allow-toggle')) {
      return;
    }
    
    onToggleExpand();
  };

  // Provider 的拖拽由上层 dnd-kit 管理

  return (
    <div className="mb-3" ref={setNodeRef} style={style}>
      <div 
        className={cn(
          "rounded-xl border bg-card transition-all duration-200 touch-pan-y",
          isDragging && "opacity-60"
        )}
        {...attributes}
      >
        <div
          className="flex items-center justify-between p-3 select-none"
          onClick={handleHeaderClick}
        >
          <div className="flex items-center gap-2">
            {/* 拖拽把手：图标 + 名称 都作为把手，避免与页面滚动冲突 */}
            <button
              className={cn(
                "p-0 m-0 bg-transparent border-0 inline-flex items-center gap-2 touch-none",
                "cursor-grab active:cursor-grabbing",
                isDragging && "cursor-grabbing"
              )}
              aria-label={t('Drag to reorder')}
              data-allow-toggle
              // 点击把手：若非拖拽，允许冒泡到 header 触发展开；拖拽中则拦截
              onClick={(e) => {
                if (isDragging) {
                  e.preventDefault();
                  e.stopPropagation();
                }
              }}
              {...listeners}
            >
              <ModelProviderIcon className="h-6 w-6" providerId={provider.providerId} />
              {/* 小屏隐藏标题，仅显示图标；大屏显示提供商标题 */}
              <span className="font-semibold hidden sm:inline">{t(provider.providerName)}</span>
            </button>
            {/* 计数信息：小屏仅显示"密钥: X"，大屏显示"密钥: X 模型: Y" */}
            <span className="text-muted-foreground text-sm">
              {t('Model Keys')}: {provider.keyCount}
              <span className="hidden sm:inline"> {t('Models')}: {provider.modelCount}</span>
            </span>
          </div>
          <div className="flex items-center gap-2">
            <IconActionButton
              label={t('View Usage Records')}
              icon={<IconChartHistogram size={18} />}
              onClick={() => {
                const providerData = feModelProviders.find(p => p.id === provider.providerId);
                if (providerData) {
                  onGoToUsage({ provider: providerData.name });
                }
              }}
            />
            <IconActionButton
              label={t('Add Model Key')}
              icon={<IconPlus size={18} />}
              onClick={() => onAddKey(provider.providerId)}
            />
          </div>
        </div>
        <CollapsiblePanel open={expanded} className="mt-1">
          {loading ? (
            // 显示骨架屏，根据 provider.keyCount 显示对应数量的骨架
            <div className="pl-6 space-y-2 pb-3">
              {Array.from({ length: provider.keyCount }).map((_, index) => (
                <div key={`skeleton-${index}`} className="rounded-lg border bg-background p-3">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2 flex-1">
                      <Skeleton className="h-5 w-32" />
                      <Skeleton className="h-4 w-24" />
                    </div>
                    <div className="flex items-center gap-2">
                      <Skeleton className="h-8 w-8 rounded" />
                      <Skeleton className="h-8 w-8 rounded" />
                      <Skeleton className="h-8 w-8 rounded" />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : provider.keys.length === 0 ? (
            <div className="text-sm text-muted-foreground px-3 pb-3">{t('No Model Keys')}</div>
          ) : (
            <div className="pl-6 space-y-2 pb-3">
              <SortableContext items={provider.keys.map(k => `key-${k.id}`)} strategy={verticalListSortingStrategy}>
                {provider.keys.map((key) => (
                  <ModelKey
                    key={key.id}
                    modelKey={key}
                    models={modelsByKey[key.id] || []}
                    expanded={!!expandedKeys[key.id]}
                    loading={loadingModels[key.id]}
                    onToggleExpand={() => onToggleKeyExpand(key.id)}
                    onEdit={onEditKey}
                    onDelete={onDeleteKey}
                    onConfigModels={onConfigModels}
                    onAddModel={onAddModel}
                    onEditModel={onEditModel}
                    onDeleteModel={onDeleteModel}
                    onGoToUsage={onGoToUsage}
                  />
                ))}
              </SortableContext>
            </div>
          )}
        </CollapsiblePanel>
      </div>
    </div>
  );
}