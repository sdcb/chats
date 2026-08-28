import { useCallback, useEffect, useState } from 'react';

import { createFetchClient } from '@/hooks/createFetchClient';
import useTranslation from '@/hooks/useTranslation';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

type RuntimeNode = {
  id: number;
  name: string;
  aiName: string;
  description: string | null;
  backendType: number;
  endpoint: string | null;
  credential: string | null;
  isEnabled: boolean;
};
type ImageEntry = {
  image: string;
  description: string | null;
  isEnabled: boolean;
};
type Template = {
  id: number;
  name: string;
  image: string;
  runtimeNodeId: number;
  runtimeNode?: RuntimeNode;
  visibility: number;
  cpuCores: number;
  memoryBytes: number;
  maxProcesses: number;
  backendNetworkName: string | null;
  defaultVolumeBytes: number | null;
};
type Quota = {
  userId: number | null;
  allowCustomImage: boolean;
  allowedNetworkModes: string;
  maxContainerCount: number | null;
  maxCpuCores: number | null;
};

const blankTemplate = {
  name: '',
  runtimeNodeId: 0,
  image: 'code-interpreter:latest',
  cpuCores: 2,
  memoryBytes: 2147483648,
  maxProcesses: 200,
  backendNetworkName: 'bridge',
  defaultVolumeBytes: null as number | null,
  visibility: 3,
};

export default function AdminContainersPage() {
  const { t } = useTranslation();
  const client = createFetchClient();
  const [nodes, setNodes] = useState<RuntimeNode[]>([]);
  const [images, setImages] = useState<ImageEntry[]>([]);
  const [templates, setTemplates] = useState<Template[]>([]);
  const [quotas, setQuotas] = useState<Quota[]>([]);
  const [loading, setLoading] = useState(true);
  const [nodeForm, setNodeForm] = useState({
    name: '',
    aiName: '',
    endpoint: '',
  });
  const [imageForm, setImageForm] = useState({ image: '', description: '' });
  const [templateForm, setTemplateForm] = useState(blankTemplate);
  const [quotaForm, setQuotaForm] = useState({
    allowCustomImage: false,
    allowedNetworkModes: 'none,bridge',
    maxContainerCount: '',
    maxCpuCores: '',
  });

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const [runtimeNodes, catalogImages, resourceTemplates, userQuotas] =
        await Promise.all([
          client.get<RuntimeNode[]>(
            '/api/admin/container-catalog/runtime-nodes',
          ),
          client.get<ImageEntry[]>('/api/admin/container-catalog/images'),
          client.get<Template[]>('/api/admin/container-catalog/templates'),
          client.get<Quota[]>('/api/admin/container-catalog/quotas'),
        ]);
      setNodes(runtimeNodes);
      setImages(catalogImages);
      setTemplates(resourceTemplates);
      setQuotas(userQuotas);
      if (runtimeNodes.length > 0)
        setTemplateForm((value) =>
          value.runtimeNodeId
            ? value
            : { ...value, runtimeNodeId: runtimeNodes[0].id },
        );
    } finally {
      setLoading(false);
    }
  }, [client]);
  useEffect(() => {
    refresh().catch(() => setLoading(false));
  }, [refresh]);

  const saveNode = async () => {
    await client.post('/api/admin/container-catalog/runtime-nodes', {
      body: {
        ...nodeForm,
        aiName: nodeForm.aiName || nodeForm.name,
        endpoint: nodeForm.endpoint.trim() || null,
        description: null,
        backendType: 1,
        credential: null,
        isEnabled: true,
      },
    });
    setNodeForm({ name: '', aiName: '', endpoint: '' });
    await refresh();
  };
  const saveImage = async () => {
    if (!imageForm.image.trim()) return;
    await client.put(
      `/api/admin/container-catalog/images/${encodeURIComponent(
        imageForm.image.trim(),
      )}`,
      { body: { description: imageForm.description || null, isEnabled: true } },
    );
    setImageForm({ image: '', description: '' });
    await refresh();
  };
  const saveTemplate = async () => {
    await client.post('/api/admin/container-catalog/templates', {
      body: templateForm,
    });
    setTemplateForm(blankTemplate);
    await refresh();
  };
  const toggleNode = async (node: RuntimeNode) => {
    await client.patch(`/api/admin/container-catalog/runtime-nodes/${node.id}/enabled`, { body: { isEnabled: !node.isEnabled } });
    await refresh();
  };
  const toggleImage = async (entry: ImageEntry) => {
    await client.put(
      `/api/admin/container-catalog/images/${encodeURIComponent(entry.image)}`,
      { body: { description: entry.description, isEnabled: !entry.isEnabled } },
    );
    await refresh();
  };
  const removeImage = async (entry: ImageEntry) => {
    await client.delete(`/api/admin/container-catalog/images/${encodeURIComponent(entry.image)}`);
    await refresh();
  };
  const removeTemplate = async (template: Template) => {
    await client.delete(`/api/admin/container-catalog/templates/${template.id}`);
    await refresh();
  };
  const saveQuota = async () => {
    await client.put('/api/admin/container-catalog/quotas', {
      body: {
        allowCustomImage: quotaForm.allowCustomImage,
        allowedNetworkModes: quotaForm.allowedNetworkModes,
        maxContainerCount: quotaForm.maxContainerCount
          ? Number(quotaForm.maxContainerCount)
          : null,
        maxCpuCores: quotaForm.maxCpuCores
          ? Number(quotaForm.maxCpuCores)
          : null,
        maxMemoryBytes: null,
        maxContainerProcesses: null,
        maxVolumeBytes: null,
        maxContainerCpuCores: null,
        maxContainerMemoryBytes: null,
        maxVolumeBytesPerVolume: null,
      },
    });
    await refresh();
  };

  return (
    <main className="mx-auto max-w-6xl p-6 space-y-6">
      <h1 className="text-2xl font-semibold">
        {t('Container administration')}
      </h1>
      <section className="rounded-lg border p-4 space-y-3">
        <h2 className="font-medium">{t('Runtime nodes')}</h2>
        <div className="grid gap-2 sm:grid-cols-4">
          <Input
            placeholder={t('Name')}
            value={nodeForm.name}
            onChange={(e) => setNodeForm({ ...nodeForm, name: e.target.value })}
          />
          <Input
            placeholder={t('AI name')}
            value={nodeForm.aiName}
            onChange={(e) =>
              setNodeForm({ ...nodeForm, aiName: e.target.value })
            }
          />
          <Input
            placeholder={t('Endpoint')}
            value={nodeForm.endpoint}
            onChange={(e) =>
              setNodeForm({ ...nodeForm, endpoint: e.target.value })
            }
          />
          <p className="text-xs text-muted-foreground sm:col-span-3">
            {t('Leave endpoint blank to use the host operating system default.')}
          </p>
          <Button onClick={() => saveNode().catch(() => null)}>
            {t('Add')}
          </Button>
        </div>
        {loading ? (
          <p>{t('Loading...')}</p>
        ) : (
          nodes.map((node) => (
            <div
              key={node.id}
              className="flex justify-between border-b py-2 text-sm"
            >
              <span>
                {node.name} ({node.aiName}) ·{' '}
                {node.endpoint ?? t('System default')}
              </span>
              <Button
                size="sm"
                variant="outline"
                onClick={() => toggleNode(node).catch(() => null)}
              >
                {node.isEnabled ? t('Disable') : t('Enable')}
              </Button>
            </div>
          ))
        )}
      </section>
      <section className="rounded-lg border p-4 space-y-3">
        <h2 className="font-medium">{t('Image catalog')}</h2>
        <div className="grid gap-2 sm:grid-cols-3">
          <Input
            placeholder="image:tag"
            value={imageForm.image}
            onChange={(e) =>
              setImageForm({ ...imageForm, image: e.target.value })
            }
          />
          <Input
            placeholder={t('Description')}
            value={imageForm.description}
            onChange={(e) =>
              setImageForm({ ...imageForm, description: e.target.value })
            }
          />
          <Button onClick={() => saveImage().catch(() => null)}>
            {t('Save')}
          </Button>
        </div>
        {images.map((entry) => (
          <div
            key={entry.image}
            className="flex justify-between border-b py-2 text-sm"
          >
            <span>
              {entry.image} · {entry.description}
            </span>
            <Button
              size="sm"
              variant="outline"
              onClick={() => toggleImage(entry).catch(() => null)}
            >
              {entry.isEnabled ? t('Disable') : t('Enable')}
            </Button>
            <Button size="sm" variant="destructive" onClick={() => removeImage(entry).catch(() => null)}>{t('Delete')}</Button>
          </div>
        ))}
      </section>
      <section className="rounded-lg border p-4 space-y-3">
        <h2 className="font-medium">{t('Templates')}</h2>
        <div className="grid gap-2 sm:grid-cols-4">
          <Input
            placeholder={t('Name')}
            value={templateForm.name}
            onChange={(e) =>
              setTemplateForm({ ...templateForm, name: e.target.value })
            }
          />
          <Input
            placeholder="image:tag"
            value={templateForm.image}
            onChange={(e) =>
              setTemplateForm({ ...templateForm, image: e.target.value })
            }
          />
          <Input
            type="number"
            placeholder="CPU"
            value={templateForm.cpuCores}
            onChange={(e) =>
              setTemplateForm({
                ...templateForm,
                cpuCores: Number(e.target.value),
              })
            }
          />
          <Button onClick={() => saveTemplate().catch(() => null)}>
            {t('Add')}
          </Button>
        </div>
        <div className="grid gap-2 sm:grid-cols-3">
          <Label>
            {t('Runtime node')}
            <select
              className="h-10 w-full rounded-md border px-2"
              value={templateForm.runtimeNodeId}
              onChange={(e) =>
                setTemplateForm({
                  ...templateForm,
                  runtimeNodeId: Number(e.target.value),
                })
              }
            >
              {nodes.map((node) => (
                <option key={node.id} value={node.id}>
                  {node.aiName}
                </option>
              ))}
            </select>
          </Label>
          <Label>
            {t('Memory bytes')}
            <Input
              type="number"
              value={templateForm.memoryBytes}
              onChange={(e) =>
                setTemplateForm({
                  ...templateForm,
                  memoryBytes: Number(e.target.value),
                })
              }
            />
          </Label>
          <Label>
            {t('Visibility (1=user, 2=AI)')}
            <Input
              type="number"
              min="0"
              max="3"
              value={templateForm.visibility}
              onChange={(e) =>
                setTemplateForm({
                  ...templateForm,
                  visibility: Number(e.target.value),
                })
              }
            />
          </Label>
        </div>
        {templates.map((template) => (
          <div key={template.id} className="flex justify-between border-b py-2 text-sm">
            <span>{template.name} · {template.image} · visibility {template.visibility}</span>
            <Button size="sm" variant="destructive" onClick={() => removeTemplate(template).catch(() => null)}>{t('Delete')}</Button>
          </div>
        ))}
      </section>
      <section className="rounded-lg border p-4 space-y-3">
        <h2 className="font-medium">{t('Container quotas')}</h2>
        <div className="grid gap-2 sm:grid-cols-4">
          <Input
            placeholder={t('Allowed network modes')}
            value={quotaForm.allowedNetworkModes}
            onChange={(e) =>
              setQuotaForm({
                ...quotaForm,
                allowedNetworkModes: e.target.value,
              })
            }
          />
          <Input
            type="number"
            placeholder={t('Max containers')}
            value={quotaForm.maxContainerCount}
            onChange={(e) =>
              setQuotaForm({ ...quotaForm, maxContainerCount: e.target.value })
            }
          />
          <Input
            type="number"
            placeholder={t('Max CPU cores')}
            value={quotaForm.maxCpuCores}
            onChange={(e) =>
              setQuotaForm({ ...quotaForm, maxCpuCores: e.target.value })
            }
          />
          <Button onClick={() => saveQuota().catch(() => null)}>
            {t('Save global default')}
          </Button>
        </div>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={quotaForm.allowCustomImage}
            onChange={(e) =>
              setQuotaForm({ ...quotaForm, allowCustomImage: e.target.checked })
            }
          />
          {t('Allow custom images')}
        </label>
        {quotas.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            {t('No quota overrides configured.')}
          </p>
        ) : (
          quotas.map((quota) => (
            <div
              key={quota.userId ?? 'default'}
              className="border-b py-2 text-sm"
            >
              {quota.userId === null
                ? t('Global default')
                : `${t('User')} ${quota.userId}`}{' '}
              · {t('Containers')}: {quota.maxContainerCount ?? t('Unlimited')} ·{' '}
              {t('CPU')}: {quota.maxCpuCores ?? t('Unlimited')} ·{' '}
              {quota.allowCustomImage
                ? t('Custom images allowed')
                : t('Catalog images only')}{' '}
              · {quota.allowedNetworkModes}
            </div>
          ))
        )}
      </section>
    </main>
  );
}
