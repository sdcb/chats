import React, { DragEvent, useEffect, useMemo, useRef, useState } from 'react';
import { DndContext, DragEndEvent, DragOverlay, PointerSensor, useSensor, useSensors, closestCorners, MeasuringStrategy } from '@dnd-kit/core';
import { SortableContext, verticalListSortingStrategy, arrayMove } from '@dnd-kit/sortable';
import { restrictToVerticalAxis } from '@dnd-kit/modifiers';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';
import { AdminModelDto, GetModelKeysResult } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';
import { formatNumberAsMoney } from '@/utils/common';
import { Button } from '@/components/ui/button';
import { LabelSwitch } from '@/components/ui/label-switch';
import { IconPlus } from '@/components/Icons';
import { getModelKeys, getModels, deleteModelKeys, deleteModels, reorderModelProviders, reorderModelKeys, reorderModels } from '@/apis/adminApis';
import ModelKeysModal from '../_components/ModelKeys/ModelKeysModal';
import ConfigModelModal from '../_components/ModelKeys/ConfigModelModal';
import ModelModal from '../_components/Models/ModelModal';
import ModelProvider, { ProviderGroup } from './components/ModelProvider';

export default function ModelManager() {
  const { t } = useTranslation();

  // data state
  const [modelKeys, setModelKeys] = useState<GetModelKeysResult[]>([]);
  const [models, setModels] = useState<AdminModelDto[]>([]);
  const [loading, setLoading] = useState(true);

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
  const [isOpenConfigModels, setIsOpenConfigModels] = useState(false);
  const [isOpenAddModel, setIsOpenAddModel] = useState(false);
  const [isOpenEditModel, setIsOpenEditModel] = useState(false);

  // defaults for dialogs
  const [currentProviderId, setCurrentProviderId] = useState<number | undefined>(undefined);
  const [currentModelKeyId, setCurrentModelKeyId] = useState<number | undefined>(undefined);
  const [selectedModel, setSelectedModel] = useState<AdminModelDto | undefined>(undefined);
  const [selectedKey, setSelectedKey] = useState<GetModelKeysResult | undefined>(undefined);

  useEffect(() => {
    init();
  }, []);

  const init = async (preserveExpandState = false) => {
    setLoading(true);
    const [keys, ms] = await Promise.all([getModelKeys(), getModels(true)]);
    setModelKeys(keys);
    setModels(ms);
    if (!preserveExpandState) {
      const providerExpand: Record<number, boolean> = {};
      const keyExpand: Record<number, boolean> = {};
      for (const p of feModelProviders) providerExpand[p.id] = false;
      for (const k of keys) keyExpand[k.id] = false;
      setExpandProviders(providerExpand);
      setExpandKeys(keyExpand);
    }
    setLoading(false);
  };

  const grouped: ProviderGroup[] = useMemo(() => {
    // 创建一个 Map 来存储每个 provider 的 group
    const groupsMap = new Map<number, ProviderGroup>();
    
    // 初始化所有 provider 的 group
    for (const p of feModelProviders) {
      groupsMap.set(p.id, {
        providerId: p.id,
        providerName: p.name,
        keys: [],
      });
    }
    
    // 按照 modelKeys 中 modelProviderId 第一次出现的顺序收集 provider
    const orderedProviderIds: number[] = [];
    const seenProviderIds = new Set<number>();
    
    for (const k of modelKeys) {
      if (!seenProviderIds.has(k.modelProviderId)) {
        orderedProviderIds.push(k.modelProviderId);
        seenProviderIds.add(k.modelProviderId);
      }
      
      const group = groupsMap.get(k.modelProviderId);
      if (group) {
        group.keys.push(k);
      }
    }
    
    // 按顺序构建最终的 groups 数组
    const orderedGroups: ProviderGroup[] = [];
    
    // 首先添加有 keys 的 provider（按 modelKeys 中出现的顺序）
    for (const providerId of orderedProviderIds) {
      const group = groupsMap.get(providerId);
      if (group) {
        orderedGroups.push(group);
      }
    }
    
    // 然后添加没有 keys 的 provider（保持原有顺序）
    for (const p of feModelProviders) {
      if (!seenProviderIds.has(p.id)) {
        const group = groupsMap.get(p.id);
        if (group) {
          orderedGroups.push(group);
        }
      }
    }
    
    return orderedGroups;
  }, [modelKeys]);

  const modelsByKey = useMemo(() => {
    const map: Record<number, AdminModelDto[]> = {};
    for (const m of models) {
      if (!map[m.modelKeyId]) map[m.modelKeyId] = [];
      map[m.modelKeyId].push(m);
    }
    return map;
  }, [models]);

  const modelCountByProvider = useMemo(() => {
    const countMap: Record<number, number> = {};
    for (const g of grouped) {
      const count = g.keys.reduce((sum, k) => sum + (modelsByKey[k.id]?.length || 0), 0);
      countMap[g.providerId] = count;
    }
    return countMap;
  }, [grouped, modelsByKey]);

  const filteredProviders = useMemo(() => {
    return grouped.filter((g) => showAllProviders || g.keys.length > 0);
  }, [grouped, showAllProviders]);

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
    setIsOpenConfigModels(true);
  };

  const openAddModel = (keyId: number) => {
    setCurrentModelKeyId(keyId);
    setIsOpenAddModel(true);
  };

  const openEditModel = (m: AdminModelDto) => {
    setSelectedModel(m);
    setIsOpenEditModel(true);
  };

  const handleToggleProvider = (providerId: number) => {
    const isCurrentlyExpanded = expandProviders[providerId];
    const newExpandProviders: Record<number, boolean> = {};
    for (const provider of feModelProviders) newExpandProviders[provider.id] = false;
    if (!isCurrentlyExpanded) {
      newExpandProviders[providerId] = true;
      const newExpandKeys: Record<number, boolean> = {};
      for (const k of modelKeys) newExpandKeys[k.id] = false;
      const providerGroup = grouped.find(g => g.providerId === providerId);
      if (providerGroup && providerGroup.keys.length > 0) {
        newExpandKeys[providerGroup.keys[0].id] = true;
      }
      setExpandKeys(newExpandKeys);
    } else {
      const newExpandKeys: Record<number, boolean> = {};
      for (const k of modelKeys) newExpandKeys[k.id] = false;
      setExpandKeys(newExpandKeys);
    }
    setExpandProviders(newExpandProviders);
  };

  const handleToggleKey = (keyId: number) => {
    const newExpandKeys: Record<number, boolean> = {};
    for (const key of modelKeys) newExpandKeys[key.id] = false;
    newExpandKeys[keyId] = !expandKeys[keyId];
    setExpandKeys(newExpandKeys);
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
    init(true);
  };

  const handleDeleteModel = async (modelId: number) => {
    await deleteModels(modelId);
    toast.success(t('Deleted successful'));
    init(true);
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
      for (const p of feModelProviders) newExpandProviders[p.id] = false;
      setExpandProviders(newExpandProviders);
      setExpandKeys((prev) => {
        const next: Record<number, boolean> = { ...prev };
        for (const k of modelKeys) next[k.id] = false;
        return next;
      });
    } else if (id.startsWith('key-')) {
      const keyId = Number(id.replace('key-', ''));
      const key = modelKeys.find(k => k.id === keyId) || null;
      if (key) {
        setCurrentDragKey(key);
        setExpandKeys((prev) => ({ ...prev, [key.id]: false }));
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

      // 乐观更新 provider 顺序：通过 modelKeys 的 provider 首次出现顺序实现
      const ids = filteredProviders.map(p => p.providerId);
      const sourceIndex = ids.indexOf(activePid);
      const targetIndex = ids.indexOf(overPid);
      const { previousId, nextId } = computePrevNext(ids, sourceIndex, targetIndex);

      // 先本地乐观更新，避免松手后弹回
      const orderMoved = arrayMove(ids, sourceIndex, targetIndex);
      setModelKeys(prev => {
        // 重建 modelKeys：按新 provider 顺序拼接各自 keys
        const groupedMap = new Map<number, GetModelKeysResult[]>();
        for (const mk of prev) {
          const arr = groupedMap.get(mk.modelProviderId) || [];
          arr.push(mk);
          groupedMap.set(mk.modelProviderId, arr);
        }
        const result: GetModelKeysResult[] = [];
        for (const id of orderMoved) result.push(...(groupedMap.get(id) || []));
        return result;
      });
      try {
        await reorderModelProviders({ sourceId: activePid, previousId, nextId });
      } finally {
        // 无论成功失败都刷新一次，确保与后端一致；失败时可考虑提示并回滚
        init(true);
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

      // 限制在同一 provider 内
      const activeKey = modelKeys.find(k => k.id === activeKid);
      const overKey = modelKeys.find(k => k.id === overKid);
      if (!activeKey || !overKey || activeKey.modelProviderId !== overKey.modelProviderId) return;

      const providerGroup = grouped.find(g => g.providerId === activeKey.modelProviderId);
      if (!providerGroup) return;
      const ids = providerGroup.keys.map(k => k.id);
      const sourceIndex = ids.indexOf(activeKid);
      const targetIndex = ids.indexOf(overKid);
      const { previousId, nextId } = computePrevNext(ids, sourceIndex, targetIndex);

      // 先本地顺序调整（乐观更新）
      const moved = arrayMove(ids, sourceIndex, targetIndex);
      setModelKeys(prev => {
        const idToKey = new Map(prev.map(k => [k.id, k] as const));
        const result: GetModelKeysResult[] = [];
        for (const mk of prev) {
          if (mk.modelProviderId !== activeKey.modelProviderId) {
            result.push(mk);
          } else {
            // 依照 moved 的顺序输出该 provider 的 keys（逐个匹配）
            const nextId = moved.shift()!;
            result.push(idToKey.get(nextId)!);
          }
        }
        return result;
      });
      try {
        await reorderModelKeys({ sourceId: activeKid, previousId, nextId });
      } finally {
        init(true);
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

      // 限制在同一 key 内
      const activeModel = models.find(m => m.modelId === activeMid);
      const overModel = models.find(m => m.modelId === overMid);
      if (!activeModel || !overModel || activeModel.modelKeyId !== overModel.modelKeyId) return;

      const keyModels = modelsByKey[activeModel.modelKeyId] || [];
      const ids = keyModels.map(m => m.modelId);
      const sourceIndex = ids.indexOf(activeMid);
      const targetIndex = ids.indexOf(overMid);
      const { previousId, nextId } = computePrevNext(ids, sourceIndex, targetIndex);

      // 先本地顺序调整（乐观更新）
      const moved = arrayMove(ids, sourceIndex, targetIndex);
      setModels(prev => {
        const idToModel = new Map(prev.map(m => [m.modelId, m] as const));
        const result: AdminModelDto[] = [];
        for (const m of prev) {
          if (m.modelKeyId !== activeModel.modelKeyId) {
            result.push(m);
          } else {
            // 依照 moved 的顺序输出该 key 的 models
            const nextId = moved.shift()!;
            result.push(idToModel.get(nextId)!);
          }
        }
        return result;
      });
      try {
        await reorderModels({ sourceId: activeMid, previousId, nextId });
      } finally {
        init(true);
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
          <Button
            variant="secondary"
            onClick={() => {
              setCurrentProviderId(undefined);
              setIsOpenKeyModal(true);
            }}
            title={t('Add Model Key')}
          >
            <IconPlus size={16} />
          </Button>
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
        {filteredProviders.length === 0 ? (
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
              modelCount={modelCountByProvider[provider.providerId] || 0}
              expanded={!!expandProviders[provider.providerId]}
              expandedKeys={expandKeys}
              onToggleExpand={() => handleToggleProvider(provider.providerId)}
              onToggleKeyExpand={handleToggleKey}
              onAddKey={openAddKey}
              onEditKey={handleEditKey}
              onDeleteKey={handleDeleteKey}
              onConfigModels={(keyId) => openConfigModels(keyId, provider.providerId)}
              onAddModel={openAddModel}
              onEditModel={openEditModel}
              onDeleteModel={handleDeleteModel}
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
                    {t('Model Keys')}: {p.keys.length} {t('Models')}: {p.keys.reduce((sum, k) => sum + (modelsByKey[k.id]?.length || 0), 0)}
                  </span>
                </div>
              </div>
            </div>
          );
        })()}
        {activeId?.startsWith('key-') && (() => {
          const kid = Number(activeId.replace('key-', ''));
          const k = modelKeys.find(x => x.id === kid);
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
          const m = models.find(x => x.modelId === mid);
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
          onSaveSuccessful={() => {
            setIsOpenKeyModal(false);
            setSelectedKey(undefined);
            init(true);
          }}
          onDeleteSuccessful={() => {
            setIsOpenKeyModal(false);
            setSelectedKey(undefined);
            init(true);
          }}
          defaultModelProviderId={currentProviderId}
        />
      )}

      {isOpenConfigModels && currentModelKeyId !== undefined && (
        <ConfigModelModal
          modelKeyId={currentModelKeyId}
          modelProverId={currentProviderId!}
          isOpen={isOpenConfigModels}
          onClose={() => setIsOpenConfigModels(false)}
          onSuccessful={() => {
            setIsOpenConfigModels(false);
            init(true);
          }}
        />
      )}

      {isOpenAddModel && (
        <ModelModal
          isOpen={isOpenAddModel}
          onClose={() => setIsOpenAddModel(false)}
          onSuccessful={() => {
            setIsOpenAddModel(false);
            init(true);
          }}
          modelKeys={modelKeys}
          defaultModelKeyId={currentModelKeyId}
        />
      )}

      {isOpenEditModel && selectedModel && (
        <ModelModal
          isOpen={isOpenEditModel}
          onClose={() => setIsOpenEditModel(false)}
          onSuccessful={() => {
            setIsOpenEditModel(false);
            init(true);
          }}
          selected={selectedModel}
          modelKeys={modelKeys}
        />
      )}
    </div>
  );
}
