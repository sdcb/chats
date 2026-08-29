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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
} from '@/components/ui/select';

import {
  DeleteTarget,
  RuntimeNode,
  RuntimeTemplate,
  TemplateFilters,
  TemplateForm,
  formatBytes,
  formatDateTime,
} from './types';

import { ADMIN_TEMPLATES_EXPORT_URL } from '@/apis/adminContainersApi';

type TemplateDataColumnKey =
  | 'id'
  | 'name'
  | 'runtimeNode'
  | 'image'
  | 'visibility'
  | 'cpuCores'
  | 'memoryBytes'
  | 'maxProcesses'
  | 'backendNetworkName'
  | 'defaultVolumeBytes'
  | 'createdAt'
  | 'updatedAt';

type TemplateColumnKey = TemplateDataColumnKey | 'actions';

const DEFAULT_COLUMNS: TemplateDataColumnKey[] = [
  'name',
  'runtimeNode',
  'image',
  'visibility',
  'cpuCores',
  'memoryBytes',
  'maxProcesses',
  'updatedAt',
];

type Props = {
  nodes: RuntimeNode[];
  templates: RuntimeTemplate[];
  loading: boolean;
  saving: boolean;
  dialog: number | 'new' | null;
  form: TemplateForm;
  setForm: Dispatch<SetStateAction<TemplateForm>>;
  onDialogChange: (dialog: number | 'new' | null) => void;
  onNew: () => void;
  onEdit: (template: RuntimeTemplate) => void;
  onSave: () => Promise<void>;
  onDelete: (target: DeleteTarget) => Promise<void>;
  filters: TemplateFilters;
  onFiltersChange: (filters: TemplateFilters) => void;
  onRefresh: () => Promise<void>;
};

export default function TemplatesTab({
  nodes,
  templates,
  loading,
  saving,
  dialog,
  form,
  setForm,
  onDialogChange,
  onNew,
  onEdit,
  onSave,
  onDelete,
  filters,
  onFiltersChange,
  onRefresh,
}: Props) {
  const { t } = useTranslation();
  const [selectedColumns, setSelectedColumns] =
    useState<TemplateDataColumnKey[]>(DEFAULT_COLUMNS);
  const committedTextFilters = useMemo(
    () => ({ query: filters.query, runtimeNodeId: filters.runtimeNodeId }),
    [filters.query, filters.runtimeNodeId],
  );
  const commitTextFilters = useCallback(
    (next: { query: string; runtimeNodeId: string }) =>
      onFiltersChange({ ...filters, ...next }),
    [filters, onFiltersChange],
  );
  const { draft, setDraft, flushDraft, hasPendingDraft } = useTextFilterDraft({
    committed: committedTextFilters,
    onCommit: commitTextFilters,
  });
  const updateFilters = (next: Partial<TemplateFilters>) =>
    onFiltersChange({ ...filters, ...next });

  const allColumns = useMemo<
    UnifiedTableColumn<RuntimeTemplate, TemplateColumnKey>[]
  >(
    () => [
      { key: 'id', title: t('Template ID'), cell: (row) => row.id },
      { key: 'name', title: t('Name'), cell: (row) => row.name },
      {
        key: 'runtimeNode',
        title: t('Runtime node'),
        cell: (row) => row.runtimeNode?.aiName || `#${row.runtimeNodeId}`,
      },
      {
        key: 'image',
        title: t('Image'),
        className: 'max-w-64',
        cell: (row) => <code className="break-all text-xs">{row.image}</code>,
      },
      {
        key: 'visibility',
        title: t('Visibility'),
        cell: (row) =>
          row.visibility === 3
            ? t('Users and AI')
            : row.visibility === 1
            ? t('Users')
            : row.visibility === 2
            ? t('AI')
            : t('Hidden'),
      },
      { key: 'cpuCores', title: t('CPU cores'), cell: (row) => row.cpuCores },
      {
        key: 'memoryBytes',
        title: t('Memory bytes'),
        cell: (row) => formatBytes(row.memoryBytes),
      },
      {
        key: 'maxProcesses',
        title: t('Max processes'),
        cell: (row) => row.maxProcesses,
      },
      {
        key: 'backendNetworkName',
        title: t('Network'),
        cell: (row) => row.backendNetworkName || t('Default'),
      },
      {
        key: 'defaultVolumeBytes',
        title: t('Default volume bytes'),
        cell: (row) => formatBytes(row.defaultVolumeBytes),
      },
      {
        key: 'createdAt',
        title: t('Created'),
        cell: (row) => formatDateTime(row.createdAt),
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
            <DeletePopover
              onDelete={() =>
                onDelete({ kind: 'template', id: row.id, label: row.name })
              }
              tooltip={t('Delete')}
              className="h-8 w-8"
              iconSize={15}
            />
          </div>
        ),
      },
    ],
    [onDelete, onEdit, t],
  );

  const visibleColumns = useMemo(
    () => [
      ...allColumns.filter(
        (
          column,
        ): column is UnifiedTableColumn<
          RuntimeTemplate,
          TemplateDataColumnKey
        > => column.key !== 'actions' && selectedColumns.includes(column.key),
      ),
      allColumns.find((column) => column.key === 'actions')!,
    ],
    [allColumns, selectedColumns],
  );

  const exportParams = useMemo(
    () => ({
      token: getUserSession(),
      query: filters.query || undefined,
      runtimeNodeId: filters.runtimeNodeId || undefined,
      visibility: filters.visibility || undefined,
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
                placeholder={t('Search resource templates')!}
                value={draft.query}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    query: event.target.value,
                  }))
                }
              />
              <Input
                className="w-[150px]"
                inputMode="numeric"
                placeholder={t('Runtime Node ID')!}
                value={draft.runtimeNodeId}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    runtimeNodeId: event.target.value,
                  }))
                }
              />
              <div className="w-[150px]">
                <Select
                  value={filters.visibility}
                  onValueChange={(value) =>
                    updateFilters({
                      visibility: value as TemplateFilters['visibility'],
                    })
                  }
                >
                  <SelectTrigger
                    value={filters.visibility}
                    onReset={() => updateFilters({ visibility: '' })}
                  >
                    {filters.visibility === '0'
                      ? t('Hidden')
                      : filters.visibility === '1'
                      ? t('Users')
                      : filters.visibility === '2'
                      ? t('AI')
                      : filters.visibility === '3'
                      ? t('Users and AI')
                      : t('All visibility')}
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="0">{t('Hidden')}</SelectItem>
                    <SelectItem value="1">{t('Users')}</SelectItem>
                    <SelectItem value="2">{t('AI')}</SelectItem>
                    <SelectItem value="3">{t('Users and AI')}</SelectItem>
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
                      key: column.key as TemplateDataColumnKey,
                      title: column.title,
                    }))}
                  selectedColumns={selectedColumns}
                  onToggleColumn={(key, checked) => {
                    const next = new Set(selectedColumns);
                    const dataKey = key as TemplateDataColumnKey;
                    if (checked) next.add(dataKey);
                    else if (next.size > 1) next.delete(dataKey);
                    else return;
                    setSelectedColumns(
                      allColumns
                        .filter((column) => column.key !== 'actions')
                        .map((column) => column.key as TemplateDataColumnKey)
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
                        exportUrl={ADMIN_TEMPLATES_EXPORT_URL}
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
                  label={t('Add template')}
                  icon={<IconPlus size={18} />}
                  onClick={onNew}
                />
              ),
            },
          ]}
          columns={visibleColumns}
          rows={templates}
          loading={loading}
          page={1}
          totalCount={templates.length}
          rowKey={(row) => row.id}
          onPageChange={() => undefined}
          pagination={false}
          emptyText={t('No resource templates found.')}
        />
      </section>

      <Dialog
        open={dialog !== null}
        onOpenChange={(open) => !open && onDialogChange(null)}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {dialog === 'new'
                ? t('Add resource template')
                : t('Edit resource template')}
            </DialogTitle>
            <DialogDescription>
              {t(
                'Define the image, resource limits and visibility for container creation.',
              )}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <Label>
              {t('Name')}
              <Input
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </Label>
            <Label>
              {t('Image')}
              <Input
                value={form.image}
                onChange={(e) => setForm({ ...form, image: e.target.value })}
              />
            </Label>
            <Label>
              {t('Runtime node')}
              <select
                className="h-10 w-full rounded-md border bg-background px-3"
                value={form.runtimeNodeId}
                onChange={(e) =>
                  setForm({ ...form, runtimeNodeId: Number(e.target.value) })
                }
              >
                {nodes.map((node) => (
                  <option key={node.id} value={node.id}>
                    {node.name} ({node.aiName})
                  </option>
                ))}
              </select>
            </Label>
            <Label>
              {t('CPU cores')}
              <Input
                type="number"
                min="0"
                step="0.1"
                value={form.cpuCores}
                onChange={(e) =>
                  setForm({ ...form, cpuCores: Number(e.target.value) })
                }
              />
            </Label>
            <Label>
              {t('Memory bytes')}
              <Input
                type="number"
                min="0"
                value={form.memoryBytes}
                onChange={(e) =>
                  setForm({ ...form, memoryBytes: Number(e.target.value) })
                }
              />
              <span className="text-xs text-muted-foreground">
                {formatBytes(form.memoryBytes)}
              </span>
            </Label>
            <Label>
              {t('Max processes')}
              <Input
                type="number"
                min="0"
                value={form.maxProcesses}
                onChange={(e) =>
                  setForm({ ...form, maxProcesses: Number(e.target.value) })
                }
              />
            </Label>
            <Label>
              {t('Network')}
              <Input
                value={form.backendNetworkName}
                placeholder="bridge"
                onChange={(e) =>
                  setForm({ ...form, backendNetworkName: e.target.value })
                }
              />
            </Label>
            <Label>
              {t('Default volume bytes')}
              <Input
                type="number"
                min="0"
                value={form.defaultVolumeBytes ?? ''}
                onChange={(e) =>
                  setForm({
                    ...form,
                    defaultVolumeBytes: e.target.value
                      ? Number(e.target.value)
                      : null,
                  })
                }
              />
            </Label>
            <Label>
              {t('Visibility')}
              <select
                className="h-10 w-full rounded-md border bg-background px-3"
                value={form.visibility}
                onChange={(e) =>
                  setForm({ ...form, visibility: Number(e.target.value) })
                }
              >
                <option value={0}>{t('Hidden')}</option>
                <option value={1}>{t('Users')}</option>
                <option value={2}>{t('AI')}</option>
                <option value={3}>{t('Users and AI')}</option>
              </select>
            </Label>
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
