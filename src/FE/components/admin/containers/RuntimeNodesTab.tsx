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
  RuntimeForm,
  RuntimeNode,
  RuntimeNodeFilters,
  RuntimeTemplate,
  formatDateTime,
} from './types';

import { ADMIN_RUNTIME_NODES_EXPORT_URL } from '@/apis/adminContainersApi';

type RuntimeDataColumnKey =
  | 'id'
  | 'name'
  | 'aiName'
  | 'backend'
  | 'status'
  | 'endpoint'
  | 'credential'
  | 'templates'
  | 'description'
  | 'createdAt'
  | 'updatedAt';

type RuntimeColumnKey = RuntimeDataColumnKey | 'actions';

const DEFAULT_COLUMNS: RuntimeDataColumnKey[] = [
  'name',
  'aiName',
  'backend',
  'status',
  'endpoint',
  'templates',
  'updatedAt',
];

type Props = {
  nodes: RuntimeNode[];
  templates: RuntimeTemplate[];
  loading: boolean;
  saving: boolean;
  dialog: number | 'new' | null;
  form: RuntimeForm;
  setForm: Dispatch<SetStateAction<RuntimeForm>>;
  onDialogChange: (dialog: number | 'new' | null) => void;
  onNew: () => void;
  onEdit: (node: RuntimeNode) => void;
  onToggle: (node: RuntimeNode) => Promise<void>;
  onSave: () => Promise<void>;
  onDelete: (target: DeleteTarget) => Promise<void>;
  filters: RuntimeNodeFilters;
  onFiltersChange: (filters: RuntimeNodeFilters) => void;
  onRefresh: () => Promise<void>;
};

export default function RuntimeNodesTab({
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
  onToggle,
  onSave,
  onDelete,
  filters,
  onFiltersChange,
  onRefresh,
}: Props) {
  const { t } = useTranslation();
  const [selectedColumns, setSelectedColumns] =
    useState<RuntimeDataColumnKey[]>(DEFAULT_COLUMNS);
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
  const updateFilters = (next: Partial<RuntimeNodeFilters>) =>
    onFiltersChange({ ...filters, ...next });

  const allColumns = useMemo<
    UnifiedTableColumn<RuntimeNode, RuntimeColumnKey>[]
  >(
    () => [
      { key: 'id', title: t('Runtime Node ID'), cell: (row) => row.id },
      { key: 'name', title: t('Name'), cell: (row) => row.name },
      { key: 'aiName', title: t('AI name'), cell: (row) => row.aiName },
      {
        key: 'backend',
        title: t('Backend'),
        cell: (row) =>
          row.backendType === 1
            ? t('Docker')
            : row.backendType === 2
            ? t('Windows Docker')
            : row.backendType === 3
            ? t('Kubernetes')
            : t('Other'),
      },
      {
        key: 'status',
        title: t('Status'),
        cell: (row) =>
          row.isEnabled ? (
            <span className="text-green-600 dark:text-green-400">
              {t('Enabled')}
            </span>
          ) : (
            <span className="text-muted-foreground">{t('Disabled')}</span>
          ),
      },
      {
        key: 'endpoint',
        title: t('Endpoint'),
        className: 'max-w-64',
        cell: (row) => (
          <code className="break-all text-xs">
            {row.endpoint || t('System default')}
          </code>
        ),
      },
      {
        key: 'credential',
        title: t('Credential'),
        cell: (row) => (row.hasCredential ? t('Configured') : EMPTY_VALUE),
      },
      {
        key: 'templates',
        title: t('Templates'),
        cell: (row) =>
          templates.filter((template) => template.runtimeNodeId === row.id)
            .length,
      },
      {
        key: 'description',
        title: t('Description'),
        className: 'max-w-72',
        cell: (row) => row.description || EMPTY_VALUE,
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
            <LabelSwitch
              checked={row.isEnabled}
              onCheckedChange={(checked) => {
                if (checked !== row.isEnabled) onToggle(row).catch(() => null);
              }}
              label={row.isEnabled ? t('Enabled') : t('Disabled')}
              className="mr-1 gap-1"
              labelClassName="text-xs"
              switchClassName="scale-75"
            />
            <IconActionButton
              label={t('Edit')}
              icon={<IconEdit size={15} />}
              className="h-8 w-8"
              onClick={() => onEdit(row)}
            />
            <DeletePopover
              onDelete={() =>
                onDelete({ kind: 'runtime', id: row.id, label: row.name })
              }
              tooltip={t('Delete')}
              className="h-8 w-8"
              iconSize={15}
            />
          </div>
        ),
      },
    ],
    [onDelete, onEdit, onToggle, t, templates],
  );

  const visibleColumns = useMemo(
    () => [
      ...allColumns.filter(
        (
          column,
        ): column is UnifiedTableColumn<RuntimeNode, RuntimeDataColumnKey> =>
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
      backendType: filters.backendType || undefined,
      enabled: filters.enabled || undefined,
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
                placeholder={t('Search runtime nodes')!}
                value={draft.query}
                onChange={(event) => setDraft({ query: event.target.value })}
              />
              <div className="w-[160px]">
                <Select
                  value={filters.backendType}
                  onValueChange={(value) =>
                    updateFilters({
                      backendType: value as RuntimeNodeFilters['backendType'],
                    })
                  }
                >
                  <SelectTrigger
                    value={filters.backendType}
                    onReset={() => updateFilters({ backendType: '' })}
                  >
                    {filters.backendType === '1'
                      ? t('Docker')
                      : filters.backendType === '2'
                      ? t('Windows Docker')
                      : filters.backendType === '3'
                      ? t('Kubernetes')
                      : filters.backendType === '4'
                      ? t('Other')
                      : t('All backends')}
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="1">{t('Docker')}</SelectItem>
                    <SelectItem value="2">{t('Windows Docker')}</SelectItem>
                    <SelectItem value="3">{t('Kubernetes')}</SelectItem>
                    <SelectItem value="4">{t('Other')}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="w-[150px]">
                <Select
                  value={filters.enabled}
                  onValueChange={(value) =>
                    updateFilters({
                      enabled: value as RuntimeNodeFilters['enabled'],
                    })
                  }
                >
                  <SelectTrigger
                    value={filters.enabled}
                    onReset={() => updateFilters({ enabled: '' })}
                  >
                    {filters.enabled === 'true'
                      ? t('Enabled')
                      : filters.enabled === 'false'
                      ? t('Disabled')
                      : t('All statuses')}
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="true">{t('Enabled')}</SelectItem>
                    <SelectItem value="false">{t('Disabled')}</SelectItem>
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
                      key: column.key,
                      title: column.title,
                    }))}
                  selectedColumns={selectedColumns}
                  onToggleColumn={(key, checked) => {
                    const next = new Set(selectedColumns);
                    const dataKey = key as RuntimeDataColumnKey;
                    if (checked) next.add(dataKey);
                    else if (next.size > 1) next.delete(dataKey);
                    else return;
                    setSelectedColumns(
                      allColumns
                        .filter((column) => column.key !== 'actions')
                        .map((column) => column.key as RuntimeDataColumnKey)
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
                        exportUrl={ADMIN_RUNTIME_NODES_EXPORT_URL}
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
                  label={t('Add runtime node')}
                  icon={<IconPlus size={18} />}
                  onClick={onNew}
                />
              ),
            },
          ]}
          columns={visibleColumns}
          rows={nodes}
          loading={loading}
          page={1}
          totalCount={nodes.length}
          rowKey={(row) => row.id}
          onPageChange={() => undefined}
          pagination={false}
          emptyText={t('No runtime nodes found.')}
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
                ? t('Add runtime node')
                : t('Edit runtime node')}
            </DialogTitle>
            <DialogDescription>
              {t(
                'Configure the Docker daemon connection and runtime identity.',
              )}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <Label>
              {t('Name')}
              <Input
                name="runtime-node-name"
                autoComplete="off"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </Label>
            <Label>
              {t('AI name')}
              <Input
                name="runtime-node-ai-name"
                autoComplete="off"
                value={form.aiName}
                onChange={(e) => setForm({ ...form, aiName: e.target.value })}
              />
            </Label>
            <Label>
              {t('Backend')}
              <select
                className="h-10 w-full rounded-md border bg-background px-3"
                value={form.backendType}
                onChange={(e) =>
                  setForm({ ...form, backendType: Number(e.target.value) })
                }
              >
                <option value={1}>{t('Docker')}</option>
                <option value={2}>{t('Windows Docker')}</option>
                <option value={3}>{t('Kubernetes')}</option>
                <option value={4}>{t('Other')}</option>
              </select>
            </Label>
            <Label>
              {t('Endpoint')}
              <Input
                name="runtime-node-endpoint"
                autoComplete="off"
                spellCheck={false}
                value={form.endpoint}
                placeholder="npipe://./pipe/docker_engine"
                onChange={(e) => setForm({ ...form, endpoint: e.target.value })}
              />
              <span className="text-xs text-muted-foreground">
                {t('Leave blank to use the host operating system default.')}
              </span>
            </Label>
            <Label className="sm:col-span-2">
              {t('Description')}
              <textarea
                className="min-h-20 w-full rounded-md border bg-background px-3 py-2 text-sm"
                value={form.description}
                onChange={(e) =>
                  setForm({ ...form, description: e.target.value })
                }
              />
            </Label>
            <Label className="sm:col-span-2">
              {t('Credential')}
              <Input
                type="password"
                name="runtime-node-credential"
                autoComplete="new-password"
                value={form.credential}
                onChange={(e) =>
                  setForm({ ...form, credential: e.target.value })
                }
              />
              <span className="text-xs text-muted-foreground">
                {t('Leave blank to keep the current credential.')}
              </span>
            </Label>
            <LabelSwitch
              checked={form.isEnabled}
              onCheckedChange={(checked) =>
                setForm({ ...form, isEnabled: checked })
              }
              label={t('Enabled')}
              className="sm:col-span-2"
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
