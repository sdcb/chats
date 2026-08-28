import { useCallback, useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { ContainerResource, ContainerTemplate } from '@/types/containers';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

import {
  createContainer,
  deleteContainer,
  listContainerTemplates,
  listContainers,
  startContainer,
  stopContainer,
  updateContainer,
} from '@/apis/containersApi';

export default function ContainersPage() {
  const { t } = useTranslation();
  const [resources, setResources] = useState<ContainerResource[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [templates, setTemplates] = useState<ContainerTemplate[]>([]);
  const [templateId, setTemplateId] = useState<number | null>(null);
  const [name, setName] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState({
    cpuCores: '',
    memoryBytes: '',
    maxProcesses: '',
    backendNetworkName: '',
  });

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      setResources(await listContainers());
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh().catch(() => setResources([]));
    listContainerTemplates()
      .then((items) => {
        setTemplates(items);
        setTemplateId(items[0]?.id ?? null);
      })
      .catch(() => setTemplates([]));
  }, [refresh]);

  const changeState = async (resource: ContainerResource) => {
    setBusyId(resource.encryptedId);
    try {
      if (resource.isStopped) await startContainer(resource.encryptedId);
      else await stopContainer(resource.encryptedId);
      await refresh();
    } finally {
      setBusyId(null);
    }
  };

  const remove = async (resource: ContainerResource) => {
    setBusyId(resource.encryptedId);
    try {
      await deleteContainer(resource.encryptedId);
      await refresh();
    } finally {
      setBusyId(null);
    }
  };

  const createDefault = async () => {
    if (templateId === null) return;
    await createContainer({
      name: name || null,
      isPermanent: true,
      templateId,
    });
    setName('');
    await refresh();
  };
  const beginEdit = (resource: ContainerResource) => {
    setEditingId(resource.encryptedId);
    setEditForm({
      cpuCores: resource.cpuCores?.toString() ?? '',
      memoryBytes: resource.memoryBytes?.toString() ?? '',
      maxProcesses: resource.maxProcesses?.toString() ?? '',
      backendNetworkName: resource.backendNetworkName ?? '',
    });
  };
  const saveEdit = async () => {
    if (!editingId) return;
    await updateContainer(editingId, {
      cpuCores: editForm.cpuCores ? Number(editForm.cpuCores) : null,
      memoryBytes: editForm.memoryBytes ? Number(editForm.memoryBytes) : null,
      maxProcesses: editForm.maxProcesses
        ? Number(editForm.maxProcesses)
        : null,
      backendNetworkName: editForm.backendNetworkName || null,
    });
    setEditingId(null);
    await refresh();
  };

  return (
    <main className="mx-auto max-w-5xl p-6 space-y-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('Docker resources')}</h1>
          <p className="text-muted-foreground">
            {t('Manage permanent and temporary Docker containers.')}
          </p>
        </div>
      </header>
      <section className="rounded-lg border p-4 space-y-4">
        <h2 className="font-medium">{t('Create permanent Docker')}</h2>
        <div className="grid gap-4 sm:grid-cols-3">
          <div className="space-y-2">
            <Label htmlFor="container-template">{t('Template')}</Label>
            <select
              id="container-template"
              className="h-10 w-full rounded-md border bg-background px-3 text-sm"
              value={templateId ?? ''}
              onChange={(event) => setTemplateId(Number(event.target.value))}
            >
              {templates.map((template) => (
                <option key={template.id} value={template.id}>
                  {template.name} · {template.runtimeNodeAIName}
                </option>
              ))}
            </select>
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor="container-name">{t('Name')}</Label>
            <Input
              id="container-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder={t('Optional container name')}
            />
          </div>
        </div>
        {templateId !== null &&
          (() => {
            const selected = templates.find(
              (template) => template.id === templateId,
            );
            return selected ? (
              <p className="text-sm text-muted-foreground">
                {selected.image} · CPU {selected.cpuCores} ·{' '}
                {Math.round(selected.memoryBytes / 1024 / 1024)} MiB · PID{' '}
                {selected.maxProcesses} ·{' '}
                {selected.backendNetworkName ?? t('Backend default network')}
              </p>
            ) : null;
          })()}
        <Button
          disabled={templateId === null}
          onClick={() => createDefault().catch(() => null)}
        >
          {t('Create permanent Docker')}
        </Button>
      </section>
      {loading ? (
        <p>{t('Loading...')}</p>
      ) : resources.length === 0 ? (
        <p>{t('No Docker resources.')}</p>
      ) : (
        <div className="space-y-3">
          {resources.map((resource) => (
            <section
              key={resource.encryptedId}
              className="rounded-lg border p-4 flex flex-col gap-3"
            >
              <div className="flex items-center justify-between gap-4">
                <div>
                  <h2 className="font-medium">{resource.name}</h2>
                  <p className="text-sm text-muted-foreground">
                    {resource.image} ·{' '}
                    {resource.isPermanent ? t('Permanent') : t('Temporary')} ·{' '}
                    {resource.isStopped ? t('Stopped') : t('Running')}
                  </p>
                </div>
                <div className="flex gap-2">
                  <Button
                    disabled={busyId === resource.encryptedId}
                    variant="outline"
                    onClick={() => beginEdit(resource)}
                  >
                    {t('Edit')}
                  </Button>
                  <Button
                    disabled={busyId === resource.encryptedId}
                    variant="outline"
                    onClick={() => changeState(resource).catch(() => null)}
                  >
                    {resource.isStopped ? t('Start') : t('Stop')}
                  </Button>
                  <Button
                    disabled={busyId === resource.encryptedId}
                    variant="destructive"
                    onClick={() => remove(resource).catch(() => null)}
                  >
                    {t('Delete')}
                  </Button>
                </div>
              </div>
              {editingId === resource.encryptedId && (
                <div className="col-span-full grid gap-2 sm:grid-cols-4 border-t pt-3">
                  <Input
                    type="number"
                    placeholder={t('CPU cores')}
                    value={editForm.cpuCores}
                    onChange={(e) =>
                      setEditForm({ ...editForm, cpuCores: e.target.value })
                    }
                  />
                  <Input
                    type="number"
                    placeholder={t('Memory bytes')}
                    value={editForm.memoryBytes}
                    onChange={(e) =>
                      setEditForm({ ...editForm, memoryBytes: e.target.value })
                    }
                  />
                  <Input
                    type="number"
                    placeholder={t('Max processes')}
                    value={editForm.maxProcesses}
                    onChange={(e) =>
                      setEditForm({ ...editForm, maxProcesses: e.target.value })
                    }
                  />
                  <div className="flex gap-2">
                    <Input
                      placeholder={t('Network')}
                      value={editForm.backendNetworkName}
                      onChange={(e) =>
                        setEditForm({
                          ...editForm,
                          backendNetworkName: e.target.value,
                        })
                      }
                    />
                    <Button onClick={() => saveEdit().catch(() => null)}>
                      {t('Save')}
                    </Button>
                  </div>
                </div>
              )}
            </section>
          ))}
        </div>
      )}
    </main>
  );
}
