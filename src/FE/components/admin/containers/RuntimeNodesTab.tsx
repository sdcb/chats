import { Dispatch, SetStateAction } from 'react';

import useTranslation from '@/hooks/useTranslation';

import {
  IconCheck,
  IconDocker,
  IconEdit,
  IconPlus,
  IconTrash,
} from '@/components/Icons';
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
  DeleteTarget,
  EMPTY_VALUE,
  RuntimeForm,
  RuntimeNode,
  RuntimeTemplate,
  formatDateTime,
} from './types';

type Props = {
  nodes: RuntimeNode[];
  templates: RuntimeTemplate[];
  rows: RuntimeNode[];
  loading: boolean;
  saving: boolean;
  search: string;
  page: number;
  totalCount: number;
  dialog: number | 'new' | null;
  form: RuntimeForm;
  setForm: Dispatch<SetStateAction<RuntimeForm>>;
  onSearchChange: (value: string) => void;
  onPageChange: (page: number) => void;
  onDialogChange: (dialog: number | 'new' | null) => void;
  onNew: () => void;
  onEdit: (node: RuntimeNode) => void;
  onToggle: (node: RuntimeNode) => Promise<void>;
  onSave: () => Promise<void>;
  onDeleteRequest: (target: DeleteTarget) => void;
};

export default function RuntimeNodesTab({
  nodes,
  templates,
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
  onToggle,
  onSave,
  onDeleteRequest,
}: Props) {
  const { t } = useTranslation();

  const columns: UnifiedTableColumn<RuntimeNode>[] = [
    {
      key: 'name',
      title: t('Name'),
      cell: (x) => (
        <div>
          <div className="font-medium text-foreground">{x.name}</div>
          <div className="text-xs text-muted-foreground">{x.aiName}</div>
        </div>
      ),
    },
    {
      key: 'backend',
      title: t('Backend'),
      cell: (x) => (
        <Badge variant="outline">
          <IconDocker size={13} className="mr-1" />
          {x.backendType === 1 ? t('Docker') : t('Other')}
        </Badge>
      ),
    },
    {
      key: 'endpoint',
      title: t('Endpoint'),
      className: 'min-w-56',
      cell: (x) => (
        <code className="text-xs">{x.endpoint || t('System default')}</code>
      ),
    },
    {
      key: 'description',
      title: t('Description'),
      className: 'min-w-48',
      cell: (x) => x.description || EMPTY_VALUE,
    },
    {
      key: 'credential',
      title: t('Credential'),
      cell: (x) => (x.hasCredential ? t('Configured') : EMPTY_VALUE),
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
      key: 'templates',
      title: t('Templates'),
      cell: (x) =>
        templates.filter((item) => item.runtimeNodeId === x.id).length,
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
      className: 'w-32',
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
            title={x.isEnabled ? t('Disable') : t('Enable')}
            onClick={() => onToggle(x).catch(() => null)}
          >
            <IconCheck size={16} />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            title={t('Delete')}
            onClick={() =>
              onDeleteRequest({ kind: 'runtime', id: x.id, label: x.name })
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
            placeholder={t('Search runtime nodes')}
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
                {t('Add runtime node')}
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
        emptyText={t('No runtime nodes found.')}
      />

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
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </Label>
            <Label>
              {t('AI name')}
              <Input
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
