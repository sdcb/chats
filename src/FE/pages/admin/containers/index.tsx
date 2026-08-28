import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import { createFetchClient } from '@/hooks/createFetchClient';
import useTranslation from '@/hooks/useTranslation';

import {
  IconDocker,
  IconFiles,
  IconRefresh,
  IconSettings,
  IconWorld,
} from '@/components/Icons';
import ImagesTab from '@/components/admin/containers/ImagesTab';
import QuotasTab from '@/components/admin/containers/QuotasTab';
import RuntimeNodesTab from '@/components/admin/containers/RuntimeNodesTab';
import TemplatesTab from '@/components/admin/containers/TemplatesTab';
import {
  ContainerTab,
  DeleteTarget,
  ImageEntry,
  ImageForm,
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
  isContainerTab,
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
  const router = useRouter();
  const client = useMemo(() => createFetchClient(), []);
  const [nodes, setNodes] = useState<RuntimeNode[]>([]);
  const [templates, setTemplates] = useState<RuntimeTemplate[]>([]);
  const [images, setImages] = useState<ImageEntry[]>([]);
  const [quotas, setQuotas] = useState<Quota[]>([]);
  const [loadingTabs, setLoadingTabs] = useState<Record<ContainerTab, boolean>>(
    {
      runtime: false,
      templates: false,
      images: false,
      quotas: false,
    },
  );
  const [loadedTabs, setLoadedTabs] = useState<Record<ContainerTab, boolean>>({
    runtime: false,
    templates: false,
    images: false,
    quotas: false,
  });
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

  const loadTab = useCallback(
    async (tab: ContainerTab, force = false) => {
      if (!force && loadedTabs[tab]) return;
      setLoadingTabs((current) => ({ ...current, [tab]: true }));
      try {
        switch (tab) {
          case 'runtime': {
            const nextNodes = await client.get<RuntimeNode[]>(
              '/api/admin/container-catalog/runtime-nodes',
            );
            setNodes(nextNodes);
            setTemplateForm((current) =>
              current.runtimeNodeId || !nextNodes.length
                ? current
                : { ...current, runtimeNodeId: nextNodes[0].id },
            );
            break;
          }
          case 'templates':
            setTemplates(
              await client.get<RuntimeTemplate[]>(
                '/api/admin/container-catalog/templates',
              ),
            );
            break;
          case 'images':
            setImages(
              await client.get<ImageEntry[]>(
                '/api/admin/container-catalog/images',
              ),
            );
            break;
          case 'quotas':
            setQuotas(
              await client.get<Quota[]>('/api/admin/container-catalog/quotas'),
            );
            break;
        }
        setLoadedTabs((current) => ({ ...current, [tab]: true }));
      } finally {
        setLoadingTabs((current) => ({ ...current, [tab]: false }));
      }
    },
    [client, loadedTabs],
  );

  const tabQueryValue = Array.isArray(router.query.tab)
    ? router.query.tab[0]
    : router.query.tab;
  const activeTab: ContainerTab = isContainerTab(tabQueryValue)
    ? tabQueryValue
    : 'runtime';

  useEffect(() => {
    if (!router.isReady) return;
    if (!isContainerTab(tabQueryValue)) {
      void router.replace(
        {
          pathname: router.pathname,
          query: { ...router.query, tab: 'runtime' },
        },
        undefined,
        { shallow: true },
      );
    }
    loadTab(activeTab).catch(() => null);
  }, [activeTab, loadTab, router.isReady, router.pathname, tabQueryValue]);

  const handleTabChange = (value: string) => {
    if (!router.isReady || !isContainerTab(value)) return;
    void router.push(
      {
        pathname: router.pathname,
        query: { ...router.query, tab: value },
      },
      undefined,
      { shallow: true },
    );
  };

  const refreshActiveTab = () => loadTab(activeTab, true);

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
      await loadTab('runtime', true);
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
      await loadTab('templates', true);
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
      await loadTab('images', true);
      if (loadedTabs.templates) await loadTab('templates', true);
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
      await loadTab('quotas', true);
    } finally {
      setSaving(false);
    }
  };

  const toggleNode = async (node: RuntimeNode) => {
    await client.patch(
      `/api/admin/container-catalog/runtime-nodes/${node.id}/enabled`,
      { body: { isEnabled: !node.isEnabled } },
    );
    await loadTab('runtime', true);
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
      const tab: ContainerTab =
        target.kind === 'runtime'
          ? 'runtime'
          : target.kind === 'template'
          ? 'templates'
          : 'images';
      await loadTab(tab, true);
      if (target.kind === 'image' && loadedTabs.templates)
        await loadTab('templates', true);
    } finally {
      setSaving(false);
    }
  };

  return (
    <main className="mx-auto max-w-[1600px] space-y-5 p-4 sm:p-6">
      <div className="relative flex items-center justify-center">
        <div className="flex items-center gap-2">
          <IconDocker size={26} />
          <h1 className="text-2xl font-semibold">
            {t('Container administration')}
          </h1>
        </div>
        <Button
          variant="outline"
          size="sm"
          className="absolute right-0 top-1/2 -translate-y-1/2"
          aria-label={t('Refresh')}
          onClick={() => refreshActiveTab().catch(() => null)}
          disabled={loadingTabs[activeTab]}
        >
          <IconRefresh
            size={16}
            className={cn('sm:mr-2', loadingTabs[activeTab] && 'animate-spin')}
          />
          <span className="hidden sm:inline">{t('Refresh')}</span>
        </Button>
      </div>

      <Tabs
        value={activeTab}
        onValueChange={handleTabChange}
        className="flex-col gap-4 border-none p-0 text-foreground"
      >
        <div className="flex w-full justify-center overflow-x-auto">
          <TabsList className="inline-flex h-auto flex-row items-center justify-center gap-0 rounded-full border border-border/60 bg-muted p-1 shadow-sm">
            <TabsTrigger
              value="runtime"
              className="flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-colors hover:text-foreground/90 focus-visible:ring-0 focus-visible:ring-offset-0 data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              <IconDocker size={16} />
              <span>{t('Runtime nodes')}</span>
              <span className="text-xs text-muted-foreground">
                {nodes.length}
              </span>
            </TabsTrigger>
            <TabsTrigger
              value="templates"
              className="flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-colors hover:text-foreground/90 focus-visible:ring-0 focus-visible:ring-offset-0 data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              <IconSettings size={16} />
              <span>{t('Resource templates')}</span>
              <span className="text-xs text-muted-foreground">
                {templates.length}
              </span>
            </TabsTrigger>
            <TabsTrigger
              value="images"
              className="flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-colors hover:text-foreground/90 focus-visible:ring-0 focus-visible:ring-offset-0 data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              <IconFiles size={16} />
              <span>{t('Image catalog')}</span>
              <span className="text-xs text-muted-foreground">
                {images.length}
              </span>
            </TabsTrigger>
            <TabsTrigger
              value="quotas"
              className="flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-colors hover:text-foreground/90 focus-visible:ring-0 focus-visible:ring-offset-0 data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              <IconWorld size={16} />
              <span>{t('Quotas')}</span>
              <span className="text-xs text-muted-foreground">
                {quotas.length}
              </span>
            </TabsTrigger>
          </TabsList>
        </div>

        <TabsContent value="runtime">
          <RuntimeNodesTab
            nodes={nodes}
            templates={templates}
            loading={loadingTabs.runtime}
            saving={saving}
            dialog={runtimeDialog}
            form={runtimeForm}
            setForm={setRuntimeForm}
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
            templates={templates}
            loading={loadingTabs.templates}
            saving={saving}
            dialog={templateDialog}
            form={templateForm}
            setForm={setTemplateForm}
            onDialogChange={setTemplateDialog}
            onNew={openNewTemplate}
            onEdit={openEditTemplate}
            onSave={saveTemplate}
            onDeleteRequest={setPendingDelete}
          />
        </TabsContent>
        <TabsContent value="images">
          <ImagesTab
            images={images}
            loading={loadingTabs.images}
            saving={saving}
            dialog={imageDialog}
            form={imageForm}
            setForm={setImageForm}
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
            loading={loadingTabs.quotas}
            saving={saving}
            dialog={quotaDialog}
            form={quotaForm}
            setForm={setQuotaForm}
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
    </main>
  );
}
