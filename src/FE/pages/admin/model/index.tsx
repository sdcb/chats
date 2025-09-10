import React, { DragEvent, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';
import { AdminModelDto, GetModelKeysResult } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';
import { Button } from '@/components/ui/button';
import { LabelSwitch } from '@/components/ui/label-switch';
import { IconPlus } from '@/components/Icons';
import { getModelKeys, getModels, deleteModelKeys, deleteModels, reorderModelProviders, reorderModelKeys } from '@/apis/adminApis';
import ModelKeysModal from '../_components/ModelKeys/ModelKeysModal';
import ConfigModelModal from '../_components/ModelKeys/ConfigModelModal';
import AddModelModal from '../_components/Models/AddModelModal';
import EditModelModal from '../_components/Models/EditModelModal';
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
  const providerRefs = useRef<Record<number, HTMLElement | null>>({});
  
  // ModelKey drag state
  const [currentDragKey, setCurrentDragKey] = useState<GetModelKeysResult | null>(null);
  const keyRefs = useRef<Record<number, HTMLElement | null>>({});

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

  // 拖拽处理函数
  const handleRemoveDragStyles = () => {
  if (!providerRefs.current) return;
  Object.keys(providerRefs.current).forEach((key: string) => {
      const element = providerRefs.current[Number(key)];
      if (element) {
        element.style.background = 'none';
        element.style.borderTop = 'none';
        element.style.borderBottom = 'none';
      }
    });
  };

  const handleProviderDragStart = (e: DragEvent<HTMLDivElement>, provider: ProviderGroup) => {
    // 如果没有 keys，不允许拖拽
    if (provider.keys.length === 0) {
      e.preventDefault();
      return;
    }

    setCurrentDragProvider(provider);
    // 收起所有展开的 provider
    const newExpandProviders: Record<number, boolean> = {};
    for (const p of feModelProviders) newExpandProviders[p.id] = false;
    setExpandProviders(newExpandProviders);
    // 同时收起所有 Key
    setExpandKeys((prev) => {
      const next: Record<number, boolean> = { ...prev };
      for (const k of modelKeys) next[k.id] = false;
      return next;
    });
  };

  const handleDragOver = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
  };

  const handleDragEnter = (index: number, providerId: number) => {
    if (currentDragProvider && currentDragProvider.providerId !== providerId) {
      // 视觉反馈：在目标位置显示插入线
      Object.keys(providerRefs.current).forEach((key: string) => {
        const element = providerRefs.current[Number(key)];
        if (element) {
          if (Number(key) === providerId) {
            // 根据拖拽方向显示不同的边框
            const sourceIndex = filteredProviders.findIndex(p => p.providerId === currentDragProvider.providerId);
            if (sourceIndex < index) {
              // 向下拖拽，在下方显示边框
              element.style.borderBottom = '2px solid hsl(var(--primary))';
              element.style.borderTop = 'none';
            } else {
              // 向上拖拽，在上方显示边框
              element.style.borderTop = '2px solid hsl(var(--primary))';
              element.style.borderBottom = 'none';
            }
            element.style.background = 'hsl(var(--muted) / 0.5)';
          } else {
            element.style.background = 'none';
            element.style.borderTop = 'none';
            element.style.borderBottom = 'none';
          }
        }
      });
    }
  };

  const handleDrop = async (e: DragEvent<HTMLDivElement>, targetProvider: ProviderGroup, targetIndex: number) => {
    e.preventDefault();
    
    if (!currentDragProvider || currentDragProvider.providerId === targetProvider.providerId) {
      handleRemoveDragStyles();
      setCurrentDragProvider(null);
      return;
    }

    try {
  // 计算拖拽的方向和位置（共用方法）
  const providerIds = filteredProviders.map(p => p.providerId);
  const sourceIndex = providerIds.findIndex(id => id === currentDragProvider.providerId);
  const { previousId, nextId } = computePrevNext(providerIds, sourceIndex, targetIndex);

      await reorderModelProviders({
        sourceId: currentDragProvider.providerId,
        previousId,
        nextId,
      });

  toast.success(t('Reorder successful'));
  // 按后端为准，刷新数据
  init(true);
    } catch (error) {
      toast.error(t('Reorder failed'));
      console.error('Reorder error:', error);
    }

    handleRemoveDragStyles();
    setCurrentDragProvider(null);
  };

  // ModelKey 拖拽处理函数
  const handleRemoveKeyDragStyles = () => {
  if (!keyRefs.current) return;
  Object.keys(keyRefs.current).forEach((key: string) => {
      const element = keyRefs.current[Number(key)];
      if (element) {
        element.style.background = 'none';
        element.style.borderTop = 'none';
        element.style.borderBottom = 'none';
      }
    });
  };

  const handleKeyDragStart = (e: React.DragEvent<HTMLDivElement>, key: GetModelKeysResult) => {
    setCurrentDragKey(key);
    // 拖拽开始时，收起该模型密钥
    setExpandKeys((prev) => ({
      ...prev,
      [key.id]: false,
    }));
  };

  const handleKeyDragEnter = (index: number, keyId: number, providerId: number) => {
    if (currentDragKey && currentDragKey.id !== keyId && currentDragKey.modelProviderId === providerId) {
      // 只在同一个 Provider 内显示视觉反馈
      Object.keys(keyRefs.current).forEach((key: string) => {
        const element = keyRefs.current[Number(key)];
        if (element) {
          if (Number(key) === keyId) {
            // 获取当前 Provider 的 keys
            const providerGroup = grouped.find(g => g.providerId === providerId);
            if (providerGroup) {
              const sourceIndex = providerGroup.keys.findIndex(k => k.id === currentDragKey.id);
              if (sourceIndex < index) {
                element.style.borderBottom = '2px solid hsl(var(--primary))';
                element.style.borderTop = 'none';
              } else {
                element.style.borderTop = '2px solid hsl(var(--primary))';
                element.style.borderBottom = 'none';
              }
              element.style.background = 'hsl(var(--muted) / 0.5)';
            }
          } else {
            element.style.background = 'none';
            element.style.borderTop = 'none';
            element.style.borderBottom = 'none';
          }
        }
      });
    }
  };

  const handleKeyDrop = async (e: React.DragEvent<HTMLDivElement>, targetKey: GetModelKeysResult, targetIndex: number) => {
    e.preventDefault();
    
    if (!currentDragKey || currentDragKey.id === targetKey.id) {
      handleRemoveKeyDragStyles();
      setCurrentDragKey(null);
      return;
    }

    // 确保只在同一个 Provider 内重排序
    if (currentDragKey.modelProviderId !== targetKey.modelProviderId) {
      toast.error(t('Cannot move keys between different providers'));
      handleRemoveKeyDragStyles();
      setCurrentDragKey(null);
      return;
    }

  try {
      // 获取当前 Provider 的 keys
      const providerGroup = grouped.find(g => g.providerId === currentDragKey.modelProviderId);
      if (!providerGroup) {
        throw new Error('Provider not found');
      }

  const ids = providerGroup.keys.map(k => k.id);
  const sourceIndex = ids.findIndex(id => id === currentDragKey.id);
  const { previousId, nextId } = computePrevNext(ids, sourceIndex, targetIndex);

      await reorderModelKeys({
        sourceId: currentDragKey.id,
        previousId,
        nextId,
      });

  toast.success(t('Reorder successful'));
  // 按后端为准，刷新数据
  init(true);
    } catch (error) {
      toast.error(t('Reorder failed'));
      console.error('ModelKey reorder error:', error);
    }

    handleRemoveKeyDragStyles();
    setCurrentDragKey(null);
  };

  const handleKeyDragEnd = () => {
    handleRemoveKeyDragStyles();
    setCurrentDragKey(null);
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
          filteredProviders.map((provider, index) => (
            <div
              key={provider.providerId}
              ref={(el) => (providerRefs.current[provider.providerId] = el)}
              onDragOver={handleDragOver}
              onDragEnter={() => handleDragEnter(index, provider.providerId)}
              onDrop={(e) => handleDrop(e, provider, index)}
              onDragEnd={handleRemoveDragStyles}
              className="rounded-md"
            >
              <ModelProvider
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
                isDragging={currentDragProvider?.providerId === provider.providerId}
                onDragStart={(e) => handleProviderDragStart(e, provider)}
                // ModelKey 拖拽相关
                currentDragKey={currentDragKey}
                keyRefs={keyRefs}
                onKeyDragStart={handleKeyDragStart}
                onKeyDragEnter={handleKeyDragEnter}
                onKeyDrop={handleKeyDrop}
                onKeyDragEnd={handleKeyDragEnd}
              />
            </div>
          ))
        )}
      </div>

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
        <AddModelModal
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
        <EditModelModal
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
