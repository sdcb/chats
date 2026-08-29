import {
  Dispatch,
  SetStateAction,
  useCallback,
  useMemo,
  useState,
} from 'react';

import useTranslation from '@/hooks/useTranslation';

import { getUserSession } from '@/utils/user';

import ExportButton from '@/components/Button/ExportButtom';
import { IconEdit, IconPlus, IconRefresh } from '@/components/Icons';
import DeletePopover from '@/components/Popover/DeletePopover';
import Tips from '@/components/Tips/Tips';
import IconActionButton from '@/components/common/IconActionButton';
import {
  UnifiedColumnSelector,
  UnifiedTable,
  UnifiedTableColumn,
} from '@/components/table/UnifiedTable';
import { useTextFilterDraft } from '@/components/table/useTextFilterDraft';
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
} from '@/components/ui/select';

import {
  DeleteTarget,
  EMPTY_VALUE,
  Quota,
  QuotaFilters,
  QuotaForm,
  formatBytes,
  formatDateTime,
} from './types';

import { ADMIN_QUOTAS_EXPORT_URL } from '@/apis/adminContainersApi';

type QuotaDataColumnKey =
  | 'id'
  | 'user'
  | 'allowedNetworkModes'
  | 'allowCustomImage'
  | 'maxContainerCount'
  | 'maxContainerProcesses'
  | 'maxCpuCores'
  | 'maxMemoryBytes'
  | 'maxVolumeBytes'
  | 'maxContainerCpuCores'
  | 'maxContainerMemoryBytes'
  | 'maxVolumeBytesPerVolume'
  | 'updatedAt';

type QuotaColumnKey = QuotaDataColumnKey | 'actions';

const DEFAULT_COLUMNS: QuotaDataColumnKey[] = [
  'user',
  'allowedNetworkModes',
  'allowCustomImage',
  'maxContainerCount',
  'maxCpuCores',
  'maxMemoryBytes',
  'updatedAt',
];

type Props = {
  quotas: Quota[];
  loading: boolean;
  saving: boolean;
  dialog: number | 'new' | null;
  form: QuotaForm;
  setForm: Dispatch<SetStateAction<QuotaForm>>;
  onDialogChange: (dialog: number | 'new' | null) => void;
  onEdit: (quota: Quota) => void;
  onNew: () => void;
  onSave: () => Promise<void>;
  onDelete: (target: DeleteTarget) => Promise<void>;
  filters: QuotaFilters;
  onFiltersChange: (filters: QuotaFilters) => void;
  onRefresh: () => Promise<void>;
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
  onDelete,
  filters,
  onFiltersChange,
  onRefresh,
}: Props) {
  const { t } = useTranslation();
  const [selectedColumns, setSelectedColumns] =
    useState<QuotaDataColumnKey[]>(DEFAULT_COLUMNS);
  const committedTextFilters = useMemo(
    () => ({ query: filters.query }),
    [filters.query],
  );
  const commitTextFilters = useCallback(
    (next: { query: string }) => onFiltersChange({ ...filters, ...next }),
    [filters, onFiltersChange],
  );
  const { draft, setDraft, flushDraft, hasPendingDraft } = useTextFilterDraft({
    committed: committedTextFilters,
    onCommit: commitTextFilters,
  });
  const updateFilters = (next: Partial<QuotaFilters>) =>
    onFiltersChange({ ...filters, ...next });

  const allColumns = useMemo<UnifiedTableColumn<Quota, QuotaColumnKey>[]>(
    () => [
      { key: 'id', title: t('Quota ID'), cell: (row) => row.id },
      {
        key: 'user',
        title: t('Owner'),
        cell: (row) =>
          row.userId == null
            ? t('Default inherited quota')
            : row.userName || `User #${row.userId}`,
      },
      {
        key: 'allowedNetworkModes',
        title: t('Allowed networks'),
        cell: (row) => row.allowedNetworkModes || EMPTY_VALUE,
      },
      {
        key: 'allowCustomImage',
        title: t('Images'),
        cell: (row) =>
          row.allowCustomImage ? t('Custom allowed') : t('Catalog only'),
      },
      {
        key: 'maxContainerCount',
        title: t('Container limit'),
        cell: (row) => row.maxContainerCount ?? EMPTY_VALUE,
      },
      {
        key: 'maxContainerProcesses',
        title: t('Process limit'),
        cell: (row) => row.maxContainerProcesses ?? EMPTY_VALUE,
      },
      {
        key: 'maxCpuCores',
        title: t('CPU limit'),
        cell: (row) =>
          row.maxCpuCores == null
            ? EMPTY_VALUE
            : `${row.maxCpuCores} ${t('cores')}`,
      },
      {
        key: 'maxMemoryBytes',
        title: t('Memory limit'),
        cell: (row) => formatBytes(row.maxMemoryBytes),
      },
      {
        key: 'maxVolumeBytes',
        title: t('Volume limit'),
        cell: (row) => formatBytes(row.maxVolumeBytes),
      },
      {
        key: 'maxContainerCpuCores',
        title: t('Max CPU per container'),
        cell: (row) =>
          row.maxContainerCpuCores == null
            ? EMPTY_VALUE
            : `${row.maxContainerCpuCores} ${t('cores')}`,
      },
      {
        key: 'maxContainerMemoryBytes',
        title: t('Max memory per container'),
        cell: (row) => formatBytes(row.maxContainerMemoryBytes),
      },
      {
        key: 'maxVolumeBytesPerVolume',
        title: t('Max volume bytes per volume'),
        cell: (row) => formatBytes(row.maxVolumeBytesPerVolume),
      },
      {
        key: 'updatedAt',
        title: t('Updated'),
        cell: (row) => formatDateTime(row.updatedAt),
      },
      {
        key: 'actions',
        title: t('Actions'),
        cell: (row) => (
          <div className="flex items-center gap-1">
            <IconActionButton
              label={t('Edit')}
              icon={<IconEdit size={15} />}
              className="h-8 w-8"
              onClick={() => onEdit(row)}
            />
            {row.userId != null && (
              <DeletePopover
                onDelete={() =>
                  onDelete({
                    kind: 'quota',
                    id: row.userId!,
                    label: row.userName || `User #${row.userId}`,
                  })
                }
                tooltip={t('Delete')}
                className="h-8 w-8"
                iconSize={15}
              />
            )}
          </div>
        ),
      },
    ],
    [onDelete, onEdit, t],
  );

  const visibleColumns = useMemo(
    () => [
      ...allColumns.filter(
        (column): column is UnifiedTableColumn<Quota, QuotaDataColumnKey> =>
          column.key !== 'actions' && selectedColumns.includes(column.key),
      ),
      allColumns.find((column) => column.key === 'actions')!,
    ],
    [allColumns, selectedColumns],
  );

  const exportParams = useMemo(
    () => ({
      token: getUserSession(),
      query: filters.query || undefined,
      allowCustomImage: filters.allowCustomImage || undefined,
      scope: filters.scope || undefined,
      columns: selectedColumns.join('~'),
    }),
    [filters, selectedColumns],
  );

  return (
    <>
      <section>
        <UnifiedTable
          filters={
            <>
              <Input
                className="w-[240px]"
                placeholder={t('Search quota policies')!}
                value={draft.query}
                onChange={(event) => setDraft({ query: event.target.value })}
              />
              <div className="w-[170px]">
                <Select
                  value={filters.allowCustomImage}
                  onValueChange={(value) =>
                    updateFilters({
                      allowCustomImage:
                        value as QuotaFilters['allowCustomImage'],
                    })
                  }
                >
                  <SelectTrigger
                    value={filters.allowCustomImage}
                    onReset={() => updateFilters({ allowCustomImage: '' })}
                  >
                    {filters.allowCustomImage === 'true'
                      ? t('Custom allowed')
                      : filters.allowCustomImage === 'false'
                      ? t('Catalog only')
                      : t('All image policies')}
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="true">{t('Custom allowed')}</SelectItem>
                    <SelectItem value="false">{t('Catalog only')}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="w-[170px]">
                <Select
                  value={filters.scope}
                  onValueChange={(value) =>
                    updateFilters({ scope: value as QuotaFilters['scope'] })
                  }
                >
                  <SelectTrigger
                    value={filters.scope}
                    onReset={() => updateFilters({ scope: '' })}
                  >
                    {filters.scope === 'default'
                      ? t('Default inherited quota')
                      : filters.scope === 'user'
                      ? t('User quotas')
                      : t('All quota scopes')}
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="default">
                      {t('Default inherited quota')}
                    </SelectItem>
                    <SelectItem value="user">{t('User quotas')}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <Button
                type="button"
                variant="outline"
                size="icon"
                disabled={loading}
                aria-label={t('Refresh')}
                title={t('Refresh')}
                onClick={() => {
                  if (hasPendingDraft) flushDraft();
                  else void onRefresh().catch(() => null);
                }}
              >
                <IconRefresh size={18} />
              </Button>
            </>
          }
          actions={[
            {
              key: 'columns',
              element: (
                <UnifiedColumnSelector
                  allColumns={allColumns
                    .filter((column) => column.key !== 'actions')
                    .map((column) => ({
                      key: column.key as QuotaDataColumnKey,
                      title: column.title,
                    }))}
                  selectedColumns={selectedColumns}
                  onToggleColumn={(key, checked) => {
                    const next = new Set(selectedColumns);
                    const dataKey = key as QuotaDataColumnKey;
                    if (checked) next.add(dataKey);
                    else if (next.size > 1) next.delete(dataKey);
                    else return;
                    setSelectedColumns(
                      allColumns
                        .filter((column) => column.key !== 'actions')
                        .map((column) => column.key as QuotaDataColumnKey)
                        .filter((column) => next.has(column)),
                    );
                  }}
                />
              ),
            },
            {
              key: 'export',
              element: (
                <Tips
                  trigger={
                    <div>
                      <ExportButton
                        exportUrl={ADMIN_QUOTAS_EXPORT_URL}
                        params={exportParams}
                        className="h-9 w-9"
                        disabled={loading}
                      />
                    </div>
                  }
                  side="bottom"
                  content={t('Export to Excel')}
                />
              ),
            },
            {
              key: 'add',
              element: (
                <IconActionButton
                  label={t('Add user quota')}
                  icon={<IconPlus size={18} />}
                  onClick={onNew}
                />
              ),
            },
          ]}
          columns={visibleColumns}
          rows={quotas}
          loading={loading}
          page={1}
          totalCount={quotas.length}
          rowKey={(row) => row.id}
          onPageChange={() => undefined}
          pagination={false}
          emptyText={t('No quota policies found.')}
        />
      </section>

      <Dialog
        open={dialog !== null}
        onOpenChange={(open) => !open && onDialogChange(null)}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>
              {dialog === 'new' ? t('Add user quota') : t('Edit quota policy')}
            </DialogTitle>
            <DialogDescription>
              {dialog === 'new'
                ? t(
                    'Create a user-specific quota that overrides the default inherited quota.',
                  )
                : t('Leave a limit blank for unlimited.')}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-3">
            {dialog === 'new' && (
              <Label className="sm:col-span-3">
                {t('User ID')}
                <Input
                  name="quota-user-id"
                  inputMode="numeric"
                  autoComplete="off"
                  value={form.userId}
                  placeholder={t('Enter user ID')!}
                  onChange={(e) => setForm({ ...form, userId: e.target.value })}
                />
              </Label>
            )}
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
