import { Dispatch, SetStateAction } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconEdit, IconPlus, IconTrash } from '@/components/Icons';
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
  onDeleteRequest: (target: DeleteTarget) => void;
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
  onDeleteRequest,
}: Props) {
  const { t } = useTranslation();

  return (
    <>
      <section className="space-y-4">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold">{t('Resource templates')}</h2>
            <p className="text-sm text-muted-foreground">
              {t(
                'Define the image, resource limits and visibility for container creation.',
              )}
            </p>
          </div>
          <IconActionButton
            label={t('Add template')}
            icon={<IconPlus size={18} />}
            onClick={onNew}
          />
        </div>

        {loading ? (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {[1, 2, 3].map((item) => (
              <Card key={item} className="animate-pulse">
                <CardHeader className="h-20" />
                <CardContent className="h-44" />
              </Card>
            ))}
          </div>
        ) : templates.length === 0 ? (
          <div className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
            {t('No resource templates found.')}
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {templates.map((template) => (
              <Card key={template.id}>
                <CardHeader className="flex flex-row items-start justify-between space-y-0 p-4">
                  <div className="min-w-0">
                    <h3 className="truncate font-semibold">{template.name}</h3>
                    <code className="mt-1 block truncate text-xs text-muted-foreground">
                      {template.image}
                    </code>
                  </div>
                  <div className="flex shrink-0 items-center gap-1">
                    <IconActionButton
                      label={t('Edit')}
                      icon={<IconEdit size={15} />}
                      className="h-8 w-8"
                      onClick={() => onEdit(template)}
                    />
                    <IconActionButton
                      label={t('Delete')}
                      icon={<IconTrash size={15} />}
                      className="h-8 w-8"
                      onClick={() =>
                        onDeleteRequest({
                          kind: 'template',
                          id: template.id,
                          label: template.name,
                        })
                      }
                    />
                  </div>
                </CardHeader>
                <CardContent className="p-4 pt-0">
                  <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
                    <CatalogCardField label={t('Runtime node')}>
                      {template.runtimeNode?.aiName ||
                        `#${template.runtimeNodeId}`}
                    </CatalogCardField>
                    <CatalogCardField label={t('Visibility')}>
                      <Badge variant="outline">
                        {template.visibility === 3
                          ? t('Users and AI')
                          : template.visibility === 1
                          ? t('Users')
                          : template.visibility === 2
                          ? t('AI')
                          : t('Hidden')}
                      </Badge>
                    </CatalogCardField>
                    <CatalogCardField label={t('CPU cores')}>
                      {template.cpuCores}
                    </CatalogCardField>
                    <CatalogCardField label={t('Memory bytes')}>
                      {formatBytes(template.memoryBytes)}
                    </CatalogCardField>
                    <CatalogCardField label={t('Max processes')}>
                      {template.maxProcesses}
                    </CatalogCardField>
                    <CatalogCardField label={t('Network')}>
                      {template.backendNetworkName || t('Default')}
                    </CatalogCardField>
                    <CatalogCardField label={t('Default volume bytes')}>
                      {formatBytes(template.defaultVolumeBytes)}
                    </CatalogCardField>
                    <CatalogCardField label={t('Updated')}>
                      {formatDateTime(template.updatedAt)}
                    </CatalogCardField>
                    <CatalogCardField label={t('Created')}>
                      {formatDateTime(template.createdAt)}
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
