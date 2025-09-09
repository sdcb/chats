import React from 'react';
import { AdminModelDto, GetModelKeysResult } from '@/types/adminApis';
import { Button } from '@/components/ui/button';
import { IconPlus } from '@/components/Icons';
import ChatIcon from '@/components/ChatIcon/ChatIcon';
import useTranslation from '@/hooks/useTranslation';
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
}: ModelProviderProps) {
  const { t } = useTranslation();

  return (
    <div className="mb-3">
      <div className="rounded-xl border bg-card">
        <div
          className="flex items-center justify-between p-3 cursor-pointer select-none"
          onClick={onToggleExpand}
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