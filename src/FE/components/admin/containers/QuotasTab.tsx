import { Dispatch, SetStateAction } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconEdit, IconPlus } from '@/components/Icons';
import {
  UnifiedTable,
  UnifiedTableColumn,
} from '@/components/table/UnifiedTable';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { LabelSwitch } from '@/components/ui/label-switch';

import {
  EMPTY_VALUE,
  Quota,
  QuotaForm,
  formatBytes,
  formatDateTime,
  limitText,
} from './types';

type Props = {
  quotas: Quota[];
  rows: Quota[];
  loading: boolean;
  saving: boolean;
  page: number;
  totalCount: number;
  dialog: number | null;
  form: QuotaForm;
  setForm: Dispatch<SetStateAction<QuotaForm>>;
  onPageChange: (page: number) => void;
  onDialogChange: (dialog: number | null) => void;
  onEdit: (quota: Quota) => void;
  onNew: () => void;
  onSave: () => Promise<void>;
};

export default function QuotasTab({
  quotas,
  rows,
  loading,
  saving,
  page,
  totalCount,
  dialog,
  form,
  setForm,
  onPageChange,
  onDialogChange,
  onEdit,
  onNew,
  onSave,
}: Props) {
  const { t } = useTranslation();

  const columns: UnifiedTableColumn<Quota>[] = [
    {
      key: 'scope',
      title: t('Scope'),
      cell: (x) => (
        <div>
          <div className="font-medium text-foreground">
            {x.userId == null
              ? t('Global default')
              : x.userName || `User #${x.userId}`}
          </div>
          <div className="text-xs text-muted-foreground">
            {t('Updated')}: {formatDateTime(x.updatedAt)}
          </div>
        </div>
      ),
    },
    {
      key: 'networks',
      title: t('Allowed networks'),
      cell: (x) => x.allowedNetworkModes || EMPTY_VALUE,
    },
    {
      key: 'containers',
      title: t('Container limit'),
      cell: (x) => limitText(x.maxContainerCount),
    },
    {
      key: 'cpu',
      title: t('CPU limit'),
      cell: (x) => limitText(x.maxCpuCores, ` ${t('cores')}`),
    },
    {
      key: 'memory',
      title: t('Memory limit'),
      cell: (x) => formatBytes(x.maxMemoryBytes),
    },
    {
      key: 'volumes',
      title: t('Volume limit'),
      cell: (x) => (
        <div className="whitespace-nowrap">
          {formatBytes(x.maxVolumeBytes)}
          <span className="text-xs text-muted-foreground">
            {' '}
            / {formatBytes(x.maxVolumeBytesPerVolume)}
          </span>
        </div>
      ),
    },
    {
      key: 'perContainer',
      title: t('Per-container limits'),
      cell: (x) => (
        <div className="whitespace-nowrap">
          {limitText(x.maxContainerCpuCores, ` ${t('cores')}`)}
          <span className="text-xs text-muted-foreground">
            {' '}
            · {formatBytes(x.maxContainerMemoryBytes)}
          </span>
        </div>
      ),
    },
    {
      key: 'processes',
      title: t('Process limit'),
      cell: (x) => limitText(x.maxContainerProcesses),
    },
    {
      key: 'custom',
      title: t('Images'),
      cell: (x) =>
        x.allowCustomImage ? (
          <Badge>{t('Custom allowed')}</Badge>
        ) : (
          <Badge variant="outline">{t('Catalog only')}</Badge>
        ),
    },
    {
      key: 'actions',
      title: t('Actions'),
      className: 'w-20',
      cell: (x) => (
        <Button
          size="icon"
          variant="ghost"
          title={t('Edit')}
          onClick={() => onEdit(x)}
        >
          <IconEdit size={16} />
        </Button>
      ),
    },
  ];

  const numberFields = [
    ['maxContainerCount', 'Max containers'],
    ['maxCpuCores', 'Max CPU cores'],
    ['maxMemoryBytes', 'Max memory bytes'],
    ['maxContainerProcesses', 'Max processes'],
    ['maxVolumeBytes', 'Max volume bytes'],
    ['maxContainerCpuCores', 'Max CPU per container'],
    ['maxContainerMemoryBytes', 'Max memory per container'],
    ['maxVolumeBytesPerVolume', 'Max volume bytes per volume'],
  ] as const;

  return (
    <>
      <UnifiedTable
        filters={
          <span className="text-sm text-muted-foreground">
            {t('Quotas control per-user container resources and image access.')}
          </span>
        }
        actions={[
          {
            key: 'edit',
            element: (
              <Button
                onClick={() => {
                  const globalQuota = quotas.find((x) => x.userId == null);
                  if (globalQuota) onEdit(globalQuota);
                  else onNew();
                }}
              >
                <IconPlus
                  size={16}
                  className="mr-2"
                  stroke="hsl(var(--primary-foreground))"
                />
                {quotas.find((x) => x.userId == null)
                  ? t('Edit global quota')
                  : t('Configure global quota')}
              </Button>
            ),
          },
        ]}
        columns={columns}
        rows={rows}
        loading={loading}
        page={page}
        totalCount={totalCount}
        rowKey={(x) => x.id}
        onPageChange={onPageChange}
        emptyText={t('No quota policies found.')}
      />

      <Dialog
        open={dialog !== null}
        onOpenChange={(open) => !open && onDialogChange(null)}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>{t('Edit quota policy')}</DialogTitle>
            <DialogDescription>
              {t('Leave a limit blank for unlimited.')}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-3">
            <Label className="sm:col-span-3">
              {t('Allowed network modes')}
              <Input
                value={form.allowedNetworkModes}
                placeholder="none,bridge"
                onChange={(e) =>
                  setForm({ ...form, allowedNetworkModes: e.target.value })
                }
              />
            </Label>
            {numberFields.map(([key, label]) => (
              <Label key={key}>
                {t(label)}
                <Input
                  type="number"
                  min="0"
                  value={form[key]}
                  onChange={(e) => setForm({ ...form, [key]: e.target.value })}
                />
              </Label>
            ))}
            <LabelSwitch
              checked={form.allowCustomImage}
              onCheckedChange={(checked) =>
                setForm({ ...form, allowCustomImage: checked })
              }
              label={t('Allow custom images')}
              className="sm:col-span-3"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => onDialogChange(null)}>
              {t('Cancel')}
            </Button>
            <Button
              disabled={saving}
              onClick={() => onSave().catch(() => null)}
            >
              {t('Save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
