import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import { createFetchClient } from '@/hooks/createFetchClient';
import useTranslation from '@/hooks/useTranslation';

import {
  IconDocker,
  IconFiles,
  IconInfo,
  IconRefresh,
  IconSettings,
  IconWorld,
} from '@/components/Icons';
import ImagesTab from '@/components/admin/containers/ImagesTab';
import QuotasTab from '@/components/admin/containers/QuotasTab';
import RuntimeNodesTab from '@/components/admin/containers/RuntimeNodesTab';
import TemplatesTab from '@/components/admin/containers/TemplatesTab';
import {
  DeleteTarget,
  ImageEntry,
  ImageForm,
  PAGE_SIZE,
  Quota,
  QuotaForm,
  RuntimeForm,
  RuntimeNode,
  RuntimeTemplate,
  TemplateForm,
  emptyImage,
  emptyQuota,
  emptyRuntime,
  emptyTemplate,
} from '@/components/admin/containers/types';
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
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import { cn } from '@/lib/utils';

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
  const [imageDialog, setImageDialog] = useState<number | 'new' | null>(null);
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
    setImageDialog(item.id);
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
      if (runtimeDialog === 'new') {
        await client.post('/api/admin/container-catalog/runtime-nodes', {
          body,
        });
      } else if (runtimeDialog != null) {
        await client.put(
          `/api/admin/container-catalog/runtime-nodes/${runtimeDialog}`,
          { body },
        );
      }
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
      if (templateDialog === 'new') {
        await client.post('/api/admin/container-catalog/templates', { body });
      } else if (templateDialog != null) {
        await client.put(
          `/api/admin/container-catalog/templates/${templateDialog}`,
          { body },
        );
      }
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
      const body = {
        image: imageForm.image.trim(),
        description: imageForm.description.trim() || null,
        isEnabled: imageForm.isEnabled,
      };
      if (imageDialog === 'new') {
        await client.post('/api/admin/container-catalog/images', { body });
      } else if (imageDialog != null) {
        await client.put(`/api/admin/container-catalog/images/${imageDialog}`, {
          body,
        });
      }
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
      if (target.kind === 'runtime') {
        await client.delete(
          `/api/admin/container-catalog/runtime-nodes/${target.id}`,
        );
      } else if (target.kind === 'template') {
        await client.delete(
          `/api/admin/container-catalog/templates/${target.id}`,
        );
      } else {
        await client.delete(`/api/admin/container-catalog/images/${target.id}`);
      }
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
          <RuntimeNodesTab
            nodes={nodes}
            templates={templates}
            rows={pagedNodes}
            loading={loading}
            saving={saving}
            search={runtimeSearch}
            page={runtimePage}
            totalCount={filteredNodes.length}
            dialog={runtimeDialog}
            form={runtimeForm}
            setForm={setRuntimeForm}
            onSearchChange={setRuntimeSearch}
            onPageChange={setRuntimePage}
            onDialogChange={setRuntimeDialog}
            onNew={openNewRuntime}
            onEdit={openEditRuntime}
            onToggle={toggleNode}
            onSave={saveRuntime}
            onDeleteRequest={setPendingDelete}
          />
        </TabsContent>
        <TabsContent value="templates">
          <TemplatesTab
            nodes={nodes}
            rows={pagedTemplates}
            loading={loading}
            saving={saving}
            search={templateSearch}
            page={templatePage}
            totalCount={filteredTemplates.length}
            dialog={templateDialog}
            form={templateForm}
            setForm={setTemplateForm}
            onSearchChange={setTemplateSearch}
            onPageChange={setTemplatePage}
            onDialogChange={setTemplateDialog}
            onNew={openNewTemplate}
            onEdit={openEditTemplate}
            onSave={saveTemplate}
            onDeleteRequest={setPendingDelete}
          />
        </TabsContent>
        <TabsContent value="images">
          <ImagesTab
            rows={pagedImages}
            loading={loading}
            saving={saving}
            search={imageSearch}
            page={imagePage}
            totalCount={filteredImages.length}
            dialog={imageDialog}
            form={imageForm}
            setForm={setImageForm}
            onSearchChange={setImageSearch}
            onPageChange={setImagePage}
            onDialogChange={setImageDialog}
            onNew={openNewImage}
            onEdit={openEditImage}
            onSave={saveImage}
            onDeleteRequest={setPendingDelete}
          />
        </TabsContent>
        <TabsContent value="quotas">
          <QuotasTab
            quotas={quotas}
            rows={pagedQuotas}
            loading={loading}
            saving={saving}
            page={quotaPage}
            totalCount={quotas.length}
            dialog={quotaDialog}
            form={quotaForm}
            setForm={setQuotaForm}
            onPageChange={setQuotaPage}
            onDialogChange={setQuotaDialog}
            onEdit={openEditQuota}
            onNew={openNewQuota}
            onSave={saveQuota}
          />
        </TabsContent>
      </Tabs>

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
