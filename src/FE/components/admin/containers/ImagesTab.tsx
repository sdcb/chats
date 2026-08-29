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
  ImageEntry,
  ImageFilters,
  ImageForm,
} from './types';

import { ADMIN_IMAGES_EXPORT_URL } from '@/apis/adminContainersApi';

type ImageDataColumnKey = 'id' | 'image' | 'description' | 'status';
type ImageColumnKey = ImageDataColumnKey | 'actions';

const DEFAULT_COLUMNS: ImageDataColumnKey[] = [
  'image',
  'status',
  'description',
];

type Props = {
  images: ImageEntry[];
  loading: boolean;
  saving: boolean;
  dialog: number | 'new' | null;
  form: ImageForm;
  setForm: Dispatch<SetStateAction<ImageForm>>;
  onDialogChange: (dialog: number | 'new' | null) => void;
  onNew: () => void;
  onEdit: (image: ImageEntry) => void;
  onSave: () => Promise<void>;
  onDelete: (target: DeleteTarget) => Promise<void>;
  filters: ImageFilters;
  onFiltersChange: (filters: ImageFilters) => void;
  onRefresh: () => Promise<void>;
};

export default function ImagesTab({
  images,
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
    useState<ImageDataColumnKey[]>(DEFAULT_COLUMNS);
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
  const updateFilters = (next: Partial<ImageFilters>) =>
    onFiltersChange({ ...filters, ...next });

  const allColumns = useMemo<UnifiedTableColumn<ImageEntry, ImageColumnKey>[]>(
    () => [
      { key: 'id', title: t('Image ID'), cell: (row) => row.id },
      { key: 'image', title: t('Image'), cell: (row) => row.image },
      {
        key: 'description',
        title: t('Description'),
        className: 'max-w-96',
        cell: (row) => row.description || EMPTY_VALUE,
      },
      {
        key: 'status',
        title: t('Status'),
        cell: (row) => (row.isEnabled ? t('Enabled') : t('Disabled')),
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
                onDelete({ kind: 'image', id: row.id, label: row.image })
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
        ): column is UnifiedTableColumn<ImageEntry, ImageDataColumnKey> =>
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
                placeholder={t('Search images')!}
                value={draft.query}
                onChange={(event) => setDraft({ query: event.target.value })}
              />
              <div className="w-[150px]">
                <Select
                  value={filters.enabled}
                  onValueChange={(value) =>
                    updateFilters({ enabled: value as ImageFilters['enabled'] })
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
                      key: column.key as ImageDataColumnKey,
                      title: column.title,
                    }))}
                  selectedColumns={selectedColumns}
                  onToggleColumn={(key, checked) => {
                    const next = new Set(selectedColumns);
                    const dataKey = key as ImageDataColumnKey;
                    if (checked) next.add(dataKey);
                    else if (next.size > 1) next.delete(dataKey);
                    else return;
                    setSelectedColumns(
                      allColumns
                        .filter((column) => column.key !== 'actions')
                        .map((column) => column.key as ImageDataColumnKey)
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
                        exportUrl={ADMIN_IMAGES_EXPORT_URL}
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
                  label={t('Add image')}
                  icon={<IconPlus size={18} />}
                  onClick={onNew}
                />
              ),
            },
          ]}
          columns={visibleColumns}
          rows={images}
          loading={loading}
          page={1}
          totalCount={images.length}
          rowKey={(row) => row.id}
          onPageChange={() => undefined}
          pagination={false}
          emptyText={t('No images found.')}
        />
      </section>

      <Dialog
        open={dialog !== null}
        onOpenChange={(open) => !open && onDialogChange(null)}
      >
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {dialog === 'new' ? t('Add image') : t('Edit image')}
            </DialogTitle>
            <DialogDescription>
              {dialog === 'new'
                ? t(
                    'Images must be enabled in the catalog before templates can use them.',
                  )
                : t(
                    'Renaming an image updates references in resource templates.',
                  )}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <Label>
              {t('Image')}
              <Input
                value={form.image}
                placeholder="registry.example.com/image:tag"
                onChange={(e) => setForm({ ...form, image: e.target.value })}
              />
            </Label>
            <Label>
              {t('Description')}
              <textarea
                className="min-h-24 w-full rounded-md border bg-background px-3 py-2 text-sm"
                value={form.description}
                onChange={(e) =>
                  setForm({ ...form, description: e.target.value })
                }
              />
            </Label>
            <LabelSwitch
              checked={form.isEnabled}
              onCheckedChange={(checked) =>
                setForm({ ...form, isEnabled: checked })
              }
              label={t('Enabled')}
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
