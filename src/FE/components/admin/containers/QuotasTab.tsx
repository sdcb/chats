import { Dispatch, SetStateAction } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconEdit, IconPlus, IconWorld } from '@/components/Icons';
import CatalogCardField from '@/components/admin/containers/CatalogCardField';
import IconActionButton from '@/components/common/IconActionButton';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
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
} from './types';

type Props = {
  quotas: Quota[];
  loading: boolean;
  saving: boolean;
  dialog: number | null;
  form: QuotaForm;
  setForm: Dispatch<SetStateAction<QuotaForm>>;
  onDialogChange: (dialog: number | null) => void;
  onEdit: (quota: Quota) => void;
  onNew: () => void;
  onSave: () => Promise<void>;
};

export default function QuotasTab({
  quotas,
  loading,
  saving,
  dialog,
  form,
  setForm,
  onDialogChange,
  onEdit,
  onNew,
  onSave,
}: Props) {
  const { t } = useTranslation();
  const globalQuota = quotas.find((quota) => quota.userId == null);

  return (
    <>
      <section className="space-y-4">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold">{t('Quotas')}</h2>
            <p className="text-sm text-muted-foreground">
              {t(
                'Quotas control per-user container resources and image access.',
              )}
            </p>
          </div>
          <IconActionButton
            label={
              globalQuota ? t('Edit global quota') : t('Configure global quota')
            }
            icon={globalQuota ? <IconEdit size={18} /> : <IconPlus size={18} />}
            onClick={() => (globalQuota ? onEdit(globalQuota) : onNew())}
          />
        </div>

        {loading ? (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {[1, 2].map((item) => (
              <Card key={item} className="animate-pulse">
                <CardHeader className="h-20" />
                <CardContent className="h-44" />
              </Card>
            ))}
          </div>
        ) : quotas.length === 0 ? (
          <div className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
            {t('No quota policies found.')}
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {quotas.map((quota) => (
              <Card key={quota.id}>
                <CardHeader className="flex flex-row items-start justify-between space-y-0 p-4">
                  <div className="flex min-w-0 items-start gap-2">
                    <IconWorld size={18} className="mt-0.5 shrink-0" />
                    <div className="min-w-0">
                      <h3 className="truncate font-semibold">
                        {quota.userId == null
                          ? t('Global default')
                          : quota.userName || `User #${quota.userId}`}
                      </h3>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {t('Updated')}: {formatDateTime(quota.updatedAt)}
                      </p>
                    </div>
                  </div>
                  <IconActionButton
                    label={t('Edit')}
                    icon={<IconEdit size={15} />}
                    className="h-8 w-8"
                    onClick={() => onEdit(quota)}
                  />
                </CardHeader>
                <CardContent className="p-4 pt-0">
                  <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
                    <CatalogCardField
                      label={t('Allowed networks')}
                      className="col-span-2"
                    >
                      {quota.allowedNetworkModes || EMPTY_VALUE}
                    </CatalogCardField>
                    <CatalogCardField label={t('Container limit')}>
                      {quota.maxContainerCount ?? EMPTY_VALUE}
                    </CatalogCardField>
                    <CatalogCardField label={t('Process limit')}>
                      {quota.maxContainerProcesses ?? EMPTY_VALUE}
                    </CatalogCardField>
                    <CatalogCardField label={t('CPU limit')}>
                      {quota.maxCpuCores == null
                        ? EMPTY_VALUE
                        : `${quota.maxCpuCores} ${t('cores')}`}
                    </CatalogCardField>
                    <CatalogCardField label={t('Memory limit')}>
                      {formatBytes(quota.maxMemoryBytes)}
                    </CatalogCardField>
                    <CatalogCardField label={t('Volume limit')}>
                      {formatBytes(quota.maxVolumeBytes)}
                    </CatalogCardField>
                    <CatalogCardField label={t('Max volume bytes per volume')}>
                      {formatBytes(quota.maxVolumeBytesPerVolume)}
                    </CatalogCardField>
                    <CatalogCardField label={t('Max CPU per container')}>
                      {quota.maxContainerCpuCores == null
                        ? EMPTY_VALUE
                        : `${quota.maxContainerCpuCores} ${t('cores')}`}
                    </CatalogCardField>
                    <CatalogCardField label={t('Max memory per container')}>
                      {formatBytes(quota.maxContainerMemoryBytes)}
                    </CatalogCardField>
                    <CatalogCardField label={t('Images')}>
                      {quota.allowCustomImage ? (
                        <Badge>{t('Custom allowed')}</Badge>
                      ) : (
                        <Badge variant="outline">{t('Catalog only')}</Badge>
                      )}
                    </CatalogCardField>
                  </dl>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </section>

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
            {(
              [
                ['maxContainerCount', 'Max containers'],
                ['maxCpuCores', 'Max CPU cores'],
                ['maxMemoryBytes', 'Max memory bytes'],
                ['maxContainerProcesses', 'Max processes'],
                ['maxVolumeBytes', 'Max volume bytes'],
                ['maxContainerCpuCores', 'Max CPU per container'],
                ['maxContainerMemoryBytes', 'Max memory per container'],
                ['maxVolumeBytesPerVolume', 'Max volume bytes per volume'],
              ] as const
            ).map(([key, label]) => (
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
