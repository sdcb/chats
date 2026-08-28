import { Dispatch, SetStateAction } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconEdit, IconPlus, IconTrash } from '@/components/Icons';
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

import { DeleteTarget, EMPTY_VALUE, ImageEntry, ImageForm } from './types';

type Props = {
  rows: ImageEntry[];
  loading: boolean;
  saving: boolean;
  search: string;
  page: number;
  totalCount: number;
  dialog: number | 'new' | null;
  form: ImageForm;
  setForm: Dispatch<SetStateAction<ImageForm>>;
  onSearchChange: (value: string) => void;
  onPageChange: (page: number) => void;
  onDialogChange: (dialog: number | 'new' | null) => void;
  onNew: () => void;
  onEdit: (image: ImageEntry) => void;
  onSave: () => Promise<void>;
  onDeleteRequest: (target: DeleteTarget) => void;
};

export default function ImagesTab({
  rows,
  loading,
  saving,
  search,
  page,
  totalCount,
  dialog,
  form,
  setForm,
  onSearchChange,
  onPageChange,
  onDialogChange,
  onNew,
  onEdit,
  onSave,
  onDeleteRequest,
}: Props) {
  const { t } = useTranslation();

  const columns: UnifiedTableColumn<ImageEntry>[] = [
    {
      key: 'image',
      title: t('Image'),
      className: 'min-w-64',
      cell: (x) => (
        <code className="text-xs font-medium text-foreground">{x.image}</code>
      ),
    },
    {
      key: 'description',
      title: t('Description'),
      className: 'min-w-64',
      cell: (x) => x.description || EMPTY_VALUE,
    },
    {
      key: 'status',
      title: t('Status'),
      cell: (x) => (
        <Badge variant={x.isEnabled ? 'default' : 'secondary'}>
          {x.isEnabled ? t('Enabled') : t('Disabled')}
        </Badge>
      ),
    },
    {
      key: 'actions',
      title: t('Actions'),
      className: 'w-28',
      cell: (x) => (
        <div className="flex gap-1">
          <Button
            size="icon"
            variant="ghost"
            title={t('Edit')}
            onClick={() => onEdit(x)}
          >
            <IconEdit size={16} />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            title={t('Delete')}
            onClick={() =>
              onDeleteRequest({ kind: 'image', id: x.id, label: x.image })
            }
          >
            <IconTrash size={16} />
          </Button>
        </div>
      ),
    },
  ];

  return (
    <>
      <UnifiedTable
        filters={
          <Input
            className="w-full sm:w-72"
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder={t('Search images')}
          />
        }
        actions={[
          {
            key: 'add',
            element: (
              <Button onClick={onNew}>
                <IconPlus
                  size={16}
                  className="mr-2"
                  stroke="hsl(var(--primary-foreground))"
                />
                {t('Add image')}
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
        emptyText={t('No images found.')}
      />

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
