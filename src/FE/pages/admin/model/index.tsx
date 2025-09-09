import React, { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';

import { formatNumberAsMoney } from '@/utils/common';

import {
  AdminModelDto,
  GetModelKeysResult,
} from '@/types/adminApis';
import { feModelProviders } from '@/types/model';

import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import ChatIcon from '@/components/ChatIcon/ChatIcon';
import DeletePopover from '@/pages/home/_components/Popover/DeletePopover';
import { IconPlus, IconPencil, IconBolt } from '@/components/Icons';

import {
  getModelKeys,
  getModels,
  deleteModelKeys,
  deleteModels,
} from '@/apis/adminApis';
import ModelKeysModal from '../_components/ModelKeys/ModelKeysModal';
import ConfigModelModal from '../_components/ModelKeys/ConfigModelModal';
import AddModelModal from '../_components/Models/AddModelModal';
import EditModelModal from '../_components/Models/EditModelModal';
import { cn } from '@/lib/utils';

type ProviderGroup = {
  providerId: number;
  providerName: string;
  keys: GetModelKeysResult[];
};

function CollapsiblePanel({
  open,
  children,
  className,
}: {
  open: boolean;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        'grid transition-[grid-template-rows] duration-300 ease-in-out',
        open ? 'grid-rows-[1fr]' : 'grid-rows-[0fr]',
        className
      )}
    >
      <div className="min-h-0 overflow-hidden">{children}</div>
    </div>
  );
}

export default function ModelManager() {
  const { t } = useTranslation();

  // data state
  const [modelKeys, setModelKeys] = useState<GetModelKeysResult[]>([]);
  const [models, setModels] = useState<AdminModelDto[]>([]);
  const [loading, setLoading] = useState(true);

  // UI state
  const [expandProviders, setExpandProviders] = useState<Record<number, boolean>>({});
  const [expandKeys, setExpandKeys] = useState<Record<number, boolean>>({});
  // show all providers (otherwise hide providers whose model count is 0)
  const [showAllProviders, setShowAllProviders] = useState(false);

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
      // default: collapse everything -> only show provider level
      const providerExpand: Record<number, boolean> = {};
      const keyExpand: Record<number, boolean> = {};
      for (const p of feModelProviders) providerExpand[p.id] = false;
      // default: do NOT expand models list under keys
      for (const k of keys) keyExpand[k.id] = false;
      setExpandProviders(providerExpand);
      setExpandKeys(keyExpand);
    }
    setLoading(false);
  };

  const grouped: ProviderGroup[] = useMemo(() => {
    const groups: ProviderGroup[] = feModelProviders.map((p) => ({
      providerId: p.id,
      providerName: p.name,
      keys: [],
    }));
    for (const k of modelKeys) {
      const g = groups.find((x) => x.providerId === k.modelProviderId);
      if (g) g.keys.push(k);
    }
    return groups;
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

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-6">
          <div className="flex items-center gap-2">
            <Switch
              checked={showAllProviders}
              onCheckedChange={(v) => setShowAllProviders(!!v)}
            />
            <span>{t('Show all providers')}</span>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="secondary"
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

      <div className="p-2">
        {/* Providers level (always grouped) */}
        <div className="space-y-2">
            {grouped
              .filter((g) => showAllProviders || g.keys.length > 0)
              .map((g) => (
              <div key={g.providerId} className="mb-3">
                <div className="rounded-xl border bg-card">
                  <div
                    className="flex items-center justify-between p-3 cursor-pointer select-none"
                    onClick={() => {
                      const isCurrentlyExpanded = expandProviders[g.providerId];
                      // 收缩所有提供商
                      const newExpandProviders: Record<number, boolean> = {};
                      for (const provider of feModelProviders) {
                        newExpandProviders[provider.id] = false;
                      }
                      // 如果当前没有展开，则展开当前提供商
                      if (!isCurrentlyExpanded) {
                        newExpandProviders[g.providerId] = true;
                        // 同时展开第一个密钥（如果存在）
                        const newExpandKeys: Record<number, boolean> = {};
                        for (const k of modelKeys) {
                          newExpandKeys[k.id] = false;
                        }
                        if (g.keys.length > 0) {
                          newExpandKeys[g.keys[0].id] = true;
                        }
                        setExpandKeys(newExpandKeys);
                      } else {
                        // 如果当前已展开，则收缩所有密钥
                        const newExpandKeys: Record<number, boolean> = {};
                        for (const k of modelKeys) {
                          newExpandKeys[k.id] = false;
                        }
                        setExpandKeys(newExpandKeys);
                      }
                      setExpandProviders(newExpandProviders);
                    }}
                  >
                    <div className="flex items-center gap-2">
                      <ChatIcon className="h-6 w-6" providerId={g.providerId} />
                      <span className="font-semibold">{t(g.providerName)}</span>
                      <span className="text-muted-foreground text-sm">{t('Model Keys')}: {g.keys.length}  {t('Models')}: {modelCountByProvider[g.providerId] || 0}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={(e) => {
                          e.stopPropagation();
                          openAddKey(g.providerId);
                        }}
                        title={t('Add Model Key')}
                      >
                        <IconPlus size={16} />
                      </Button>
                    </div>
                  </div>
                  <CollapsiblePanel open={!!expandProviders[g.providerId]} className="mt-1">
                    {/* keys under provider (tree view) */}
                    {g.keys.length === 0 ? (
                      <div className="text-sm text-muted-foreground px-3 pb-3">{t('No Model Keys')}</div>
                    ) : (
                      <div className="pl-6 space-y-2 pb-3">
                        {g.keys.map((k, keyIndex) => (
                          <div key={k.id} className="rounded-md border bg-background/30">
                            <div
                              className="flex items-center justify-between p-3 cursor-pointer select-none"
                              onClick={(e) => {
                                e.stopPropagation();
                                // 收缩当前提供商下的所有其他密钥
                                const newExpandKeys: Record<number, boolean> = {};
                                for (const key of modelKeys) {
                                  newExpandKeys[key.id] = false;
                                }
                                // 切换当前密钥的展开状态
                                newExpandKeys[k.id] = !expandKeys[k.id];
                                setExpandKeys(newExpandKeys);
                              }}
                            >
                              <div className="flex items-center gap-2">
                                <span className="font-medium">{k.name}</span>
                              </div>
                              <div className="flex gap-2">
                                <Button
                                  variant="secondary"
                                  size="sm"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    openConfigModels(k.id, g.providerId);
                                  }}
                                  title={t('Fast Add Models')}
                                >
                                  <IconBolt size={16} />
                                </Button>
                                <Button variant="secondary"
                                  size="sm"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    openAddModel(k.id);
                                  }}
                                  title={t('Add Model')}
                                >
                                  <IconPlus size={16} />
                                </Button>
                                <Button
                                  variant="secondary"
                                  size="sm"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    // Open edit modal for model key
                                    setCurrentProviderId(g.providerId);
                                    const selected = modelKeys.find(mk => mk.id === k.id);
                                    if (selected) {
                                      setSelectedKey(selected);
                                      setIsOpenKeyModal(true);
                                    }
                                  }}
                                  title={t('Edit')}
                                >
                                  <IconPencil size={16} />
                                </Button>
                                {(modelsByKey[k.id] || []).length === 0 && (
                                  <div onClick={(e) => e.stopPropagation()} title={t('Delete')}>
                                    <DeletePopover 
                                      onDelete={() => handleDeleteKey(k.id)}
                                    />
                                  </div>
                                )}
                              </div>
                            </div>
                            <CollapsiblePanel open={!!expandKeys[k.id]}>
                              {/* models under key (tree view) */}
                              <div className="pr-3 pb-3 pl-4 space-y-1">
                                {(modelsByKey[k.id] || []).map((m, modelIndex) => (
                                  <div key={m.modelId} className="flex items-center justify-between px-2 py-1 rounded hover:bg-muted/40">
                                    <div className="flex-1 min-w-0 cursor-pointer" onClick={() => openEditModel(m)}>
                                      <div className="truncate">{m.name}</div>
                                      <div className="text-xs text-blue-600 truncate">
                                        {'￥' + formatNumberAsMoney(m.inputTokenPrice1M) + '/' + formatNumberAsMoney(m.outputTokenPrice1M)}
                                      </div>
                                    </div>
                                    <div className="flex gap-2 ml-3">
                                      <Button variant="secondary" size="sm" onClick={() => openEditModel(m)} title={t('Edit Model')}>
                                        <IconPencil size={16} />
                                      </Button>
                                      <div title={t('Delete Model')}>
                                        <DeletePopover 
                                          onDelete={() => handleDeleteModel(m.modelId)}
                                        />
                                      </div>
                                    </div>
                                  </div>
                                ))}
                              </div>
                            </CollapsiblePanel>
                          </div>
                        ))}
                      </div>
                    )}
                  </CollapsiblePanel>
                </div>
              </div>
            ))}
          </div>
      </div>

      {/* dialogs */}
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
