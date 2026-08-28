import { Dispatch, SetStateAction } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconDocker, IconEdit, IconPlus, IconTrash } from '@/components/Icons';
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
  onDeleteRequest: (target: DeleteTarget) => void;
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
  onDeleteRequest,
}: Props) {
  const { t } = useTranslation();

  return (
    <>
      <section className="space-y-4">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold">{t('Runtime nodes')}</h2>
            <p className="text-sm text-muted-foreground">
              {t(
                'Configure the Docker daemon connection and runtime identity.',
              )}
            </p>
          </div>
          <IconActionButton
            label={t('Add runtime node')}
            icon={<IconPlus size={18} />}
            onClick={onNew}
          />
        </div>

        {loading ? (
          <div className="grid gap-4 md:grid-cols-2">
            {[1, 2].map((item) => (
              <Card key={item} className="animate-pulse">
                <CardHeader className="h-20" />
                <CardContent className="h-36" />
              </Card>
            ))}
          </div>
        ) : nodes.length === 0 ? (
          <div className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
            {t('No runtime nodes found.')}
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {nodes.map((node) => {
              const templateCount = templates.filter(
                (template) => template.runtimeNodeId === node.id,
              ).length;
              return (
                <Card key={node.id}>
                  <CardHeader className="flex flex-row items-start justify-between space-y-0 p-4">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <IconDocker size={18} className="shrink-0" />
                        <h3 className="truncate font-semibold">{node.name}</h3>
                      </div>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {node.aiName}
                      </p>
                    </div>
                    <div className="flex shrink-0 items-center gap-1">
                      <LabelSwitch
                        checked={node.isEnabled}
                        onCheckedChange={(checked) => {
                          if (checked !== node.isEnabled) {
                            onToggle(node).catch(() => null);
                          }
                        }}
                        label={node.isEnabled ? t('Enabled') : t('Disabled')}
                        className="gap-1"
                        labelClassName="text-xs"
                        switchClassName="scale-75"
                      />
                      <IconActionButton
                        label={t('Edit')}
                        icon={<IconEdit size={15} />}
                        className="h-8 w-8"
                        onClick={() => onEdit(node)}
                      />
                      <IconActionButton
                        label={t('Delete')}
                        icon={<IconTrash size={15} />}
                        className="h-8 w-8"
                        onClick={() =>
                          onDeleteRequest({
                            kind: 'runtime',
                            id: node.id,
                            label: node.name,
                          })
                        }
                      />
                    </div>
                  </CardHeader>
                  <CardContent className="p-4 pt-0">
                    <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
                      <CatalogCardField label={t('Backend')}>
                        <Badge variant="outline">
                          <IconDocker size={12} className="mr-1" />
                          {node.backendType === 1 ? t('Docker') : t('Other')}
                        </Badge>
                      </CatalogCardField>
                      <CatalogCardField label={t('Status')}>
                        {node.isEnabled ? t('Enabled') : t('Disabled')}
                      </CatalogCardField>
                      <CatalogCardField label={t('Endpoint')} mono>
                        {node.endpoint || t('System default')}
                      </CatalogCardField>
                      <CatalogCardField label={t('Credential')}>
                        {node.hasCredential ? t('Configured') : EMPTY_VALUE}
                      </CatalogCardField>
                      <CatalogCardField label={t('Templates')}>
                        {templateCount}
                      </CatalogCardField>
                      <CatalogCardField label={t('Updated')}>
                        {formatDateTime(node.updatedAt)}
                      </CatalogCardField>
                      <CatalogCardField
                        label={t('Created')}
                        className="col-span-2"
                      >
                        {formatDateTime(node.createdAt)}
                      </CatalogCardField>
                      <CatalogCardField
                        label={t('Description')}
                        className="col-span-2"
                      >
                        {node.description || EMPTY_VALUE}
                      </CatalogCardField>
                    </dl>
                  </CardContent>
                </Card>
              );
            })}
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
