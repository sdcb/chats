import React, { DragEvent, useEffect, useMemo, useRef, useState } from 'react';
import { DndContext, DragEndEvent, DragOverlay, PointerSensor, useSensor, useSensors, closestCorners, MeasuringStrategy } from '@dnd-kit/core';
import { SortableContext, verticalListSortingStrategy, arrayMove } from '@dnd-kit/sortable';
import { restrictToVerticalAxis } from '@dnd-kit/modifiers';
import toast from 'react-hot-toast';
import { useRouter } from 'next/router';
import useTranslation from '@/hooks/useTranslation';
import { AdminModelDto, GetModelKeysResult, ModelProviderDto } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';
import { formatNumberAsMoney } from '@/utils/common';
import { Button } from '@/components/ui/button';
import IconActionButton from '@/components/common/IconActionButton';
import { LabelSwitch } from '@/components/ui/label-switch';
import { Skeleton } from '@/components/ui/skeleton';
import { IconPlus } from '@/components/Icons';
import { 
  getModelProviders, 
  getModelKeysByProvider, 
  getModelsByKey,
  deleteModelKeys, 
  deleteModels, 
  reorderModelProviders, 
  reorderModelKeys, 
  reorderModels 
} from '@/apis/adminApis';
import ModelKeysModal from '@/components/admin/ModelKeys/ModelKeysModal';
import QuickAddModelModal from '@/components/admin/ModelKeys/QuickAddModelModal';
import ModelModal from '@/components/admin/Models/ModelModal';
import ModelProvider, { ProviderGroup } from '@/components/admin/model/ModelProvider';
import { 
  ApiType,
  getDefaultConfigByApiType 
} from '@/constants/modelDefaults';

export default function ModelManager() {
  const { t } = useTranslation();
  const router = useRouter();

  // data state
  const [providers, setProviders] = useState<ModelProviderDto[]>([]);
  const [keysByProvider, setKeysByProvider] = useState<Record<number, GetModelKeysResult[]>>({});
  const [modelsByKey, setModelsByKey] = useState<Record<number, AdminModelDto[]>>({});
  const [loading, setLoading] = useState(true);
  const [loadingKeys, setLoadingKeys] = useState<Record<number, boolean>>({});
  const [loadingModels, setLoadingModels] = useState<Record<number, boolean>>({});

  // UI state
  const [expandProviders, setExpandProviders] = useState<Record<number, boolean>>({});
  const [expandKeys, setExpandKeys] = useState<Record<number, boolean>>({});
  const [showAllProviders, setShowAllProviders] = useState(false);

  // drag state
  const [currentDragProvider, setCurrentDragProvider] = useState<ProviderGroup | null>(null);
  // dnd-kit 不需要 providerRefs
  
  // ModelKey drag state
  const [currentDragKey, setCurrentDragKey] = useState<GetModelKeysResult | null>(null);
  // dnd-kit 不需要 keyRefs
  const [activeId, setActiveId] = useState<string | null>(null);

  // dnd-kit sensors
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 5,
      },
    })
  );

  // dialogs
  const [isOpenKeyModal, setIsOpenKeyModal] = useState(false);
  const [isOpenQuickAddModels, setIsOpenQuickAddModels] = useState(false);
  const [isOpenAddModel, setIsOpenAddModel] = useState(false);
  const [isOpenEditModel, setIsOpenEditModel] = useState(false);

  // defaults for dialogs
  const [currentProviderId, setCurrentProviderId] = useState<number | undefined>(undefined);
  const [currentModelKeyId, setCurrentModelKeyId] = useState<number | undefined>(undefined);
  const [selectedModel, setSelectedModel] = useState<AdminModelDto | undefined>(undefined);
  const [selectedKey, setSelectedKey] = useState<GetModelKeysResult | undefined>(undefined);
  const [addModelDefaults, setAddModelDefaults] = useState<any>(undefined);

  useEffect(() => {
    init();
  }, []);

  const init = async () => {
    setLoading(true);
    const providersData = await getModelProviders();
    setProviders(providersData);
    
    // 初始化展开状态
    const providerExpand: Record<number, boolean> = {};
    for (const p of providersData) providerExpand[p.providerId] = false;
    setExpandProviders(providerExpand);
    setExpandKeys({});
    
    setLoading(false);
  };

  // 刷新 provider 列表
  const refreshProviders = async () => {
    const providersData = await getModelProviders();
    setProviders(providersData);
  };

  // 加载指定 provider 的 keys
  const loadProviderKeys = async (providerId: number) => {
    if (keysByProvider[providerId]) return; // 已加载
    
    setLoadingKeys(prev => ({ ...prev, [providerId]: true }));
    try {
      const keys = await getModelKeysByProvider(providerId);
      setKeysByProvider(prev => ({ ...prev, [providerId]: keys }));
    } finally {
      setLoadingKeys(prev => ({ ...prev, [providerId]: false }));
    }
  };

  // 刷新指定 provider 的 keys
  const refreshProviderKeys = async (providerId: number) => {
    const keys = await getModelKeysByProvider(providerId);
    setKeysByProvider(prev => ({ ...prev, [providerId]: keys }));
  };

  // 加载指定 key 的 models
  const loadKeyModels = async (keyId: number) => {
    if (modelsByKey[keyId]) return; // 已加载
    
    setLoadingModels(prev => ({ ...prev, [keyId]: true }));
    try {
      const models = await getModelsByKey(keyId);
      setModelsByKey(prev => ({ ...prev, [keyId]: models }));
    } finally {
      setLoadingModels(prev => ({ ...prev, [keyId]: false }));
    }
  };

  // 刷新指定 key 的 models
  const refreshKeyModels = async (keyId: number) => {
    const models = await getModelsByKey(keyId);
    setModelsByKey(prev => ({ ...prev, [keyId]: models }));
  };

  const grouped: ProviderGroup[] = useMemo(() => {
    return providers.map(p => {
      const providerInfo = feModelProviders.find(fp => fp.id === p.providerId);
      return {
        providerId: p.providerId,
        providerName: providerInfo?.name || `Provider ${p.providerId}`,
        keys: keysByProvider[p.providerId] || [],
        keyCount: p.keyCount,
        modelCount: p.modelCount,
      };
    });
  }, [providers, keysByProvider]);

  const filteredProviders = useMemo(() => {
    if (showAllProviders) {
      return grouped;
    }
    // 过滤出有 keys 的 provider（基于后端返回的 keyCount）
    return grouped.filter((g) => {
      const provider = providers.find(p => p.providerId === g.providerId);
      return provider && provider.keyCount > 0;
    });
  }, [grouped, showAllProviders, providers]);

  // 统一计算拖拽放置的 previousId/nextId，避免重复逻辑
  const computePrevNext = <T,>(ids: T[], sourceIndex: number, targetIndex: number) => {
    let previousId: T | null = null;
    let nextId: T | null = null;
    if (sourceIndex < targetIndex) {
      previousId = ids[targetIndex];
      nextId = targetIndex < ids.length - 1 ? ids[targetIndex + 1] : null;
    } else {
      previousId = targetIndex > 0 ? ids[targetIndex - 1] : null;
      nextId = ids[targetIndex];
    }
    return { previousId, nextId };
  };

  const openAddKey = (providerId: number) => {
    setCurrentProviderId(providerId);
    setIsOpenKeyModal(true);
  };

  const openConfigModels = (keyId: number, providerId: number) => {
    setCurrentModelKeyId(keyId);
    setCurrentProviderId(providerId);
    setIsOpenQuickAddModels(true);
  };

  const openAddModel = (keyId: number) => {
    setCurrentModelKeyId(keyId);
    setIsOpenAddModel(true);
  };

  const openEditModel = (m: AdminModelDto) => {
    setSelectedModel(m);
    setIsOpenEditModel(true);
  };

  const handleToggleProvider = async (providerId: number) => {
    const isCurrentlyExpanded = expandProviders[providerId];
    const newExpandProviders: Record<number, boolean> = {};
    for (const provider of providers) newExpandProviders[provider.providerId] = false;
    
    if (!isCurrentlyExpanded) {
      // 先展开 UI
      newExpandProviders[providerId] = true;
      setExpandProviders(newExpandProviders);
      
      // 然后异步加载该 provider 的 keys
      await loadProviderKeys(providerId);
      
      // 展开第一个 key
      const keys = keysByProvider[providerId];
      if (keys && keys.length > 0) {
        const newExpandKeys: Record<number, boolean> = {};
        newExpandKeys[keys[0].id] = true;
        setExpandKeys(newExpandKeys);
        
        // 加载第一个 key 的 models
        await loadKeyModels(keys[0].id);
      }
    } else {
      setExpandKeys({});
      setExpandProviders(newExpandProviders);
    }
  };

  const handleToggleKey = async (keyId: number) => {
    const newExpandKeys: Record<number, boolean> = {};
    const wasExpanded = expandKeys[keyId];
    
    // 收起所有其他 keys
    for (const keys of Object.values(keysByProvider)) {
      for (const key of keys) {
        newExpandKeys[key.id] = false;
      }
    }
    
    // 切换当前 key
    newExpandKeys[keyId] = !wasExpanded;
    
    // 如果是展开操作，先展开 UI 再加载数据
    if (!wasExpanded) {
      setExpandKeys(newExpandKeys);
      await loadKeyModels(keyId);
    } else {
      setExpandKeys(newExpandKeys);
    }
  };

  const handleEditKey = (key: GetModelKeysResult) => {
    const provider = feModelProviders.find(p => p.id === key.modelProviderId);
    if (provider) {
      setCurrentProviderId(provider.id);
      setSelectedKey(key);
      setIsOpenKeyModal(true);
    }
  };

  const handleDeleteKey = async (keyId: number) => {
    const count = (modelsByKey[keyId] || []).length;
    if (count > 0) {
      toast.error(t('Cannot delete: models exist under this key'));
      return;
    }
    await deleteModelKeys(keyId);
    toast.success(t('Deleted successful'));
    
    // 找到该 key 所属的 provider 并刷新
    for (const [providerId, keys] of Object.entries(keysByProvider)) {
      if (keys.some(k => k.id === keyId)) {
        await Promise.all([
          refreshProviders(),
          refreshProviderKeys(Number(providerId))
        ]);
        break;
      }
    }
  };

  const handleDeleteModel = async (modelId: number) => {
    await deleteModels(modelId);
    toast.success(t('Deleted successful'));
    
    // 找到该 model 所属的 key 并刷新
    for (const [keyId, models] of Object.entries(modelsByKey)) {
      if (models.some(m => m.modelId === modelId)) {
        await Promise.all([
          refreshProviders(),
          refreshKeyModels(Number(keyId))
        ]);
        break;
      }
    }
  };

  const handleGoToUsage = (params: {
    provider?: string;
    modelKey?: string;
    model?: string;
  }) => {
    const query: Record<string, string> = {};
    if (params.provider) query.provider = params.provider;
    if (params.modelKey) query['model-key'] = params.modelKey;
    if (params.model) query.model = params.model;
    
    router.push({
      pathname: '/admin/usage',
      query,
    });
  };

  // 旧的原生拖拽处理已移除，统一用 dnd-kit

  const onDragStart = (event: any) => {
    const id = String(event.active.id);
    setActiveId(id);
    if (id.startsWith('provider-')) {
      const providerId = Number(id.replace('provider-', ''));
      const provider = grouped.find(g => g.providerId === providerId) || null;
      setCurrentDragProvider(provider);
      // 收起所有 provider 与 key
      const newExpandProviders: Record<number, boolean> = {};
      for (const p of providers) newExpandProviders[p.providerId] = false;
      setExpandProviders(newExpandProviders);
      setExpandKeys({});
    } else if (id.startsWith('key-')) {
      const keyId = Number(id.replace('key-', ''));
      // 从 keysByProvider 中查找 key
      let key: GetModelKeysResult | null = null;
      for (const keys of Object.values(keysByProvider)) {
        const found = keys.find(k => k.id === keyId);
        if (found) {
          key = found;
          break;
        }
      }
      if (key) {
        const currentKeyId = key.id; // 保存 keyId 避免 TypeScript 类型检查问题
        setCurrentDragKey(key);
        setExpandKeys((prev) => ({ ...prev, [currentKeyId]: false }));
      }
    }
  };

  // 使用 dnd-kit 统一处理拖拽结束：识别是 Provider 还是 Key 还是 Model 的排序
  const onDragEnd = async (event: DragEndEvent) => {
    const { active, over } = event;
    if (!over) return;

    const activeId: string = String(active.id);
    const overId: string = String(over.id);

    // Provider 排序：id 形如 provider-<id>
    if (activeId.startsWith('provider-') && overId.startsWith('provider-')) {
      const pid = (s: string) => Number(s.replace('provider-', ''));
      const activePid = pid(activeId);
      const overPid = pid(overId);
      if (activePid === overPid) return;

      const ids = filteredProviders.map(p => p.providerId);
      const sourceIndex = ids.indexOf(activePid);
      const targetIndex = ids.indexOf(overPid);
      const { previousId, nextId } = computePrevNext(ids, sourceIndex, targetIndex);

      // 乐观更新 provider 顺序
      const orderMoved = arrayMove(ids, sourceIndex, targetIndex);
      const reorderedProviders = orderMoved.map(id => providers.find(p => p.providerId === id)!);
      setProviders(reorderedProviders);
      
      try {
        await reorderModelProviders({ sourceId: activePid, previousId, nextId });
        await refreshProviders();
      } catch (error) {
        // 失败时恢复
        await refreshProviders();
        toast.error(t('Reorder failed'));
      }
      setActiveId(null);
      setCurrentDragProvider(null);
      return;
    }

    // Key 排序：id 形如 key-<id>
    if (activeId.startsWith('key-') && overId.startsWith('key-')) {
      const kid = (s: string) => Number(s.replace('key-', ''));
      const activeKid = kid(activeId);
      const overKid = kid(overId);
      if (activeKid === overKid) return;

      // 找到 active 和 over key 所属的 provider
      let activeProviderId: number | null = null;
      let activeKey: GetModelKeysResult | null = null;
      let overKey: GetModelKeysResult | null = null;

      for (const [providerId, keys] of Object.entries(keysByProvider)) {
        const active = keys.find(k => k.id === activeKid);
        const over = keys.find(k => k.id === overKid);
        if (active) {
          activeProviderId = Number(providerId);
          activeKey = active;
        }
        if (over) {
          overKey = over;
        }
      }

      // 限制在同一 provider 内
      if (!activeKey || !overKey || activeKey.modelProviderId !== overKey.modelProviderId) return;

      const keys = keysByProvider[activeProviderId!] || [];
      const ids = keys.map(k => k.id);
      const sourceIndex = ids.indexOf(activeKid);
      const targetIndex = ids.indexOf(overKid);
      const { previousId, nextId } = computePrevNext(ids, sourceIndex, targetIndex);

      // 乐观更新 key 顺序
      const movedIds = arrayMove(ids, sourceIndex, targetIndex);
      const reorderedKeys = movedIds.map(id => keys.find(k => k.id === id)!);
      setKeysByProvider(prev => ({ ...prev, [activeProviderId!]: reorderedKeys }));
      
      try {
        await reorderModelKeys({ sourceId: activeKid, previousId, nextId });
        await refreshProviderKeys(activeProviderId!);
      } catch (error) {
        // 失败时恢复
        await refreshProviderKeys(activeProviderId!);
        toast.error(t('Reorder failed'));
      }
      setActiveId(null);
      setCurrentDragKey(null);
      return;
    }

    // Model 排序：id 形如 model-<id>
    if (activeId.startsWith('model-') && overId.startsWith('model-')) {
      const mid = (s: string) => Number(s.replace('model-', ''));
      const activeMid = mid(activeId);
      const overMid = mid(overId);
      if (activeMid === overMid) return;

      // 找到 active 和 over model 所属的 key
      let activeKeyId: number | null = null;
      let activeModel: AdminModelDto | null = null;
      let overModel: AdminModelDto | null = null;

      for (const [keyId, models] of Object.entries(modelsByKey)) {
        const active = models.find(m => m.modelId === activeMid);
        const over = models.find(m => m.modelId === overMid);
        if (active) {
          activeKeyId = Number(keyId);
          activeModel = active;
        }
        if (over) {
          overModel = over;
        }
      }

      // 限制在同一 key 内
      if (!activeModel || !overModel || activeModel.modelKeyId !== overModel.modelKeyId) return;

      const models = modelsByKey[activeKeyId!] || [];
      const ids = models.map(m => m.modelId);
      const sourceIndex = ids.indexOf(activeMid);
      const targetIndex = ids.indexOf(overMid);
      const { previousId, nextId } = computePrevNext(ids, sourceIndex, targetIndex);

      // 乐观更新 model 顺序
      const movedIds = arrayMove(ids, sourceIndex, targetIndex);
      const reorderedModels = movedIds.map(id => models.find(m => m.modelId === id)!);
      setModelsByKey(prev => ({ ...prev, [activeKeyId!]: reorderedModels }));
      
      try {
        await reorderModels({ sourceId: activeMid, previousId, nextId });
        await refreshKeyModels(activeKeyId!);
      } catch (error) {
        // 失败时恢复
        await refreshKeyModels(activeKeyId!);
        toast.error(t('Reorder failed'));
      }
      setActiveId(null);
      return;
    }
    setActiveId(null);
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-6">
          <LabelSwitch
            checked={showAllProviders}
            onCheckedChange={setShowAllProviders}
            label={t('Show all providers')}
          />
        </div>
        <div className="flex items-center gap-2">
          <IconActionButton
            label={t('Add Model Key')}
            icon={<IconPlus size={18} />}
            onClick={() => {
              setCurrentProviderId(undefined);
              setIsOpenKeyModal(true);
            }}
          />
        </div>
      </div>

      <DndContext
        sensors={sensors}
        onDragStart={onDragStart}
        onDragEnd={onDragEnd}
  collisionDetection={closestCorners}
        modifiers={[restrictToVerticalAxis]}
        measuring={{ droppable: { strategy: MeasuringStrategy.Always } }}
      >
      <SortableContext
        items={filteredProviders.map(p => `provider-${p.providerId}`)}
        strategy={verticalListSortingStrategy}
      >
      <div className="space-y-2">
        {loading ? (
          <div className="space-y-3">
            {/* 模拟3个Provider卡片的骨架屏 */}
            {[1, 2, 3].map((index) => (
              <div key={index} className="rounded-xl border bg-card p-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Skeleton className="h-6 w-6 rounded" />
                    <Skeleton className="h-5 w-20" />
                    <Skeleton className="h-4 w-32" />
                  </div>
                  <div className="flex items-center gap-2">
                    <Skeleton className="h-8 w-16" />
                    <Skeleton className="h-4 w-4" />
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : filteredProviders.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-24 border rounded-xl bg-muted/30 text-center gap-4">
            <div className="text-lg font-medium">{t('No providers or model keys yet')}</div>
            <div className="text-sm text-muted-foreground max-w-md">
              {t('You have no model keys under any provider. Add a model key first or show all providers to start.')}
            </div>
            <div className="flex gap-3">
              {!showAllProviders && (
                <Button variant="secondary" onClick={() => setShowAllProviders(true)}>
                  {t('Show all providers')}
                </Button>
              )}
              <Button onClick={() => { setCurrentProviderId(undefined); setIsOpenKeyModal(true); }}>
                {t('Add Model Key')}
              </Button>
            </div>
          </div>
        ) : (
          filteredProviders.map((provider) => (
            <ModelProvider
              key={`provider-${provider.providerId}`}
              provider={provider}
              modelsByKey={modelsByKey}
              expanded={!!expandProviders[provider.providerId]}
              expandedKeys={expandKeys}
              loading={loadingKeys[provider.providerId]}
              loadingModels={loadingModels}
              onToggleExpand={() => handleToggleProvider(provider.providerId)}
              onToggleKeyExpand={handleToggleKey}
              onAddKey={openAddKey}
              onEditKey={handleEditKey}
              onDeleteKey={handleDeleteKey}
              onConfigModels={(keyId) => openConfigModels(keyId, provider.providerId)}
              onAddModel={openAddModel}
              onEditModel={openEditModel}
              onDeleteModel={handleDeleteModel}
              onGoToUsage={handleGoToUsage}
            />
          ))
        )}
      </div>
      </SortableContext>
      <DragOverlay>
        {activeId?.startsWith('provider-') && (() => {
          const pid = Number(activeId.replace('provider-', ''));
          const p = grouped.find(g => g.providerId === pid);
          if (!p) return null;
          return (
            <div className="rounded-xl border bg-card shadow-md">
              <div className="flex items-center justify-between p-3">
                <div className="flex items-center gap-2">
                  {/* 简化的 Overlay 头部，避免在 Overlay 中再次使用 useSortable */}
                  <span className="font-semibold">{t(p.providerName)}</span>
                  <span className="text-muted-foreground text-sm">
                    {t('Model Keys')}: {p.keyCount}
                    <span className="hidden sm:inline"> {t('Models')}: {p.modelCount}</span>
                  </span>
                </div>
              </div>
            </div>
          );
        })()}
        {activeId?.startsWith('key-') && (() => {
          const kid = Number(activeId.replace('key-', ''));
          let k: GetModelKeysResult | null = null;
          for (const keys of Object.values(keysByProvider)) {
            const found = keys.find(x => x.id === kid);
            if (found) {
              k = found;
              break;
            }
          }
          if (!k) return null;
          return (
            <div className="rounded-md border bg-background/90 shadow-md">
              <div className="flex items-center justify-between p-3">
                <div className="flex items-center gap-2">
                  <span className="font-medium">{k.name}</span>
                </div>
              </div>
            </div>
          );
        })()}
        {activeId?.startsWith('model-') && (() => {
          const mid = Number(activeId.replace('model-', ''));
          let m: AdminModelDto | null = null;
          for (const models of Object.values(modelsByKey)) {
            const found = models.find(x => x.modelId === mid);
            if (found) {
              m = found;
              break;
            }
          }
          if (!m) return null;
          return (
            <div className="rounded border bg-background/90 shadow-md px-2 py-1">
              <div className="flex items-center justify-between">
                <div className="flex-1 min-w-0">
                  <div className="truncate font-medium">{m.name}</div>
                  <div className="text-xs text-blue-600 truncate">
                    {'￥' + formatNumberAsMoney(m.inputTokenPrice1M) + '/' + formatNumberAsMoney(m.outputTokenPrice1M)}
                  </div>
                </div>
              </div>
            </div>
          );
        })()}
      </DragOverlay>
      </DndContext>

      {isOpenKeyModal && (
        <ModelKeysModal
          selected={selectedKey || null}
          isOpen={isOpenKeyModal}
          onClose={() => {
            setIsOpenKeyModal(false);
            setSelectedKey(undefined);
          }}
          onSaveSuccessful={async () => {
            setIsOpenKeyModal(false);
            setSelectedKey(undefined);
            await refreshProviders();
            // 如果有展开的 provider，刷新其 keys
            const expandedProviderId = Object.keys(expandProviders).find(
              k => expandProviders[Number(k)]
            );
            if (expandedProviderId) {
              await refreshProviderKeys(Number(expandedProviderId));
            }
          }}
          onDeleteSuccessful={async () => {
            setIsOpenKeyModal(false);
            setSelectedKey(undefined);
            await refreshProviders();
            // 如果有展开的 provider，刷新其 keys
            const expandedProviderId = Object.keys(expandProviders).find(
              k => expandProviders[Number(k)]
            );
            if (expandedProviderId) {
              await refreshProviderKeys(Number(expandedProviderId));
            }
          }}
          defaultModelProviderId={currentProviderId}
        />
      )}

      {isOpenQuickAddModels && currentModelKeyId !== undefined && (
        <QuickAddModelModal
          modelKeyId={currentModelKeyId}
          modelProverId={currentProviderId!}
          isOpen={isOpenQuickAddModels}
          onClose={() => setIsOpenQuickAddModels(false)}
          onSuccessful={async () => {
            // 不关闭对话框，只刷新数据，让用户可以继续添加
            await Promise.all([
              refreshProviders(),
              refreshKeyModels(currentModelKeyId)
            ]);
          }}
          onOpenEditModel={(deploymentName, apiType) => {
            // 根据 deploymentName 和 currentModelKeyId 找到对应的模型
            const models = modelsByKey[currentModelKeyId] || [];
            const model = models.find(
              m => m.deploymentName === deploymentName && m.modelKeyId === currentModelKeyId
            );
            if (model) {
              // 编辑已存在的模型
              setSelectedModel(model);
              setIsOpenEditModel(true);
            } else {
              // 创建新模型，根据 apiType 设置默认配置
              const defaultConfig = getDefaultConfigByApiType(apiType as ApiType);
              
              // 打开添加模型对话框，并传递默认值
              setIsOpenAddModel(true);
              // defaultValues 按照后端 UpdateModelDto 格式传递
              setAddModelDefaults({
                name: deploymentName, // 使用部署名称作为模型显示名称的默认值
                deploymentName: deploymentName,
                modelKeyId: currentModelKeyId, // 保持 number 类型，让 ModelModal 自动转换
                ...defaultConfig,
              });
            }
            // 保持 ConfigModelModal 打开，用户可以继续操作其他模型
          }}
        />
      )}

      {isOpenAddModel && (
        <ModelModal
          isOpen={isOpenAddModel}
          onClose={() => {
            setIsOpenAddModel(false);
            setAddModelDefaults(undefined); // 清除默认值
          }}
          onSuccessful={async () => {
            setIsOpenAddModel(false);
            setAddModelDefaults(undefined); // 清除默认值
            if (currentModelKeyId) {
              await Promise.all([
                refreshProviders(),
                refreshKeyModels(currentModelKeyId)
              ]);
            }
          }}
          modelKeys={Object.values(keysByProvider).flat()}
          defaultValues={addModelDefaults || {
            modelKeyId: currentModelKeyId // number 类型
          }}
        />
      )}

      {isOpenEditModel && selectedModel && (
        <ModelModal
          isOpen={isOpenEditModel}
          onClose={() => setIsOpenEditModel(false)}
          onSuccessful={async () => {
            setIsOpenEditModel(false);
            if (selectedModel) {
              await Promise.all([
                refreshProviders(),
                refreshKeyModels(selectedModel.modelKeyId)
              ]);
            }
          }}
          selected={selectedModel}
          modelKeys={Object.values(keysByProvider).flat()}
        />
      )}
    </div>
  );
}
