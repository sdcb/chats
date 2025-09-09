import React from 'react';
import { AdminModelDto } from '@/types/adminApis';
import { formatNumberAsMoney } from '@/utils/common';
import { Button } from '@/components/ui/button';
import { IconPencil } from '@/components/Icons';
import DeletePopover from '@/pages/home/_components/Popover/DeletePopover';
import useTranslation from '@/hooks/useTranslation';

interface ModelItemProps {
  model: AdminModelDto;
  onEdit: (model: AdminModelDto) => void;
  onDelete: (modelId: number) => void;
}

export default function ModelItem({ model, onEdit, onDelete }: ModelItemProps) {
  const { t } = useTranslation();

  return (
    <div className="flex items-center justify-between px-2 py-1 rounded hover:bg-muted/40">
      <div
        className="flex-1 min-w-0 cursor-pointer"
        onClick={() => onEdit(model)}
      >
        <div className="truncate">{model.name}</div>
        <div className="text-xs text-blue-600 truncate">
          {'￥' + formatNumberAsMoney(model.inputTokenPrice1M) + '/' + formatNumberAsMoney(model.outputTokenPrice1M)}
        </div>
      </div>
      <div className="flex gap-2 ml-3">
        <Button
          variant="secondary"
          size="sm"
          onClick={() => onEdit(model)}
          title={t('Edit Model')}
        >
          <IconPencil size={16} />
        </Button>
        <div title={t('Delete Model')}>
          <DeletePopover onDelete={() => onDelete(model.modelId)} />
        </div>
      </div>
    </div>
  );
}