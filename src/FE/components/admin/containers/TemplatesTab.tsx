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

import {
  DeleteTarget,
  RuntimeNode,
  RuntimeTemplate,
  TemplateForm,
  formatBytes,
  formatDateTime,
} from './types';

type Props = {
  nodes: RuntimeNode[];
  rows: RuntimeTemplate[];
  loading: boolean;
  saving: boolean;
  search: string;
  page: number;
  totalCount: number;
  dialog: number | 'new' | null;
  form: TemplateForm;
  setForm: Dispatch<SetStateAction<TemplateForm>>;
  onSearchChange: (value: string) => void;
  onPageChange: (page: number) => void;
  onDialogChange: (dialog: number | 'new' | null) => void;
  onNew: () => void;
  onEdit: (template: RuntimeTemplate) => void;
  onSave: () => Promise<void>;
  onDeleteRequest: (target: DeleteTarget) => void;
};

export default function TemplatesTab({
  nodes,
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

  const columns: UnifiedTableColumn<RuntimeTemplate>[] = [
    {
      key: 'name',
      title: t('Name'),
      cell: (x) => (
        <span className="font-medium text-foreground">{x.name}</span>
      ),
    },
    {
      key: 'image',
      title: t('Image'),
      className: 'min-w-48',
      cell: (x) => <code className="text-xs">{x.image}</code>,
    },
    {
      key: 'runtime',
      title: t('Runtime node'),
      cell: (x) => x.runtimeNode?.aiName || `#${x.runtimeNodeId}`,
    },
    {
      key: 'resources',
      title: t('Resources'),
      cell: (x) => (
        <div className="whitespace-nowrap">
          {x.cpuCores} {t('CPU cores')} · {formatBytes(x.memoryBytes)}
        </div>
      ),
    },
    {
      key: 'processes',
      title: t('Max processes'),
      cell: (x) => x.maxProcesses,
    },
    {
      key: 'network',
      title: t('Network'),
      cell: (x) => x.backendNetworkName || t('Default'),
    },
    {
      key: 'volume',
      title: t('Default volume bytes'),
      cell: (x) => formatBytes(x.defaultVolumeBytes),
    },
    {
      key: 'visibility',
      title: t('Visibility'),
      cell: (x) => (
        <Badge variant="outline">
          {x.visibility === 3
            ? t('Users and AI')
            : x.visibility === 1
            ? t('Users')
            : x.visibility === 2
            ? t('AI')
            : t('Hidden')}
        </Badge>
      ),
    },
    {
      key: 'updated',
      title: t('Updated'),
      cell: (x) => formatDateTime(x.updatedAt),
    },
    {
      key: 'created',
      title: t('Created'),
      cell: (x) => formatDateTime(x.createdAt),
    },
    {
      key: 'actions',
      title: t('Actions'),
      className: 'w-24',
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
              onDeleteRequest({ kind: 'template', id: x.id, label: x.name })
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
            placeholder={t('Search templates')}
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
                {t('Add template')}
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
        emptyText={t('No resource templates found.')}
      />

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
