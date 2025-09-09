import React from 'react';
import { AdminModelDto, GetModelKeysResult } from '@/types/adminApis';
import { Button } from '@/components/ui/button';
import { IconPlus } from '@/components/Icons';
import ChatIcon from '@/components/ChatIcon/ChatIcon';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import ModelKey from './ModelKey';
import CollapsiblePanel from './CollapsiblePanel';

export type ProviderGroup = {
  providerId: number;
  providerName: string;
  keys: GetModelKeysResult[];
};

interface ModelProviderProps {
  provider: ProviderGroup;
  modelsByKey: Record<number, AdminModelDto[]>;
  modelCount: number;
  expanded: boolean;
  expandedKeys: Record<number, boolean>;
  onToggleExpand: () => void;
  onToggleKeyExpand: (keyId: number) => void;
  onAddKey: (providerId: number) => void;
  onEditKey: (key: GetModelKeysResult) => void;
  onDeleteKey: (keyId: number) => void;
  onConfigModels: (keyId: number) => void;
  onAddModel: (keyId: number) => void;
  onEditModel: (model: AdminModelDto) => void;
  onDeleteModel: (modelId: number) => void;
  isDragging?: boolean;
  onDragStart?: (e: React.DragEvent<HTMLDivElement>) => void;
}

export default function ModelProvider({
  provider,
  modelsByKey,
  modelCount,
  expanded,
  expandedKeys,
  onToggleExpand,
  onToggleKeyExpand,
  onAddKey,
  onEditKey,
  onDeleteKey,
  onConfigModels,
  onAddModel,
  onEditModel,
  onDeleteModel,
  isDragging = false,
  onDragStart,
}: ModelProviderProps) {
  const { t } = useTranslation();

  const handleHeaderClick = (e: React.MouseEvent) => {
    // 如果正在拖拽，不触发展开/收起
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
    
    if (onDragStart) {
      onDragStart(e);
    }
  };

  return (
    <div className="mb-3">
      <div 
        className={cn(
          "rounded-xl border bg-card transition-all duration-200",
          isDragging && "opacity-50 transform scale-95",
          provider.keys.length > 0 && "cursor-move"
        )}
        draggable={provider.keys.length > 0}
        onDragStart={handleDragStart}
      >
        <div
          className="flex items-center justify-between p-3 cursor-pointer select-none"
          onClick={handleHeaderClick}
        >
          <div className="flex items-center gap-2">
            <ChatIcon className="h-6 w-6" providerId={provider.providerId} />
            <span className="font-semibold">{t(provider.providerName)}</span>
            <span className="text-muted-foreground text-sm">
              {t('Model Keys')}: {provider.keys.length} {t('Models')}: {modelCount}
            </span>
          </div>
          <div className="flex items-center gap-2">
            <Button
              variant="secondary"
              size="sm"
              onClick={(e) => {
                e.stopPropagation();
                onAddKey(provider.providerId);
              }}
              title={t('Add Model Key')}
            >
              <IconPlus size={16} />
            </Button>
          </div>
        </div>
        <CollapsiblePanel open={expanded} className="mt-1">
          {provider.keys.length === 0 ? (
            <div className="text-sm text-muted-foreground px-3 pb-3">{t('No Model Keys')}</div>
          ) : (
            <div className="pl-6 space-y-2 pb-3">
              {provider.keys.map((key) => (
                <ModelKey
                  key={key.id}
                  modelKey={key}
                  models={modelsByKey[key.id] || []}
                  expanded={!!expandedKeys[key.id]}
                  onToggleExpand={() => onToggleKeyExpand(key.id)}
                  onEdit={onEditKey}
                  onDelete={onDeleteKey}
                  onConfigModels={onConfigModels}
                  onAddModel={onAddModel}
                  onEditModel={onEditModel}
                  onDeleteModel={onDeleteModel}
                />
              ))}
            </div>
          )}
        </CollapsiblePanel>
      </div>
    </div>
  );
}