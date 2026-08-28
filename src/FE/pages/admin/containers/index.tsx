import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import { createFetchClient } from '@/hooks/createFetchClient';
import useTranslation from '@/hooks/useTranslation';

import {
  IconCheck,
  IconDocker,
  IconEdit,
  IconFiles,
  IconInfo,
  IconPlus,
  IconRefresh,
  IconSettings,
  IconTrash,
  IconWorld,
} from '@/components/Icons';
import {
  UnifiedTable,
  UnifiedTableColumn,
} from '@/components/table/UnifiedTable';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import { cn } from '@/lib/utils';

type RuntimeNode = {
  id: number;
  name: string;
  aiName: string;
  description: string | null;
  backendType: number;
  endpoint: string | null;
  hasCredential: boolean;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
};
type RuntimeTemplate = {
  id: number;
  name: string;
  runtimeNodeId: number;
  image: string;
  cpuCores: number;
  memoryBytes: number;
  maxProcesses: number;
  backendNetworkName: string | null;
  defaultVolumeBytes: number | null;
  visibility: number;
  createdAt: string;
  updatedAt: string;
  runtimeNode?: RuntimeNode | null;
};
type ImageEntry = {
  image: string;
  description: string | null;
  isEnabled: boolean;
};
type Quota = {
  id: number;
  userId: number | null;
  userName: string | null;
  allowCustomImage: boolean;
  allowedNetworkModes: string;
  maxContainerCount: number | null;
  maxCpuCores: number | null;
  maxMemoryBytes: number | null;
  maxContainerProcesses: number | null;
  maxVolumeBytes: number | null;
  maxContainerCpuCores: number | null;
  maxContainerMemoryBytes: number | null;
  maxVolumeBytesPerVolume: number | null;
  updatedAt: string;
};
type RuntimeForm = {
  name: string;
  aiName: string;
  description: string;
  backendType: number;
  endpoint: string;
  credential: string;
  isEnabled: boolean;
};
type TemplateForm = {
  name: string;
  runtimeNodeId: number;
  image: string;
  cpuCores: number;
  memoryBytes: number;
  maxProcesses: number;
  backendNetworkName: string;
  defaultVolumeBytes: number | null;
  visibility: number;
};
type ImageForm = { image: string; description: string; isEnabled: boolean };
type QuotaForm = {
  allowCustomImage: boolean;
  allowedNetworkModes: string;
  maxContainerCount: string;
  maxCpuCores: string;
  maxMemoryBytes: string;
  maxContainerProcesses: string;
  maxVolumeBytes: string;
  maxContainerCpuCores: string;
  maxContainerMemoryBytes: string;
  maxVolumeBytesPerVolume: string;
};
type DeleteTarget = {
  kind: 'runtime' | 'template' | 'image';
  id: number | string;
  label: string;
};

const PAGE_SIZE = 20;

const emptyRuntime: RuntimeForm = {
  name: '',
  aiName: '',
  description: '',
  backendType: 1,
  endpoint: '',
  credential: '',
  isEnabled: true,
};
const emptyTemplate: TemplateForm = {
  name: '',
  runtimeNodeId: 0,
  image: 'code-interpreter:latest',
  cpuCores: 2,
  memoryBytes: 2147483648,
  maxProcesses: 200,
  backendNetworkName: 'bridge',
  defaultVolumeBytes: null,
  visibility: 3,
};
const emptyImage: ImageForm = { image: '', description: '', isEnabled: true };
const emptyQuota: QuotaForm = {
  allowCustomImage: false,
  allowedNetworkModes: 'none,bridge',
  maxContainerCount: '',
  maxCpuCores: '',
  maxMemoryBytes: '',
  maxContainerProcesses: '',
  maxVolumeBytes: '',
  maxContainerCpuCores: '',
  maxContainerMemoryBytes: '',
  maxVolumeBytesPerVolume: '',
};

const formatDateTime = (value?: string) => {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? value : date.toLocaleString();
};
const formatBytes = (value: number | null | undefined) => {
  if (value == null) return '—';
  if (value === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const index = Math.min(
    Math.floor(Math.log(value) / Math.log(1024)),
    units.length - 1,
  );
  return `${(value / 1024 ** index).toFixed(index === 0 ? 0 : 1)} ${
    units[index]
  }`;
};
const limitText = (value: number | null | undefined, suffix = '') =>
  value == null ? '—' : `${value}${suffix}`;

export default function AdminContainersPage() {
  const { t } = useTranslation();
  const client = useMemo(() => createFetchClient(), []);
  const [nodes, setNodes] = useState<RuntimeNode[]>([]);
  const [templates, setTemplates] = useState<RuntimeTemplate[]>([]);
  const [images, setImages] = useState<ImageEntry[]>([]);
  const [quotas, setQuotas] = useState<Quota[]>([]);
  const [loading, setLoading] = useState(true);
  const [runtimePage, setRuntimePage] = useState(1);
  const [templatePage, setTemplatePage] = useState(1);
  const [imagePage, setImagePage] = useState(1);
  const [quotaPage, setQuotaPage] = useState(1);
  const [activeTab, setActiveTab] = useState('runtime');
  const [runtimeSearch, setRuntimeSearch] = useState('');
  const [templateSearch, setTemplateSearch] = useState('');
  const [imageSearch, setImageSearch] = useState('');
  const [runtimeDialog, setRuntimeDialog] = useState<number | 'new' | null>(
    null,
  );
  const [templateDialog, setTemplateDialog] = useState<number | 'new' | null>(
    null,
  );
  const [imageDialog, setImageDialog] = useState<string | 'new' | null>(null);
  const [quotaDialog, setQuotaDialog] = useState<number | null>(null);
  const [runtimeForm, setRuntimeForm] = useState<RuntimeForm>(emptyRuntime);
  const [templateForm, setTemplateForm] = useState<TemplateForm>(emptyTemplate);
  const [imageForm, setImageForm] = useState<ImageForm>(emptyImage);
  const [quotaForm, setQuotaForm] = useState<QuotaForm>(emptyQuota);
  const [saving, setSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<DeleteTarget | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const [nextNodes, nextTemplates, nextImages, nextQuotas] =
        await Promise.all([
          client.get<RuntimeNode[]>(
            '/api/admin/container-catalog/runtime-nodes',
          ),
          client.get<RuntimeTemplate[]>(
            '/api/admin/container-catalog/templates',
          ),
          client.get<ImageEntry[]>('/api/admin/container-catalog/images'),
          client.get<Quota[]>('/api/admin/container-catalog/quotas'),
        ]);
      setNodes(nextNodes);
      setTemplates(nextTemplates);
      setImages(nextImages);
      setQuotas(nextQuotas);
      setRuntimePage(1);
      setTemplatePage(1);
      setImagePage(1);
      setQuotaPage(1);
      setTemplateForm((current) =>
        current.runtimeNodeId || !nextNodes.length
          ? current
          : { ...current, runtimeNodeId: nextNodes[0].id },
      );
    } finally {
      setLoading(false);
    }
  }, [client]);
  useEffect(() => {
    refresh().catch(() => setLoading(false));
  }, [refresh]);

  const openNewRuntime = () => {
    setRuntimeForm(emptyRuntime);
    setRuntimeDialog('new');
  };
  const openEditRuntime = (node: RuntimeNode) => {
    setRuntimeForm({
      name: node.name,
      aiName: node.aiName,
      description: node.description || '',
      backendType: node.backendType,
      endpoint: node.endpoint || '',
      // The API deliberately exposes only whether a credential exists. Keep
      // the password field blank so an edit cannot leak or overwrite it.
      credential: '',
      isEnabled: node.isEnabled,
    });
    setRuntimeDialog(node.id);
  };
  const openNewTemplate = () => {
    setTemplateForm({ ...emptyTemplate, runtimeNodeId: nodes[0]?.id || 0 });
    setTemplateDialog('new');
  };
  const openEditTemplate = (item: RuntimeTemplate) => {
    setTemplateForm({
      name: item.name,
      runtimeNodeId: item.runtimeNodeId,
      image: item.image,
      cpuCores: item.cpuCores,
      memoryBytes: item.memoryBytes,
      maxProcesses: item.maxProcesses,
      backendNetworkName: item.backendNetworkName || '',
      defaultVolumeBytes: item.defaultVolumeBytes,
      visibility: item.visibility,
    });
    setTemplateDialog(item.id);
  };
  const openNewImage = () => {
    setImageForm(emptyImage);
    setImageDialog('new');
  };
  const openEditImage = (item: ImageEntry) => {
    setImageForm({
      image: item.image,
      description: item.description || '',
      isEnabled: item.isEnabled,
    });
    setImageDialog(item.image);
  };
  const openEditQuota = (quota: Quota) => {
    const asText = (value: number | null) =>
      value == null ? '' : String(value);
    setQuotaForm({
      allowCustomImage: quota.allowCustomImage,
      allowedNetworkModes: quota.allowedNetworkModes,
      maxContainerCount: asText(quota.maxContainerCount),
      maxCpuCores: asText(quota.maxCpuCores),
      maxMemoryBytes: asText(quota.maxMemoryBytes),
      maxContainerProcesses: asText(quota.maxContainerProcesses),
      maxVolumeBytes: asText(quota.maxVolumeBytes),
      maxContainerCpuCores: asText(quota.maxContainerCpuCores),
      maxContainerMemoryBytes: asText(quota.maxContainerMemoryBytes),
      maxVolumeBytesPerVolume: asText(quota.maxVolumeBytesPerVolume),
    });
    setQuotaDialog(quota.id);
  };
  const openNewQuota = () => {
    setQuotaForm(emptyQuota);
    setQuotaDialog(0);
  };

  const saveRuntime = async () => {
    if (!runtimeForm.name.trim() || !runtimeForm.aiName.trim()) return;
    setSaving(true);
    try {
      const body = {
        name: runtimeForm.name.trim(),
        aiName: runtimeForm.aiName.trim(),
        description: runtimeForm.description.trim() || null,
        backendType: runtimeForm.backendType,
        endpoint: runtimeForm.endpoint.trim() || null,
        credential: runtimeForm.credential.trim() || null,
        isEnabled: runtimeForm.isEnabled,
      };
      if (runtimeDialog === 'new')
        await client.post('/api/admin/container-catalog/runtime-nodes', {
          body,
        });
      else if (runtimeDialog != null)
        await client.put(
          `/api/admin/container-catalog/runtime-nodes/${runtimeDialog}`,
          { body },
        );
      toast.success(t('Save successful'));
      setRuntimeDialog(null);
      await refresh();
    } finally {
      setSaving(false);
    }
  };
  const saveTemplate = async () => {
    if (
      !templateForm.name.trim() ||
      !templateForm.image.trim() ||
      !templateForm.runtimeNodeId
    )
      return;
    setSaving(true);
    try {
      const body = {
        ...templateForm,
        name: templateForm.name.trim(),
        image: templateForm.image.trim(),
        backendNetworkName: templateForm.backendNetworkName.trim() || null,
      };
      if (templateDialog === 'new')
        await client.post('/api/admin/container-catalog/templates', { body });
      else if (templateDialog != null)
        await client.put(
          `/api/admin/container-catalog/templates/${templateDialog}`,
          { body },
        );
      toast.success(t('Save successful'));
      setTemplateDialog(null);
      await refresh();
    } finally {
      setSaving(false);
    }
  };
  const saveImage = async () => {
    if (!imageForm.image.trim()) return;
    setSaving(true);
    try {
      await client.put(
        `/api/admin/container-catalog/images/${encodeURIComponent(
          imageForm.image.trim(),
        )}`,
        {
          body: {
            description: imageForm.description.trim() || null,
            isEnabled: imageForm.isEnabled,
          },
        },
      );
      toast.success(t('Save successful'));
      setImageDialog(null);
      await refresh();
    } finally {
      setSaving(false);
    }
  };
  const saveQuota = async () => {
    const numberOrNull = (value: string) =>
      value.trim() ? Number(value) : null;
    setSaving(true);
    try {
      const existingQuota = quotas.find((quota) => quota.id === quotaDialog);
      const quotaUrl =
        existingQuota?.userId == null
          ? '/api/admin/container-catalog/quotas'
          : `/api/admin/container-catalog/quotas/${existingQuota.userId}`;
      await client.put(quotaUrl, {
        body: {
          allowCustomImage: quotaForm.allowCustomImage,
          allowedNetworkModes: quotaForm.allowedNetworkModes.trim(),
          maxContainerCount: numberOrNull(quotaForm.maxContainerCount),
          maxCpuCores: numberOrNull(quotaForm.maxCpuCores),
          maxMemoryBytes: numberOrNull(quotaForm.maxMemoryBytes),
          maxContainerProcesses: numberOrNull(quotaForm.maxContainerProcesses),
          maxVolumeBytes: numberOrNull(quotaForm.maxVolumeBytes),
          maxContainerCpuCores: numberOrNull(quotaForm.maxContainerCpuCores),
          maxContainerMemoryBytes: numberOrNull(
            quotaForm.maxContainerMemoryBytes,
          ),
          maxVolumeBytesPerVolume: numberOrNull(
            quotaForm.maxVolumeBytesPerVolume,
          ),
        },
      });
      toast.success(t('Save successful'));
      setQuotaDialog(null);
      await refresh();
    } finally {
      setSaving(false);
    }
  };
  const toggleNode = async (node: RuntimeNode) => {
    await client.patch(
      `/api/admin/container-catalog/runtime-nodes/${node.id}/enabled`,
      { body: { isEnabled: !node.isEnabled } },
    );
    await refresh();
  };
  const performDelete = async () => {
    if (!pendingDelete) return;
    const target = pendingDelete;
    setSaving(true);
    try {
      if (target.kind === 'runtime')
        await client.delete(
          `/api/admin/container-catalog/runtime-nodes/${target.id}`,
        );
      if (target.kind === 'template')
        await client.delete(
          `/api/admin/container-catalog/templates/${target.id}`,
        );
      if (target.kind === 'image')
        await client.delete(
          `/api/admin/container-catalog/images/${encodeURIComponent(
            String(target.id),
          )}`,
        );
      toast.success(t('Deleted successful'));
      setPendingDelete(null);
      await refresh();
    } finally {
      setSaving(false);
    }
  };

  const filteredNodes = useMemo(
    () =>
      nodes.filter((x) =>
        `${x.name} ${x.aiName} ${x.endpoint || ''}`
          .toLowerCase()
          .includes(runtimeSearch.toLowerCase()),
      ),
    [nodes, runtimeSearch],
  );
  const filteredTemplates = useMemo(
    () =>
      templates.filter((x) =>
        `${x.name} ${x.image} ${x.runtimeNode?.aiName || ''}`
          .toLowerCase()
          .includes(templateSearch.toLowerCase()),
      ),
    [templates, templateSearch],
  );
  const filteredImages = useMemo(
    () =>
      images.filter((x) =>
        `${x.image} ${x.description || ''}`
          .toLowerCase()
          .includes(imageSearch.toLowerCase()),
      ),
    [images, imageSearch],
  );
  useEffect(() => setRuntimePage(1), [runtimeSearch]);
  useEffect(() => setTemplatePage(1), [templateSearch]);
  useEffect(() => setImagePage(1), [imageSearch]);
  const pagedNodes = filteredNodes.slice(
    (runtimePage - 1) * PAGE_SIZE,
    runtimePage * PAGE_SIZE,
  );
  const pagedTemplates = filteredTemplates.slice(
    (templatePage - 1) * PAGE_SIZE,
    templatePage * PAGE_SIZE,
  );
  const pagedImages = filteredImages.slice(
    (imagePage - 1) * PAGE_SIZE,
    imagePage * PAGE_SIZE,
  );
  const pagedQuotas = quotas.slice(
    (quotaPage - 1) * PAGE_SIZE,
    quotaPage * PAGE_SIZE,
  );
  const runtimeColumns: UnifiedTableColumn<RuntimeNode>[] = [
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
      cell: (x) => x.description || '—',
    },
    {
      key: 'credential',
      title: t('Credential'),
      cell: (x) => (x.hasCredential ? t('Configured') : '—'),
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
            onClick={() => openEditRuntime(x)}
          >
            <IconEdit size={16} />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            title={x.isEnabled ? t('Disable') : t('Enable')}
            onClick={() => toggleNode(x).catch(() => null)}
          >
            <IconCheck size={16} />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            title={t('Delete')}
            onClick={() =>
              setPendingDelete({ kind: 'runtime', id: x.id, label: x.name })
            }
          >
            <IconTrash size={16} />
          </Button>
        </div>
      ),
    },
  ];
  const templateColumns: UnifiedTableColumn<RuntimeTemplate>[] = [
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
            onClick={() => openEditTemplate(x)}
          >
            <IconEdit size={16} />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            title={t('Delete')}
            onClick={() =>
              setPendingDelete({ kind: 'template', id: x.id, label: x.name })
            }
          >
            <IconTrash size={16} />
          </Button>
        </div>
      ),
    },
  ];
  const imageColumns: UnifiedTableColumn<ImageEntry>[] = [
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
      cell: (x) => x.description || '—',
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
            onClick={() => openEditImage(x)}
          >
            <IconEdit size={16} />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            title={t('Delete')}
            onClick={() =>
              setPendingDelete({ kind: 'image', id: x.image, label: x.image })
            }
          >
            <IconTrash size={16} />
          </Button>
        </div>
      ),
    },
  ];
  const quotaColumns: UnifiedTableColumn<Quota>[] = [
    {
      key: 'scope',
      title: t('Scope'),
      cell: (x) => (
        <div>
          <div className="font-medium text-foreground">
            {x.userId == null
              ? t('Global default')
              : x.userName || `User #${x.userId}`}
          </div>
          <div className="text-xs text-muted-foreground">
            {t('Updated')}: {formatDateTime(x.updatedAt)}
          </div>
        </div>
      ),
    },
    {
      key: 'networks',
      title: t('Allowed networks'),
      cell: (x) => x.allowedNetworkModes || '—',
    },
    {
      key: 'containers',
      title: t('Container limit'),
      cell: (x) => limitText(x.maxContainerCount),
    },
    {
      key: 'cpu',
      title: t('CPU limit'),
      cell: (x) => limitText(x.maxCpuCores, ` ${t('cores')}`),
    },
    {
      key: 'memory',
      title: t('Memory limit'),
      cell: (x) => formatBytes(x.maxMemoryBytes),
    },
    {
      key: 'volumes',
      title: t('Volume limit'),
      cell: (x) => (
        <div className="whitespace-nowrap">
          {formatBytes(x.maxVolumeBytes)}
          <span className="text-xs text-muted-foreground">
            {' '}
            / {formatBytes(x.maxVolumeBytesPerVolume)}
          </span>
        </div>
      ),
    },
    {
      key: 'perContainer',
      title: t('Per-container limits'),
      cell: (x) => (
        <div className="whitespace-nowrap">
          {limitText(x.maxContainerCpuCores, ` ${t('cores')}`)}
          <span className="text-xs text-muted-foreground">
            {' '}
            · {formatBytes(x.maxContainerMemoryBytes)}
          </span>
        </div>
      ),
    },
    {
      key: 'processes',
      title: t('Process limit'),
      cell: (x) => limitText(x.maxContainerProcesses),
    },
    {
      key: 'custom',
      title: t('Images'),
      cell: (x) =>
        x.allowCustomImage ? (
          <Badge>{t('Custom allowed')}</Badge>
        ) : (
          <Badge variant="outline">{t('Catalog only')}</Badge>
        ),
    },
    {
      key: 'actions',
      title: t('Actions'),
      className: 'w-20',
      cell: (x) => (
        <Button
          size="icon"
          variant="ghost"
          title={t('Edit')}
          onClick={() => openEditQuota(x)}
        >
          <IconEdit size={16} />
        </Button>
      ),
    },
  ];
  const tableFilters = (
    value: string,
    setValue: (value: string) => void,
    placeholder: string,
  ) => (
    <Input
      className="w-full sm:w-72"
      value={value}
      onChange={(event) => setValue(event.target.value)}
      placeholder={placeholder}
    />
  );
  const tableActions = (label: string, onClick: () => void) => (
    <Button onClick={onClick}>
      <IconPlus
        size={16}
        className="mr-2"
        stroke="hsl(var(--primary-foreground))"
      />
      {label}
    </Button>
  );

  return (
    <main className="mx-auto max-w-[1600px] space-y-5 p-4 sm:p-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <IconDocker size={26} />
            <h1 className="text-2xl font-semibold">
              {t('Container administration')}
            </h1>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            {t(
              'Manage Docker runtimes, images, templates and resource quotas.',
            )}
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => refresh().catch(() => null)}
          disabled={loading}
        >
          <IconRefresh
            size={16}
            className={cn('mr-2', loading && 'animate-spin')}
          />
          {t('Refresh')}
        </Button>
      </div>
      <Tabs
        value={activeTab}
        onValueChange={setActiveTab}
        className="flex-col gap-4 border-none p-0 text-foreground"
      >
        <TabsList className="grid h-auto w-full grid-cols-2 gap-1 rounded-lg border bg-muted p-1 sm:grid-cols-4">
          <TabsTrigger value="runtime" className="gap-2">
            <IconDocker size={16} />
            {t('Runtime nodes')}
            <span className="text-xs text-muted-foreground">
              {nodes.length}
            </span>
          </TabsTrigger>
          <TabsTrigger value="templates" className="gap-2">
            <IconSettings size={16} />
            {t('Resource templates')}
            <span className="text-xs text-muted-foreground">
              {templates.length}
            </span>
          </TabsTrigger>
          <TabsTrigger value="images" className="gap-2">
            <IconFiles size={16} />
            {t('Image catalog')}
            <span className="text-xs text-muted-foreground">
              {images.length}
            </span>
          </TabsTrigger>
          <TabsTrigger value="quotas" className="gap-2">
            <IconWorld size={16} />
            {t('Quotas')}
            <span className="text-xs text-muted-foreground">
              {quotas.length}
            </span>
          </TabsTrigger>
        </TabsList>
        <TabsContent value="runtime">
          <UnifiedTable
            filters={tableFilters(
              runtimeSearch,
              setRuntimeSearch,
              t('Search runtime nodes'),
            )}
            actions={[
              {
                key: 'add',
                element: tableActions(t('Add runtime node'), openNewRuntime),
              },
            ]}
            columns={runtimeColumns}
            rows={pagedNodes}
            loading={loading}
            page={runtimePage}
            totalCount={filteredNodes.length}
            rowKey={(x) => x.id}
            onPageChange={setRuntimePage}
            emptyText={t('No runtime nodes found.')}
          />
        </TabsContent>
        <TabsContent value="templates">
          <UnifiedTable
            filters={tableFilters(
              templateSearch,
              setTemplateSearch,
              t('Search templates'),
            )}
            actions={[
              {
                key: 'add',
                element: tableActions(t('Add template'), openNewTemplate),
              },
            ]}
            columns={templateColumns}
            rows={pagedTemplates}
            loading={loading}
            page={templatePage}
            totalCount={filteredTemplates.length}
            rowKey={(x) => x.id}
            onPageChange={setTemplatePage}
            emptyText={t('No resource templates found.')}
          />
        </TabsContent>
        <TabsContent value="images">
          <UnifiedTable
            filters={tableFilters(
              imageSearch,
              setImageSearch,
              t('Search images'),
            )}
            actions={[
              {
                key: 'add',
                element: tableActions(t('Add image'), openNewImage),
              },
            ]}
            columns={imageColumns}
            rows={pagedImages}
            loading={loading}
            page={imagePage}
            totalCount={filteredImages.length}
            rowKey={(x) => x.image}
            onPageChange={setImagePage}
            emptyText={t('No images found.')}
          />
        </TabsContent>
        <TabsContent value="quotas">
          <UnifiedTable
            filters={
              <span className="text-sm text-muted-foreground">
                {t(
                  'Quotas control per-user container resources and image access.',
                )}
              </span>
            }
            actions={[
              {
                key: 'edit',
                element: tableActions(
                  quotas.find((x) => x.userId == null)
                    ? t('Edit global quota')
                    : t('Configure global quota'),
                  () => {
                    const globalQuota = quotas.find((x) => x.userId == null);
                    if (globalQuota) openEditQuota(globalQuota);
                    else openNewQuota();
                  },
                ),
              },
            ]}
            columns={quotaColumns}
            rows={pagedQuotas}
            loading={loading}
            page={quotaPage}
            totalCount={quotas.length}
            rowKey={(x) => x.id}
            onPageChange={setQuotaPage}
            emptyText={t('No quota policies found.')}
          />
        </TabsContent>
      </Tabs>
      <Dialog
        open={runtimeDialog !== null}
        onOpenChange={(open) => !open && setRuntimeDialog(null)}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {runtimeDialog === 'new'
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
                value={runtimeForm.name}
                onChange={(e) =>
                  setRuntimeForm({ ...runtimeForm, name: e.target.value })
                }
              />
            </Label>
            <Label>
              {t('AI name')}
              <Input
                value={runtimeForm.aiName}
                onChange={(e) =>
                  setRuntimeForm({ ...runtimeForm, aiName: e.target.value })
                }
              />
            </Label>
            <Label>
              {t('Backend')}
              <select
                className="h-10 w-full rounded-md border bg-background px-3"
                value={runtimeForm.backendType}
                onChange={(e) =>
                  setRuntimeForm({
                    ...runtimeForm,
                    backendType: Number(e.target.value),
                  })
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
                value={runtimeForm.endpoint}
                placeholder="npipe://./pipe/docker_engine"
                onChange={(e) =>
                  setRuntimeForm({ ...runtimeForm, endpoint: e.target.value })
                }
              />
              <span className="text-xs text-muted-foreground">
                {t('Leave blank to use the host operating system default.')}
              </span>
            </Label>
            <Label className="sm:col-span-2">
              {t('Description')}
              <textarea
                className="min-h-20 w-full rounded-md border bg-background px-3 py-2 text-sm"
                value={runtimeForm.description}
                onChange={(e) =>
                  setRuntimeForm({
                    ...runtimeForm,
                    description: e.target.value,
                  })
                }
              />
            </Label>
            <Label className="sm:col-span-2">
              {t('Credential')}
              <Input
                type="password"
                value={runtimeForm.credential}
                onChange={(e) =>
                  setRuntimeForm({ ...runtimeForm, credential: e.target.value })
                }
              />
              <span className="text-xs text-muted-foreground">
                {t('Leave blank to keep the current credential.')}
              </span>
            </Label>
            <label className="flex items-center gap-2 text-sm sm:col-span-2">
              <input
                type="checkbox"
                checked={runtimeForm.isEnabled}
                onChange={(e) =>
                  setRuntimeForm({
                    ...runtimeForm,
                    isEnabled: e.target.checked,
                  })
                }
              />
              {t('Enabled')}
            </label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRuntimeDialog(null)}>
              {t('Cancel')}
            </Button>
            <Button
              disabled={saving}
              onClick={() => saveRuntime().catch(() => null)}
            >
              {t('Save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog
        open={templateDialog !== null}
        onOpenChange={(open) => !open && setTemplateDialog(null)}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {templateDialog === 'new'
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
                value={templateForm.name}
                onChange={(e) =>
                  setTemplateForm({ ...templateForm, name: e.target.value })
                }
              />
            </Label>
            <Label>
              {t('Image')}
              <Input
                value={templateForm.image}
                onChange={(e) =>
                  setTemplateForm({ ...templateForm, image: e.target.value })
                }
              />
            </Label>
            <Label>
              {t('Runtime node')}
              <select
                className="h-10 w-full rounded-md border bg-background px-3"
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
                value={templateForm.cpuCores}
                onChange={(e) =>
                  setTemplateForm({
                    ...templateForm,
                    cpuCores: Number(e.target.value),
                  })
                }
              />
            </Label>
            <Label>
              {t('Memory bytes')}
              <Input
                type="number"
                min="0"
                value={templateForm.memoryBytes}
                onChange={(e) =>
                  setTemplateForm({
                    ...templateForm,
                    memoryBytes: Number(e.target.value),
                  })
                }
              />
              <span className="text-xs text-muted-foreground">
                {formatBytes(templateForm.memoryBytes)}
              </span>
            </Label>
            <Label>
              {t('Max processes')}
              <Input
                type="number"
                min="0"
                value={templateForm.maxProcesses}
                onChange={(e) =>
                  setTemplateForm({
                    ...templateForm,
                    maxProcesses: Number(e.target.value),
                  })
                }
              />
            </Label>
            <Label>
              {t('Network')}
              <Input
                value={templateForm.backendNetworkName}
                placeholder="bridge"
                onChange={(e) =>
                  setTemplateForm({
                    ...templateForm,
                    backendNetworkName: e.target.value,
                  })
                }
              />
            </Label>
            <Label>
              {t('Default volume bytes')}
              <Input
                type="number"
                min="0"
                value={templateForm.defaultVolumeBytes ?? ''}
                onChange={(e) =>
                  setTemplateForm({
                    ...templateForm,
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
                value={templateForm.visibility}
                onChange={(e) =>
                  setTemplateForm({
                    ...templateForm,
                    visibility: Number(e.target.value),
                  })
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
            <Button variant="outline" onClick={() => setTemplateDialog(null)}>
              {t('Cancel')}
            </Button>
            <Button
              disabled={saving}
              onClick={() => saveTemplate().catch(() => null)}
            >
              {t('Save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog
        open={imageDialog !== null}
        onOpenChange={(open) => !open && setImageDialog(null)}
      >
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {imageDialog === 'new' ? t('Add image') : t('Edit image')}
            </DialogTitle>
            <DialogDescription>
              {t(
                'Images must be enabled in the catalog before templates can use them.',
              )}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <Label>
              {t('Image')}
              <Input
                disabled={imageDialog !== 'new'}
                value={imageForm.image}
                placeholder="registry.example.com/image:tag"
                onChange={(e) =>
                  setImageForm({ ...imageForm, image: e.target.value })
                }
              />
            </Label>
            <Label>
              {t('Description')}
              <textarea
                className="min-h-24 w-full rounded-md border bg-background px-3 py-2 text-sm"
                value={imageForm.description}
                onChange={(e) =>
                  setImageForm({ ...imageForm, description: e.target.value })
                }
              />
            </Label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={imageForm.isEnabled}
                onChange={(e) =>
                  setImageForm({ ...imageForm, isEnabled: e.target.checked })
                }
              />
              {t('Enabled')}
            </label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setImageDialog(null)}>
              {t('Cancel')}
            </Button>
            <Button
              disabled={saving}
              onClick={() => saveImage().catch(() => null)}
            >
              {t('Save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog
        open={quotaDialog !== null}
        onOpenChange={(open) => !open && setQuotaDialog(null)}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>{t('Edit quota policy')}</DialogTitle>
            <DialogDescription>
              {t('Leave a limit blank for unlimited.')}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-3">
            <Label className="sm:col-span-3">
              {t('Allowed network modes')}
              <Input
                value={quotaForm.allowedNetworkModes}
                placeholder="none,bridge"
                onChange={(e) =>
                  setQuotaForm({
                    ...quotaForm,
                    allowedNetworkModes: e.target.value,
                  })
                }
              />
            </Label>
            {(
              [
                ['maxContainerCount', 'Max containers'],
                ['maxCpuCores', 'Max CPU cores'],
                ['maxMemoryBytes', 'Max memory bytes'],
                ['maxContainerProcesses', 'Max processes'],
                ['maxVolumeBytes', 'Max volume bytes'],
                ['maxContainerCpuCores', 'Max CPU per container'],
                ['maxContainerMemoryBytes', 'Max memory per container'],
                ['maxVolumeBytesPerVolume', 'Max volume bytes per volume'],
              ] as const
            ).map(([key, label]) => (
              <Label key={key}>
                {t(label)}
                <Input
                  type="number"
                  min="0"
                  value={quotaForm[key]}
                  onChange={(e) =>
                    setQuotaForm({ ...quotaForm, [key]: e.target.value })
                  }
                />
              </Label>
            ))}
            <label className="flex items-center gap-2 text-sm sm:col-span-3">
              <input
                type="checkbox"
                checked={quotaForm.allowCustomImage}
                onChange={(e) =>
                  setQuotaForm({
                    ...quotaForm,
                    allowCustomImage: e.target.checked,
                  })
                }
              />
              {t('Allow custom images')}
            </label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setQuotaDialog(null)}>
              {t('Cancel')}
            </Button>
            <Button
              disabled={saving}
              onClick={() => saveQuota().catch(() => null)}
            >
              {t('Save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <AlertDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('Confirm deletion')}</AlertDialogTitle>
            <AlertDialogDescription>
              {t('Are you sure you want to delete {{name}}?', {
                name: pendingDelete?.label || '',
              })}
              <br />
              <span className="text-xs">
                {t('This action cannot be undone.')}
              </span>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('Cancel')}</AlertDialogCancel>
            <AlertDialogAction
              disabled={saving}
              onClick={(event) => {
                event.preventDefault();
                performDelete().catch(() => null);
              }}
            >
              {t('Delete')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <div className="flex items-start gap-2 rounded-lg border border-dashed p-3 text-xs text-muted-foreground">
        <IconInfo size={16} className="mt-0.5 shrink-0" />
        <span>
          {t(
            'Runtime nodes point to Docker daemons. Templates control what users and AI can create; quotas enforce per-user resource limits.',
          )}
        </span>
      </div>
    </main>
  );
}
