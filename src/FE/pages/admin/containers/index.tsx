import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import { createFetchClient } from '@/hooks/createFetchClient';
import useTranslation from '@/hooks/useTranslation';

import {
  IconDesktop,
  IconDocker,
  IconFiles,
  IconSettings,
  IconWorld,
} from '@/components/Icons';
import ContainerResourcesTab from '@/components/admin/containers/ContainerResourcesTab';
import ImagesTab from '@/components/admin/containers/ImagesTab';
import QuotasTab from '@/components/admin/containers/QuotasTab';
import RuntimeNodesTab from '@/components/admin/containers/RuntimeNodesTab';
import TemplatesTab from '@/components/admin/containers/TemplatesTab';
import {
  ContainerTab,
  DeleteTarget,
  ImageEntry,
  ImageFilters,
  ImageForm,
  Quota,
  QuotaFilters,
  QuotaForm,
  RuntimeForm,
  RuntimeNode,
  RuntimeNodeFilters,
  RuntimeTemplate,
  TemplateFilters,
  TemplateForm,
  emptyImage,
  emptyQuota,
  emptyRuntime,
  emptyTemplate,
  isContainerTab,
} from '@/components/admin/containers/types';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import {
  getAdminImages,
  getAdminQuotas,
  getAdminRuntimeNodes,
  getAdminTemplates,
} from '@/apis/adminContainersApi';

type CatalogFilters = {
  runtime: RuntimeNodeFilters;
  templates: TemplateFilters;
  images: ImageFilters;
  quotas: QuotaFilters;
};

type RouterQuery = Record<string, string | string[] | undefined>;

const readQueryValue = (query: RouterQuery, key: string) => {
  const value = query[key];
  return Array.isArray(value) ? value[0] || '' : value || '';
};

const readEnumQueryValue = <T extends string>(
  query: RouterQuery,
  key: string,
  values: readonly T[],
): T | '' => {
  const value = readQueryValue(query, key);
  return values.includes(value as T) ? (value as T) : '';
};

const readPositiveIntegerQueryValue = (query: RouterQuery, key: string) => {
  const value = readQueryValue(query, key);
  return /^\d+$/.test(value) ? value : '';
};

const buildCatalogFilterQuery = <T extends keyof CatalogFilters>(
  tab: T,
  filters: CatalogFilters[T],
) => {
  const query: Record<string, string> = { tab };
  Object.entries(filters as Record<string, string>).forEach(([key, value]) => {
    if (value) query[key] = value;
  });
  return query;
};

export default function AdminContainersPage() {
  const { t } = useTranslation();
  const router = useRouter();
  const client = useMemo(() => createFetchClient(), []);
  const [nodes, setNodes] = useState<RuntimeNode[]>([]);
  const [templates, setTemplates] = useState<RuntimeTemplate[]>([]);
  const [images, setImages] = useState<ImageEntry[]>([]);
  const [quotas, setQuotas] = useState<Quota[]>([]);
  const [runtimeFilters, setRuntimeFilters] = useState<RuntimeNodeFilters>({
    query: '',
    backendType: '',
    enabled: '',
  });
  const [templateFilters, setTemplateFilters] = useState<TemplateFilters>({
    query: '',
    runtimeNodeId: '',
    visibility: '',
  });
  const [imageFilters, setImageFilters] = useState<ImageFilters>({
    query: '',
    enabled: '',
  });
  const [quotaFilters, setQuotaFilters] = useState<QuotaFilters>({
    query: '',
    allowCustomImage: '',
    scope: '',
  });
  const [loadingTabs, setLoadingTabs] = useState<Record<ContainerTab, boolean>>(
    {
      resources: false,
      runtime: false,
      templates: false,
      images: false,
      quotas: false,
    },
  );
  const [loadedTabs, setLoadedTabs] = useState<Record<ContainerTab, boolean>>({
    resources: false,
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
  const [quotaDialog, setQuotaDialog] = useState<number | 'new' | null>(null);
  const [runtimeForm, setRuntimeForm] = useState<RuntimeForm>(emptyRuntime);
  const [templateForm, setTemplateForm] = useState<TemplateForm>(emptyTemplate);
  const [imageForm, setImageForm] = useState<ImageForm>(emptyImage);
  const [quotaForm, setQuotaForm] = useState<QuotaForm>(emptyQuota);
  const [saving, setSaving] = useState(false);
  const requestIdsRef = useRef<Record<ContainerTab, number>>({
    resources: 0,
    runtime: 0,
    templates: 0,
    images: 0,
    quotas: 0,
  });

  const filtersRef = useMemo(
    (): CatalogFilters => ({
      runtime: runtimeFilters,
      templates: templateFilters,
      images: imageFilters,
      quotas: quotaFilters,
    }),
    [imageFilters, quotaFilters, runtimeFilters, templateFilters],
  );

  const loadTab = useCallback(
    async (
      tab: ContainerTab,
      force = false,
      overrideFilters?: Partial<CatalogFilters>,
    ) => {
      if (!force && loadedTabs[tab]) return;
      const requestId = ++requestIdsRef.current[tab];
      setLoadingTabs((current) => ({ ...current, [tab]: true }));
      try {
        switch (tab) {
          case 'resources':
            break;
          case 'runtime': {
            const nextNodes = await getAdminRuntimeNodes(
              overrideFilters?.runtime ?? filtersRef.runtime,
            );
            if (requestId === requestIdsRef.current[tab]) {
              setNodes(nextNodes);
              setTemplateForm((current) =>
                current.runtimeNodeId || !nextNodes.length
                  ? current
                  : { ...current, runtimeNodeId: nextNodes[0].id },
              );
            }
            break;
          }
          case 'templates':
            {
              const nextTemplates = await getAdminTemplates(
                overrideFilters?.templates ?? filtersRef.templates,
              );
              if (requestId === requestIdsRef.current[tab])
                setTemplates(nextTemplates);
            }
            break;
          case 'images':
            {
              const nextImages = await getAdminImages(
                overrideFilters?.images ?? filtersRef.images,
              );
              if (requestId === requestIdsRef.current[tab])
                setImages(nextImages);
            }
            break;
          case 'quotas':
            {
              const nextQuotas = await getAdminQuotas(
                overrideFilters?.quotas ?? filtersRef.quotas,
              );
              if (requestId === requestIdsRef.current[tab])
                setQuotas(nextQuotas);
            }
            break;
        }
        if (requestId === requestIdsRef.current[tab])
          setLoadedTabs((current) => ({ ...current, [tab]: true }));
      } finally {
        if (requestId === requestIdsRef.current[tab])
          setLoadingTabs((current) => ({ ...current, [tab]: false }));
      }
    },
    [client, filtersRef, loadedTabs],
  );

  const updateCatalogFilters = useCallback(
    <T extends keyof CatalogFilters>(tab: T, filters: CatalogFilters[T]) => {
      if (tab === 'runtime') setRuntimeFilters(filters as RuntimeNodeFilters);
      else if (tab === 'templates')
        setTemplateFilters(filters as TemplateFilters);
      else if (tab === 'images') setImageFilters(filters as ImageFilters);
      else if (tab === 'quotas') setQuotaFilters(filters as QuotaFilters);

      void router.replace(
        {
          pathname: router.pathname,
          query: buildCatalogFilterQuery(tab, filters),
        },
        undefined,
        { shallow: true },
      );
    },
    [router],
  );

  const tabQueryValue = Array.isArray(router.query.tab)
    ? router.query.tab[0]
    : router.query.tab;
  const activeTab: ContainerTab = isContainerTab(tabQueryValue)
    ? tabQueryValue
    : 'resources';

  const catalogUrlKey = useMemo(() => {
    const keys = ['tab'];
    if (activeTab === 'runtime') keys.push('query', 'backendType', 'enabled');
    else if (activeTab === 'templates')
      keys.push('query', 'runtimeNodeId', 'visibility');
    else if (activeTab === 'images') keys.push('query', 'enabled');
    else if (activeTab === 'quotas')
      keys.push('query', 'allowCustomImage', 'scope');
    return JSON.stringify(
      keys.map((key) => [key, readQueryValue(router.query, key)]),
    );
  }, [activeTab, router.query]);
  const appliedCatalogUrlKeyRef = useRef<string | null>(null);

  useEffect(() => {
    if (!router.isReady) return;

    // The first tab is the default view, so keep its URL clean. Only the
    // non-default tabs need an explicit `?tab=` query parameter.
    if (
      tabQueryValue === 'resources' ||
      (tabQueryValue !== undefined && !isContainerTab(tabQueryValue))
    ) {
      const nextQuery = { ...router.query };
      delete nextQuery.tab;
      void router.replace(
        {
          pathname: router.pathname,
          query: nextQuery,
        },
        undefined,
        { shallow: true },
      );
    }

    if (appliedCatalogUrlKeyRef.current === catalogUrlKey) return;
    appliedCatalogUrlKeyRef.current = catalogUrlKey;

    if (activeTab === 'runtime') {
      const nextFilters: RuntimeNodeFilters = {
        query: readQueryValue(router.query, 'query'),
        backendType: readEnumQueryValue(router.query, 'backendType', [
          '1',
          '2',
          '3',
          '4',
        ]),
        enabled: readEnumQueryValue(router.query, 'enabled', ['true', 'false']),
      };
      setRuntimeFilters(nextFilters);
      void loadTab('runtime', true, { runtime: nextFilters }).catch(() => null);
    } else if (activeTab === 'templates') {
      const nextFilters: TemplateFilters = {
        query: readQueryValue(router.query, 'query'),
        runtimeNodeId: readPositiveIntegerQueryValue(
          router.query,
          'runtimeNodeId',
        ),
        visibility: readEnumQueryValue(router.query, 'visibility', [
          '0',
          '1',
          '2',
          '3',
        ]),
      };
      setTemplateFilters(nextFilters);
      void loadTab('templates', true, { templates: nextFilters }).catch(
        () => null,
      );
    } else if (activeTab === 'images') {
      const nextFilters: ImageFilters = {
        query: readQueryValue(router.query, 'query'),
        enabled: readEnumQueryValue(router.query, 'enabled', ['true', 'false']),
      };
      setImageFilters(nextFilters);
      void loadTab('images', true, { images: nextFilters }).catch(() => null);
    } else if (activeTab === 'quotas') {
      const nextFilters: QuotaFilters = {
        query: readQueryValue(router.query, 'query'),
        allowCustomImage: readEnumQueryValue(router.query, 'allowCustomImage', [
          'true',
          'false',
        ]),
        scope: readEnumQueryValue(router.query, 'scope', ['default', 'user']),
      };
      setQuotaFilters(nextFilters);
      void loadTab('quotas', true, { quotas: nextFilters }).catch(() => null);
    }
  }, [
    activeTab,
    catalogUrlKey,
    loadTab,
    router.isReady,
    router.pathname,
    router.query,
    tabQueryValue,
  ]);

  const handleTabChange = (value: string) => {
    if (!router.isReady || !isContainerTab(value)) return;

    // Catalog tabs do not share the resource list's paging/filter/column
    // state. Carry the selected catalog tab's filters in its URL.
    let nextQuery: Record<string, string> = {};
    if (value === 'runtime')
      nextQuery = buildCatalogFilterQuery('runtime', runtimeFilters);
    else if (value === 'templates')
      nextQuery = buildCatalogFilterQuery('templates', templateFilters);
    else if (value === 'images')
      nextQuery = buildCatalogFilterQuery('images', imageFilters);
    else if (value === 'quotas')
      nextQuery = buildCatalogFilterQuery('quotas', quotaFilters);

    void router.push(
      {
        pathname: router.pathname,
        query: nextQuery,
      },
      undefined,
      { shallow: true },
    );
  };

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
      userId: quota.userId == null ? '' : String(quota.userId),
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
    setQuotaDialog('new');
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
    const existingQuota =
      typeof quotaDialog === 'number'
        ? quotas.find((quota) => quota.id === quotaDialog)
        : undefined;
    const userId =
      quotaDialog === 'new'
        ? Number(quotaForm.userId)
        : existingQuota?.userId ?? null;
    if (
      quotaDialog === 'new' &&
      (!Number.isInteger(userId) || userId == null || userId <= 0)
    )
      return;

    setSaving(true);
    try {
      const quotaUrl =
        userId == null
          ? '/api/admin/container-catalog/quotas'
          : `/api/admin/container-catalog/quotas/${userId}`;
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

  const performDelete = async (target: DeleteTarget) => {
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
      } else if (target.kind === 'image') {
        await client.delete(`/api/admin/container-catalog/images/${target.id}`);
      } else {
        await client.delete(`/api/admin/container-catalog/quotas/${target.id}`);
      }
      toast.success(t('Deleted successful'));
      const tab: ContainerTab =
        target.kind === 'runtime'
          ? 'runtime'
          : target.kind === 'template'
          ? 'templates'
          : target.kind === 'image'
          ? 'images'
          : 'quotas';
      await loadTab(tab, true);
      if (target.kind === 'image' && loadedTabs.templates)
        await loadTab('templates', true);
    } finally {
      setSaving(false);
    }
  };

  return (
    <main className="w-full space-y-5 p-4 sm:p-6">
      <Tabs
        value={activeTab}
        onValueChange={handleTabChange}
        className="flex-col gap-4 border-none p-0 text-foreground"
      >
        <div className="flex w-full justify-center overflow-x-auto">
          <TabsList className="inline-flex h-auto flex-row items-center justify-center gap-0 rounded-full border border-border/60 bg-muted p-1 shadow-sm">
            <TabsTrigger
              value="resources"
              className="flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-colors hover:text-foreground/90 focus-visible:ring-0 focus-visible:ring-offset-0 data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              <IconDesktop size={16} />
              <span>{t('Container resources')}</span>
            </TabsTrigger>
            <TabsTrigger
              value="runtime"
              className="flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-colors hover:text-foreground/90 focus-visible:ring-0 focus-visible:ring-offset-0 data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              <IconDocker size={16} />
              <span>{t('Runtime nodes')}</span>
            </TabsTrigger>
            <TabsTrigger
              value="templates"
              className="flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-colors hover:text-foreground/90 focus-visible:ring-0 focus-visible:ring-offset-0 data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              <IconSettings size={16} />
              <span>{t('Resource templates')}</span>
            </TabsTrigger>
            <TabsTrigger
              value="images"
              className="flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-colors hover:text-foreground/90 focus-visible:ring-0 focus-visible:ring-offset-0 data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              <IconFiles size={16} />
              <span>{t('Image catalog')}</span>
            </TabsTrigger>
            <TabsTrigger
              value="quotas"
              className="flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-colors hover:text-foreground/90 focus-visible:ring-0 focus-visible:ring-offset-0 data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              <IconWorld size={16} />
              <span>{t('Quotas')}</span>
            </TabsTrigger>
          </TabsList>
        </div>

        <TabsContent value="resources">
          <ContainerResourcesTab />
        </TabsContent>
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
            onDelete={performDelete}
            filters={runtimeFilters}
            onFiltersChange={(next) => updateCatalogFilters('runtime', next)}
            onRefresh={() => loadTab('runtime', true)}
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
            onDelete={performDelete}
            filters={templateFilters}
            onFiltersChange={(next) => updateCatalogFilters('templates', next)}
            onRefresh={() => loadTab('templates', true)}
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
            onDelete={performDelete}
            filters={imageFilters}
            onFiltersChange={(next) => updateCatalogFilters('images', next)}
            onRefresh={() => loadTab('images', true)}
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
            onDelete={performDelete}
            filters={quotaFilters}
            onFiltersChange={(next) => updateCatalogFilters('quotas', next)}
            onRefresh={() => loadTab('quotas', true)}
          />
        </TabsContent>
      </Tabs>
    </main>
  );
}
