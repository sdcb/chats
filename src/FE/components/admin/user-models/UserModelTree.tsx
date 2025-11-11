import React, { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';
import { 
  UserModelPermissionUserDto, 
  UserModelProviderDto, 
  UserModelKeyDto, 
  UserModelPermissionModelDto 
} from '@/types/adminApis';
import { feModelProviders } from '@/types/model';
import { 
  getModelProvidersForUser,
  getModelKeysByProviderForUser,
  getModelsByKeyForUser,
  addUserModel,
  deleteUserModel,
  batchAddUserModelsByProvider,
  batchDeleteUserModelsByProvider,
  batchAddUserModelsByKey,
  batchDeleteUserModelsByKey,
  editUserModel,
} from '@/apis/adminApis';
import { Badge } from '@/components/ui/badge';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { IconChevronDown, IconChevronRight, IconLoader, IconPencil } from '@/components/Icons';
import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import EditUserModelDialog from './EditUserModelDialog';

interface IProps {
  user: UserModelPermissionUserDto;
  isExpanded: boolean;
  onToggle: () => void;
  onUserModelCountChange?: (userId: string, modelCount: number) => void;
}

export default function UserModelTree({ user, isExpanded, onToggle, onUserModelCountChange }: IProps) {
  const { t } = useTranslation();
  const userId = user.id.toString();
  
  const [loading, setLoading] = useState(false);
  const [providers, setProviders] = useState<UserModelProviderDto[]>([]);
  const [expandedProviders, setExpandedProviders] = useState<Set<number>>(new Set());
  const [expandedKeys, setExpandedKeys] = useState<Set<number>>(new Set());
  const [keysByProvider, setKeysByProvider] = useState<Record<number, UserModelKeyDto[]>>({});
  const [modelsByKey, setModelsByKey] = useState<Record<number, UserModelPermissionModelDto[]>>({});
  const [pendingModels, setPendingModels] = useState<Set<number>>(new Set());
  const [batchPendingProviders, setBatchPendingProviders] = useState<Set<number>>(new Set());
  const [batchPendingKeys, setBatchPendingKeys] = useState<Set<number>>(new Set());
  const [loadingProviders, setLoadingProviders] = useState<Set<number>>(new Set());
  const [loadingKeys, setLoadingKeys] = useState<Set<number>>(new Set());
  const [editingModel, setEditingModel] = useState<UserModelPermissionModelDto | null>(null);
  const [editDialogOpen, setEditDialogOpen] = useState(false);

  useEffect(() => {
    if (isExpanded && providers.length === 0) {
      loadProviders();
    }
  }, [isExpanded]);

  const loadProviders = async () => {
    try {
      setLoading(true);
      const data = await getModelProvidersForUser(userId);
      setProviders(data);
    } catch (error) {
      console.error('Failed to load providers:', error);
      toast.error(t('Failed to load data'));
    } finally {
      setLoading(false);
    }
  };

  const loadProviderKeys = async (
    providerId: number,
    options?: { force?: boolean }
  ): Promise<UserModelKeyDto[] | undefined> => {
    if (loadingProviders.has(providerId)) {
      return keysByProvider[providerId];
    }

    if (!options?.force && keysByProvider[providerId]) {
      return keysByProvider[providerId];
    }

    if (options?.force) {
      setKeysByProvider(prev => {
        const next = { ...prev };
        delete next[providerId];
        return next;
      });
    }

    setLoadingProviders(prev => {
      const next = new Set(prev);
      next.add(providerId);
      return next;
    });

    try {
      const keys = await getModelKeysByProviderForUser(userId, providerId);
      setKeysByProvider(prev => ({ ...prev, [providerId]: keys }));
      return keys;
    } catch (error) {
      console.error('Failed to load keys:', error);
      toast.error(t('Failed to load keys'));
      return undefined;
    } finally {
      setLoadingProviders(prev => {
        const next = new Set(prev);
        next.delete(providerId);
        return next;
      });
    }
  };

  const loadKeyModels = async (
    keyId: number,
    options?: { force?: boolean }
  ): Promise<UserModelPermissionModelDto[] | undefined> => {
    if (loadingKeys.has(keyId)) {
      return modelsByKey[keyId];
    }

    if (!options?.force && modelsByKey[keyId]) {
      return modelsByKey[keyId];
    }

    if (options?.force) {
      setModelsByKey(prev => {
        const next = { ...prev };
        delete next[keyId];
        return next;
      });
    }

    setLoadingKeys(prev => {
      const next = new Set(prev);
      next.add(keyId);
      return next;
    });

    try {
      const models = await getModelsByKeyForUser(userId, keyId);
      setModelsByKey(prev => ({ ...prev, [keyId]: models }));
      return models;
    } catch (error) {
      console.error('Failed to load models:', error);
      toast.error(t('Failed to load models'));
      return undefined;
    } finally {
      setLoadingKeys(prev => {
        const next = new Set(prev);
        next.delete(keyId);
        return next;
      });
    }
  };

  const handleToggleProvider = (providerId: number) => {
    setExpandedProviders(prev => {
      const next = new Set(prev);
      if (next.has(providerId)) {
        next.delete(providerId);
      } else {
        next.add(providerId);
        if (!keysByProvider[providerId]) {
          void loadProviderKeys(providerId);
        }
      }
      return next;
    });
  };

  const handleToggleKey = (keyId: number) => {
    setExpandedKeys(prev => {
      const next = new Set(prev);
      if (next.has(keyId)) {
        next.delete(keyId);
      } else {
        next.add(keyId);
        if (!modelsByKey[keyId]) {
          void loadKeyModels(keyId);
        }
      }
      return next;
    });
  };

  // 添加单个模型
  const handleAddModel = async (model: UserModelPermissionModelDto, keyId: number) => {
    if (pendingModels.has(model.modelId) || model.isAssigned) return;

    setPendingModels(prev => new Set(prev).add(model.modelId));

    try {
      const expiresAt = new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString();
      const result = await addUserModel({
        userId: parseInt(userId, 10),
        modelId: model.modelId,
        tokens: 0,
        counts: 0,
        expires: expiresAt,
      });

      // 使用后端返回的统计信息更新本地状态
      if (result.model) {
        setModelsByKey(prev => ({
          ...prev,
          [keyId]: prev[keyId]?.map(m =>
            m.modelId === result.model!.modelId ? result.model! : m
          ) || []
        }));
      }

      if (result.keyStats) {
        setKeysByProvider(prev => {
          const providerId = Object.keys(prev).find(pId =>
            prev[parseInt(pId)].some(k => k.id === keyId)
          );
          if (!providerId) return prev;
          return {
            ...prev,
            [providerId]: prev[parseInt(providerId)].map(k =>
              k.id === keyId ? result.keyStats! : k
            ),
          };
        });
      }

      if (result.providerStats) {
        setProviders(prev =>
          prev.map(p =>
            p.providerId === result.providerStats!.providerId ? result.providerStats! : p
          )
        );
      }

      onUserModelCountChange?.(userId, result.userModelCount);
    } catch (error) {
      console.error('Failed to add model:', error);
      toast.error(t('Failed to add model'));
    } finally {
      setPendingModels(prev => {
        const next = new Set(prev);
        next.delete(model.modelId);
        return next;
      });
    }
  };

  // 删除单个模型
  const handleDeleteModel = async (model: UserModelPermissionModelDto, keyId: number) => {
    if (pendingModels.has(model.modelId) || !model.isAssigned || !model.userModelId) return;

    setPendingModels(prev => new Set(prev).add(model.modelId));

    const userModelId = model.userModelId;

    try {
      const result = await deleteUserModel(userModelId);

      // 使用后端返回的统计信息更新本地状态
      if (result.model) {
        setModelsByKey(prev => ({
          ...prev,
          [keyId]: prev[keyId]?.map(m =>
            m.modelId === result.model!.modelId ? result.model! : m
          ) || []
        }));
      }

      if (result.keyStats) {
        setKeysByProvider(prev => {
          const providerId = Object.keys(prev).find(pId =>
            prev[parseInt(pId)].some(k => k.id === keyId)
          );
          if (!providerId) return prev;
          return {
            ...prev,
            [providerId]: prev[parseInt(providerId)].map(k =>
              k.id === keyId ? result.keyStats! : k
            ),
          };
        });
      }

      if (result.providerStats) {
        setProviders(prev =>
          prev.map(p =>
            p.providerId === result.providerStats!.providerId ? result.providerStats! : p
          )
        );
      }

      onUserModelCountChange?.(userId, result.userModelCount);
    } catch (error) {
      console.error('Failed to delete model:', error);
      toast.error(t('Failed to remove model'));
    } finally {
      setPendingModels(prev => {
        const next = new Set(prev);
        next.delete(model.modelId);
        return next;
      });
    }
  };

  // 批量操作：Provider级别
  const handleBatchToggleProvider = async (providerId: number) => {
    const provider = providers.find(p => p.providerId === providerId);
    if (!provider) return;

    const isFullyAssigned = provider.assignedModelCount === provider.modelCount;

    setBatchPendingProviders(prev => new Set(prev).add(providerId));

    try {
      const result = isFullyAssigned
        ? await batchDeleteUserModelsByProvider({
            userId: parseInt(userId, 10),
            providerId,
          })
        : await batchAddUserModelsByProvider({
            userId: parseInt(userId, 10),
            providerId,
          });

      // 使用后端返回的统计信息更新状态
      if (result.providerStats) {
        setProviders(prev =>
          prev.map(p =>
            p.providerId === result.providerStats!.providerId ? result.providerStats! : p
          )
        );
      }

      onUserModelCountChange?.(userId, result.userModelCount);

      // 如果provider已展开，重新加载keys和models
      if (expandedProviders.has(providerId)) {
        await loadProviderKeys(providerId, { force: true });
        const keys = keysByProvider[providerId] || [];
        for (const key of keys) {
          if (expandedKeys.has(key.id)) {
            await loadKeyModels(key.id, { force: true });
          }
        }
      }

    } catch (error) {
      console.error('Batch operation failed:', error);
      toast.error(isFullyAssigned ? t('Failed to remove models') : t('Failed to add models'));
    } finally {
      setBatchPendingProviders(prev => {
        const next = new Set(prev);
        next.delete(providerId);
        return next;
      });
    }
  };

  // 批量操作：Key级别
  const handleBatchToggleKey = async (keyId: number) => {
    // 找到key和它所属的provider
    let key: UserModelKeyDto | undefined;
    let providerId: number | undefined;
    for (const [pId, keys] of Object.entries(keysByProvider)) {
      const found = keys.find(k => k.id === keyId);
      if (found) {
        key = found;
        providerId = parseInt(pId);
        break;
      }
    }
    
    if (!key || providerId === undefined) return;

    const isFullyAssigned = key.assignedModelCount === key.modelCount;

    setBatchPendingKeys(prev => new Set(prev).add(keyId));

    try {
      const result = isFullyAssigned
        ? await batchDeleteUserModelsByKey({
            userId: parseInt(userId, 10),
            keyId,
          })
        : await batchAddUserModelsByKey({
            userId: parseInt(userId, 10),
            keyId,
          });

      // 使用后端返回的统计信息更新状态
      if (result.providerStats) {
        setProviders(prev =>
          prev.map(p =>
            p.providerId === result.providerStats!.providerId ? result.providerStats! : p
          )
        );
      }

      if (result.keyStats) {
        setKeysByProvider(prev => {
          if (!providerId) return prev;
          return {
            ...prev,
            [providerId]: prev[providerId].map(k =>
              k.id === keyId ? result.keyStats! : k
            ),
          };
        });
      }

      onUserModelCountChange?.(userId, result.userModelCount);

      // 如果key已展开，重新加载models
      if (expandedKeys.has(keyId)) {
        await loadKeyModels(keyId, { force: true });
      }

    } catch (error) {
      console.error('Batch operation failed:', error);
      toast.error(isFullyAssigned ? t('Failed to remove models') : t('Failed to add models'));
    } finally {
      setBatchPendingKeys(prev => {
        const next = new Set(prev);
        next.delete(keyId);
        return next;
      });
    }
  };

  const getProviderCheckState = (provider: UserModelProviderDto): 'checked' | 'unchecked' | 'indeterminate' => {
    if (provider.modelCount === 0) return 'unchecked';
    if (provider.assignedModelCount === 0) return 'unchecked';
    if (provider.assignedModelCount === provider.modelCount) return 'checked';
    return 'indeterminate';
  };

  const getKeyCheckState = (key: UserModelKeyDto): 'checked' | 'unchecked' | 'indeterminate' => {
    if (key.modelCount === 0) return 'unchecked';
    if (key.assignedModelCount === 0) return 'unchecked';
    if (key.assignedModelCount === key.modelCount) return 'checked';
    return 'indeterminate';
  };

  // 计算过期时间显示
  const getExpiresDisplay = (expires: string | null | undefined) => {
    if (!expires) return { 
      text: t('No expiration'), 
      shortText: '-', 
      color: 'text-muted-foreground', 
      isExpired: false 
    };
    
    const expireDate = new Date(expires);
    const now = new Date();
    const diffMs = expireDate.getTime() - now.getTime();
    const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays < 0) {
      return { 
        text: t('Expired'), 
        shortText: t('Expired'), 
        color: 'text-red-500', 
        isExpired: true 
      };
    } else if (diffDays === 0) {
      return { 
        text: t('Expires today'), 
        shortText: '0d', 
        color: 'text-orange-500', 
        isExpired: false 
      };
    } else if (diffDays === 1) {
      return { 
        text: t('1 day left'), 
        shortText: '1d', 
        color: 'text-orange-500', 
        isExpired: false 
      };
    } else {
      return { 
        text: `${diffDays} ${t('days left')}`, 
        shortText: `${diffDays}d`, 
        color: 'text-muted-foreground', 
        isExpired: false 
      };
    }
  };

  // 格式化过期时间用于tooltip
  const formatExpiresForTooltip = (expires: string | null | undefined) => {
    if (!expires) return '';
    return new Date(expires).toLocaleString();
  };

  // 编辑模型
  const handleEditModel = (model: UserModelPermissionModelDto) => {
    setEditingModel(model);
    setEditDialogOpen(true);
  };

  // 保存编辑
  const handleSaveEdit = async (tokensDelta: number, countsDelta: number, expires: string) => {
    if (!editingModel || !editingModel.userModelId) return;

    try {
      const result = await editUserModel(editingModel.userModelId, {
        tokensDelta,
        countsDelta,
        expires,
      });

      // 使用后端返回的统计信息更新本地状态
      if (result.model) {
        // 找到并更新模型所在的key
        setModelsByKey(prev => {
          const newState = { ...prev };
          for (const [keyId, models] of Object.entries(newState)) {
            const modelIndex = models.findIndex(m => m.modelId === result.model!.modelId);
            if (modelIndex !== -1) {
              newState[parseInt(keyId)] = models.map(m =>
                m.modelId === result.model!.modelId ? result.model! : m
              );
              break;
            }
          }
          return newState;
        });
      }

      if (result.keyStats) {
        setKeysByProvider(prev => {
          const providerId = Object.keys(prev).find(pId =>
            prev[parseInt(pId)].some(k => k.id === result.keyStats!.id)
          );
          if (!providerId) return prev;
          return {
            ...prev,
            [providerId]: prev[parseInt(providerId)].map(k =>
              k.id === result.keyStats!.id ? result.keyStats! : k
            ),
          };
        });
      }

      if (result.providerStats) {
        setProviders(prev =>
          prev.map(p =>
            p.providerId === result.providerStats!.providerId ? result.providerStats! : p
          )
        );
      }

      onUserModelCountChange?.(userId, result.userModelCount);
    } catch (error) {
      console.error('Failed to update model:', error);
      toast.error(t('Failed to update model'));
      throw error;
    }
  };

  const userModelCount = user.userModelCount;

  return (
    <div className="border-b last:border-0">
      <div
        className="flex items-center justify-between p-4 hover:bg-muted/50 cursor-pointer"
        onClick={onToggle}
      >
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <div className={cn("transition-transform duration-200", isExpanded && "rotate-0", !isExpanded && "-rotate-90")}>
              <IconChevronDown size={16} />
            </div>
            <div className={`w-2 h-2 rounded-full ${user.enabled ? 'bg-green-400' : 'bg-gray-400'}`} />
          </div>
          <div>
            <div className="font-medium">{user.username}</div>
            <div className="text-sm text-muted-foreground">{user.email || user.phone}</div>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant="secondary">
            {t('{{count}} models', { count: userModelCount })}
          </Badge>
        </div>
      </div>

      <div 
        className={cn(
          "grid transition-all duration-300 ease-in-out",
          isExpanded ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
        )}
      >
        <div className="overflow-hidden">
          <div className="pl-8 pr-4 pb-4">
          {loading ? (
            <div className="space-y-1">
              {Array.from({ length: user.modelProviderCount || 2 }).map((_, idx) => (
                <div key={`skeleton-provider-${idx}`} className="border rounded-lg">
                  <div className="flex items-center p-3">
                    <div className="flex items-center gap-2 flex-1">
                      <Skeleton className="h-3.5 w-3.5 rounded" />
                      <Skeleton className="h-5 w-5 rounded-full" />
                      <Skeleton className="h-5 w-5 rounded" />
                      <Skeleton className="h-4 w-24" />
                      <Skeleton className="h-5 w-32 rounded-full" />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : providers.length === 0 ? (
            <div className="text-sm text-muted-foreground text-center py-4">
              {t('No models available')}
            </div>
          ) : (
            <div className="space-y-1">
              {providers.map(provider => {
                const feProvider = feModelProviders.find(fp => fp.id === provider.providerId);
                const isProviderExpanded = expandedProviders.has(provider.providerId);
                const keys = keysByProvider[provider.providerId] || [];
                const providerCheckState = getProviderCheckState(provider);
                const isProviderPending = batchPendingProviders.has(provider.providerId);

                return (
                  <div key={provider.providerId} className="border rounded-lg">
                    <div className="flex items-center p-3 hover:bg-muted/50">
                      <div 
                        className="flex items-center gap-2 flex-1 cursor-pointer"
                        onClick={() => !isProviderPending && handleToggleProvider(provider.providerId)}
                      >
                        <div className={cn("transition-transform duration-200", isProviderExpanded && "rotate-0", !isProviderExpanded && "-rotate-90")}>
                          <IconChevronDown size={14} />
                        </div>
                        <button
                          type="button"
                          className="flex items-center justify-center"
                          onClick={(e) => {
                            e.stopPropagation();
                            if (!isProviderPending) {
                              handleBatchToggleProvider(provider.providerId);
                            }
                          }}
                          disabled={isProviderPending}
                        >
                          {isProviderPending ? (
                            <IconLoader size={20} className="animate-spin text-muted-foreground" />
                          ) : providerCheckState === 'checked' ? (
                            <div className="w-5 h-5 rounded-full bg-green-500 flex items-center justify-center hover:bg-green-600 transition-colors">
                              <span className="text-white text-xs">✓</span>
                            </div>
                          ) : providerCheckState === 'indeterminate' ? (
                            <div className="w-5 h-5 rounded-full bg-blue-500 flex items-center justify-center hover:bg-blue-600 transition-colors">
                              <span className="text-white text-xs">−</span>
                            </div>
                          ) : (
                            <div className="w-5 h-5 rounded-full border-2 border-muted-foreground/30 hover:border-primary transition-colors" />
                          )}
                        </button>
                        <ModelProviderIcon providerId={provider.providerId} className="w-5 h-5" />
                        <span className="text-sm font-medium">
                          {feProvider ? t(feProvider.name) : `Provider ${provider.providerId}`}
                        </span>
                        <Badge variant="outline" className="text-xs">
                          {t('{{keyCount}} keys, {{assigned}}/{{total}} models', { 
                            keyCount: provider.keyCount, 
                            assigned: provider.assignedModelCount, 
                            total: provider.modelCount 
                          })}
                        </Badge>
                      </div>
                    </div>

                    <div 
                      className={cn(
                        "grid transition-all duration-300 ease-in-out",
                        isProviderExpanded ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
                      )}
                    >
                      <div className="overflow-hidden">
                        <div className="pl-6 pr-3 pb-2 space-y-1">
                        {loadingProviders.has(provider.providerId) ? (
                          // 骨架屏：根据provider.keyCount显示对应数量
                          Array.from({ length: provider.keyCount }).map((_, idx) => (
                            <div key={`skeleton-key-${idx}`} className="border-l-2 border-muted pl-3">
                              <div className="flex items-center p-2 rounded">
                                <div className="flex items-center gap-2 flex-1">
                                  <Skeleton className="h-3 w-3 rounded" />
                                  <Skeleton className="h-4 w-4 rounded-full" />
                                  <Skeleton className="h-3 w-20" />
                                  <Skeleton className="h-4 w-24 rounded-full" />
                                </div>
                              </div>
                            </div>
                          ))
                        ) : (
                          keys.map(key => {
                            const isKeyExpanded = expandedKeys.has(key.id);
                            const models = modelsByKey[key.id] || [];
                            const keyCheckState = getKeyCheckState(key);
                            const isKeyPending = batchPendingKeys.has(key.id);

                            return (
                              <div key={key.id} className="border-l-2 border-muted pl-3">
                              <div className="flex items-center p-2 hover:bg-muted/30 rounded">
                                <div 
                                  className="flex items-center gap-2 flex-1 cursor-pointer"
                                  onClick={() => !isKeyPending && !isProviderPending && handleToggleKey(key.id)}
                                >
                                  <div className={cn("transition-transform duration-200", isKeyExpanded && "rotate-0", !isKeyExpanded && "-rotate-90")}>
                                    <IconChevronDown size={12} />
                                  </div>
                                  <button
                                    type="button"
                                    className="flex items-center justify-center"
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      if (!isKeyPending && !isProviderPending) {
                                        handleBatchToggleKey(key.id);
                                      }
                                    }}
                                    disabled={isKeyPending || isProviderPending}
                                  >
                                    {isKeyPending ? (
                                      <IconLoader size={16} className="animate-spin text-muted-foreground" />
                                    ) : keyCheckState === 'checked' ? (
                                      <div className="w-4 h-4 rounded-full bg-green-500 flex items-center justify-center hover:bg-green-600 transition-colors">
                                        <span className="text-white text-[10px]">✓</span>
                                      </div>
                                    ) : keyCheckState === 'indeterminate' ? (
                                      <div className="w-4 h-4 rounded-full bg-blue-500 flex items-center justify-center hover:bg-blue-600 transition-colors">
                                        <span className="text-white text-[10px]">−</span>
                                      </div>
                                    ) : (
                                      <div className="w-4 h-4 rounded-full border-2 border-muted-foreground/30 hover:border-primary transition-colors" />
                                    )}
                                  </button>
                                  <span className="text-xs font-medium">{key.name}</span>
                                  <Badge variant="outline" className="text-xs">
                                    {t('{{assigned}}/{{total}} models', { 
                                      assigned: key.assignedModelCount, 
                                      total: key.modelCount 
                                    })}
                                  </Badge>
                                </div>
                              </div>

                              <div 
                                className={cn(
                                  "grid transition-all duration-300 ease-in-out",
                                  isKeyExpanded ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
                                )}
                              >
                                <div className="overflow-hidden">
                                  <div className="pl-4 space-y-1 mt-1">
                                  {loadingKeys.has(key.id) ? (
                                    // 骨架屏：根据key.modelCount显示对应数量
                                    Array.from({ length: key.modelCount }).map((_, idx) => (
                                      <div key={`skeleton-model-${idx}`} className="flex items-center justify-between p-2 rounded bg-muted/30">
                                        <div className="flex items-center gap-2">
                                          <Skeleton className="h-4 w-4 rounded-full" />
                                          <Skeleton className="h-3 w-32" />
                                        </div>
                                      </div>
                                    ))
                                  ) : (
                                    models.map(model => {
                                      const isPending = pendingModels.has(model.modelId);
                                      const isDisabled = isPending || isKeyPending || isProviderPending;
                                      const expiresDisplay = getExpiresDisplay(model.expires);

                                      return (
                                        <div
                                          key={model.modelId}
                                        className={cn(
                                          "flex items-center justify-between p-2 rounded text-xs transition-colors",
                                          model.isDeleted && "bg-red-50 dark:bg-red-950/20",
                                          !model.isDeleted && model.isAssigned && "bg-green-50 dark:bg-green-950/20",
                                          !model.isDeleted && !model.isAssigned && "bg-muted/30",
                                          isDisabled && "opacity-50"
                                        )}
                                      >
                                        <div className="flex items-center gap-2 flex-1 min-w-0">
                                          {isPending ? (
                                            <IconLoader size={14} className="animate-spin text-muted-foreground" />
                                          ) : model.isAssigned ? (
                                            <button
                                              type="button"
                                              className="flex items-center justify-center flex-shrink-0"
                                              onClick={(e) => {
                                                e.stopPropagation();
                                                if (!isDisabled) {
                                                  handleDeleteModel(model, key.id);
                                                }
                                              }}
                                              disabled={isDisabled}
                                            >
                                              <div className="w-4 h-4 rounded-full bg-green-500 flex items-center justify-center hover:bg-green-600 transition-colors">
                                                <span className="text-white text-xs">✓</span>
                                              </div>
                                              <span className="sr-only">{t('Remove Model')}</span>
                                            </button>
                                          ) : (
                                            <button
                                              type="button"
                                              className="flex items-center justify-center flex-shrink-0"
                                              onClick={(e) => {
                                                e.stopPropagation();
                                                if (!isDisabled) {
                                                  handleAddModel(model, key.id);
                                                }
                                              }}
                                              disabled={isDisabled}
                                            >
                                              <div className="w-4 h-4 rounded-full border-2 border-muted-foreground/30 hover:border-primary transition-colors" />
                                              <span className="sr-only">{t('Add Model')}</span>
                                            </button>
                                          )}
                                          <div className="flex items-center gap-2 flex-1 min-w-0">
                                            <span className={cn(
                                              "truncate",
                                              model.isAssigned ? "font-medium" : "text-muted-foreground"
                                            )}>
                                              {model.name}
                                            </span>
                                            {model.isDeleted && (
                                              <Badge variant="destructive" className="text-[10px] py-0 px-1 h-4 flex-shrink-0">
                                                {t('Disabled')}
                                              </Badge>
                                            )}
                                          </div>
                                        </div>
                                        {model.isAssigned && (
                                          <div className="flex items-center gap-2 sm:gap-3 flex-shrink-0">
                                            <TooltipProvider>
                                              <Tooltip>
                                                <TooltipTrigger asChild>
                                                  <span className="text-muted-foreground whitespace-nowrap text-[11px]">
                                                    {/* 移动端显示简洁格式 */}
                                                    <span className="sm:hidden">
                                                      {model.counts}/{model.tokens} {expiresDisplay.shortText}
                                                    </span>
                                                    {/* 桌面端显示详细格式 */}
                                                    <span className="hidden sm:inline">
                                                      {t('{{counts}} counts, {{tokens}} tokens', { counts: model.counts, tokens: model.tokens })}, <span className={expiresDisplay.color}>{expiresDisplay.text}</span>
                                                    </span>
                                                  </span>
                                                </TooltipTrigger>
                                                <TooltipContent>
                                                  <div className="space-y-1">
                                                    <p>{t('Counts')}: {model.counts}</p>
                                                    <p>{t('Tokens')}: {model.tokens}</p>
                                                    {model.expires && <p>{t('Expires')}: {formatExpiresForTooltip(model.expires)}</p>}
                                                  </div>
                                                </TooltipContent>
                                              </Tooltip>
                                            </TooltipProvider>
                                            <button
                                              type="button"
                                              className="flex items-center justify-center p-1 hover:bg-muted rounded transition-colors"
                                              onClick={(e) => {
                                                e.stopPropagation();
                                                if (!isDisabled) {
                                                  handleEditModel(model);
                                                }
                                              }}
                                              disabled={isDisabled}
                                            >
                                              <IconPencil size={14} className="text-muted-foreground hover:text-foreground" />
                                              <span className="sr-only">{t('Edit')}</span>
                                            </button>
                                          </div>
                                        )}
                                      </div>
                                    );
                                  })
                                  )}
                                  </div>
                                </div>
                              </div>
                            </div>
                          );
                        })
                        )}
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
          </div>
        </div>
      </div>

      {editingModel && (
        <EditUserModelDialog
          open={editDialogOpen}
          onOpenChange={setEditDialogOpen}
          model={editingModel}
          onSave={handleSaveEdit}
        />
      )}
    </div>
  );
}
