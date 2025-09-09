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
}: ModelKeyProps) {
  const { t } = useTranslation();

  return (
    <div className="rounded-md border bg-background/30">
      <div
        className="flex items-center justify-between p-3 cursor-pointer select-none"
        onClick={onToggleExpand}
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