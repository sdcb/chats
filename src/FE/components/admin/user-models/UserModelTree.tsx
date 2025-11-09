import React, { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';
import { GetUsersResult, ModelProviderDto, GetModelKeysResult, AdminModelDto, UserModelDisplay } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';
import { 
  getModelProviders, 
  getModelKeysByProvider, 
  getModelsByKey,
  getModelsByUserId,
  addUserModel,
  deleteUserModel,
} from '@/apis/adminApis';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { IconChevronDown, IconChevronRight, IconLoader } from '@/components/Icons';
import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

interface IProps {
  user: GetUsersResult;
  isExpanded: boolean;
  onToggle: () => void;
  onUserModelCountChange?: (userId: string, modelCount: number) => void;
}

interface ProviderNode {
  providerId: number;
  providerName: string;
  isExpanded: boolean;
  keys: KeyNode[];
}

interface KeyNode {
  keyId: number;
  keyName: string;
  isExpanded: boolean;
  models: ModelNode[];
}

interface ModelNode {
  modelId: number;
  modelName: string;
  isAssigned: boolean;
  userModelId?: number;
  tokens?: number;
  counts?: number;
  expires?: string;
}

export default function UserModelTree({ user, isExpanded, onToggle, onUserModelCountChange }: IProps) {
  const { t } = useTranslation();
  const userId = user.id.toString();
  const [loading, setLoading] = useState(false);
  const [providers, setProviders] = useState<ModelProviderDto[]>([]);
  const [expandedProviders, setExpandedProviders] = useState<Set<number>>(new Set());
  const [expandedKeys, setExpandedKeys] = useState<Set<number>>(new Set());
  const [keysByProvider, setKeysByProvider] = useState<Record<number, GetModelKeysResult[]>>({});
  const [modelsByKey, setModelsByKey] = useState<Record<number, AdminModelDto[]>>({});
  const [userModels, setUserModels] = useState<UserModelDisplay[]>([]);
  const [pendingStates, setPendingStates] = useState<Record<number, 'adding' | 'removing'>>({});

  const applyUserModels = useCallback(
    (updater: (prev: UserModelDisplay[]) => UserModelDisplay[]) => {
      setUserModels((prev) => {
        const next = updater(prev);
        onUserModelCountChange?.(userId, next.length);
        return next;
      });
    },
    [onUserModelCountChange, userId],
  );

  useEffect(() => {
    if (isExpanded) {
      loadData();
    }
  }, [isExpanded]);

  const refreshUserModels = useCallback(async () => {
    const userModelsData = await getModelsByUserId(userId);
    setUserModels(userModelsData);
    onUserModelCountChange?.(userId, userModelsData.length);
  }, [onUserModelCountChange, userId]);

  const loadData = async () => {
    const shouldShowLoading = providers.length === 0 && userModels.length === 0;
    try {
      if (providers.length === 0) {
        if (shouldShowLoading) {
          setLoading(true);
        }
        const providersData = await getModelProviders();
        setProviders(providersData);
      } else if (shouldShowLoading) {
        setLoading(true);
      }
      await refreshUserModels();
    } catch (error) {
      console.error('Failed to load data:', error);
      toast.error(t('Failed to load data'));
    } finally {
      if (shouldShowLoading) {
        setLoading(false);
      }
    }
  };

  const loadProviderKeys = async (providerId: number) => {
    if (keysByProvider[providerId]) return;

    try {
      const keys = await getModelKeysByProvider(providerId);
      setKeysByProvider(prev => ({ ...prev, [providerId]: keys }));
    } catch (error) {
      console.error('Failed to load keys:', error);
      toast.error(t('Failed to load keys'));
    }
  };

  const loadKeyModels = async (keyId: number) => {
    if (modelsByKey[keyId]) return;

    try {
      const models = await getModelsByKey(keyId);
      setModelsByKey(prev => ({ ...prev, [keyId]: models }));
    } catch (error) {
      console.error('Failed to load models:', error);
      toast.error(t('Failed to load models'));
    }
  };

  const handleToggleProvider = async (providerId: number) => {
    const newExpanded = new Set(expandedProviders);
    if (newExpanded.has(providerId)) {
      newExpanded.delete(providerId);
      setExpandedProviders(newExpanded);
    } else {
      newExpanded.add(providerId);
      setExpandedProviders(newExpanded);
      await loadProviderKeys(providerId);
    }
  };

  const handleToggleKey = async (keyId: number) => {
    const newExpanded = new Set(expandedKeys);
    if (newExpanded.has(keyId)) {
      newExpanded.delete(keyId);
      setExpandedKeys(newExpanded);
    } else {
      newExpanded.add(keyId);
      setExpandedKeys(newExpanded);
      await loadKeyModels(keyId);
    }
  };

  const handleAddModel = async (model: AdminModelDto) => {
    const modelId = model.modelId;
    if (pendingStates[modelId]) {
      return;
    }

    const alreadyAssigned = userModels.some((item) => item.modelId === modelId);
    if (alreadyAssigned) {
      return;
    }

    setPendingStates((prev) => ({ ...prev, [modelId]: 'adding' }));

    const expiresAt = new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString();

    try {
      const createdUserModel = await addUserModel({
        userId: parseInt(userId, 10),
        modelId,
        tokens: 0,
        counts: 100,
        expires: expiresAt,
      });

      applyUserModels((prev) => {
        const filtered = prev.filter((item) => item.modelId !== createdUserModel.modelId);
        return [createdUserModel, ...filtered];
      });
    } catch (error) {
      console.error('Failed to add model:', error);
      toast.error(t('Failed to add model'));
    } finally {
      setPendingStates((prev) => {
        const { [modelId]: _, ...rest } = prev;
        return rest;
      });
    }
  };

  const handleDeleteModel = async (userModel: UserModelDisplay) => {
    const modelId = userModel.modelId;
    if (pendingStates[modelId]) {
      return;
    }

    setPendingStates((prev) => ({ ...prev, [modelId]: 'removing' }));

    try {
      await deleteUserModel(userModel.id);
      applyUserModels((prev) => prev.filter((item) => item.modelId !== modelId));
    } catch (error) {
      console.error('Failed to remove model:', error);
      toast.error(t('Failed to remove model'));
    } finally {
      setPendingStates((prev) => {
        const { [modelId]: _, ...rest } = prev;
        return rest;
      });
    }
  };

  const userModelCount = userModels.length;

  return (
    <div className="border-b last:border-0">
      <div
        className="flex items-center justify-between p-4 hover:bg-muted/50 cursor-pointer"
        onClick={onToggle}
      >
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            {isExpanded ? (
              <IconChevronDown size={16} />
            ) : (
              <IconChevronRight size={16} />
            )}
            <div className={`w-2 h-2 rounded-full ${user.enabled ? 'bg-green-400' : 'bg-gray-400'}`} />
          </div>
          <div>
            <div className="font-medium">{user.username}</div>
            <div className="text-sm text-muted-foreground">{user.email || user.phone}</div>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant="secondary">
            {userModelCount} {t('models')}
          </Badge>
        </div>
      </div>

      {isExpanded && (
        <div className="pl-8 pr-4 pb-4">
          {loading ? (
            <div className="space-y-2">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
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

                return (
                  <div key={provider.providerId} className="border rounded-lg">
                    <div
                      className="flex items-center justify-between p-3 hover:bg-muted/50 cursor-pointer"
                      onClick={() => handleToggleProvider(provider.providerId)}
                    >
                      <div className="flex items-center gap-2">
                        {isProviderExpanded ? <IconChevronDown size={14} /> : <IconChevronRight size={14} />}
                        <ModelProviderIcon providerId={provider.providerId} className="w-5 h-5" />
                        <span className="text-sm font-medium">
                          {feProvider?.name || `Provider ${provider.providerId}`}
                        </span>
                        <Badge variant="outline" className="text-xs">
                          {provider.keyCount} {t('keys')}, {provider.modelCount} {t('models')}
                        </Badge>
                      </div>
                    </div>

                    {isProviderExpanded && (
                      <div className="pl-6 pr-3 pb-2 space-y-1">
                        {keys.map(key => {
                          const isKeyExpanded = expandedKeys.has(key.id);
                          const models = modelsByKey[key.id] || [];

                          return (
                            <div key={key.id} className="border-l-2 border-muted pl-3">
                              <div
                                className="flex items-center justify-between p-2 hover:bg-muted/30 cursor-pointer rounded"
                                onClick={() => handleToggleKey(key.id)}
                              >
                                <div className="flex items-center gap-2">
                                  {isKeyExpanded ? <IconChevronDown size={12} /> : <IconChevronRight size={12} />}
                                  <span className="text-xs font-medium">{key.name}</span>
                                  <Badge variant="outline" className="text-xs">
                                    {key.totalModelCount} {t('models')}
                                  </Badge>
                                </div>
                              </div>

                              {isKeyExpanded && (
                                <div className="pl-4 space-y-1 mt-1">
                                  {models.map(model => {
                                    const userModel = userModels.find(um => um.modelId === model.modelId);
                                    const isAssigned = !!userModel;
                                    const pendingState = pendingStates[model.modelId];
                                    const isPending = !!pendingState;

                                    return (
                                      <div
                                        key={model.modelId}
                                        className={cn(
                                          "flex items-center justify-between p-2 rounded text-xs",
                                          isAssigned ? "bg-green-50 dark:bg-green-950/20" : "bg-muted/30"
                                        )}
                                      >
                                        <div className="flex items-center gap-2">
                                          {isPending ? (
                                            <IconLoader size={14} className="animate-spin text-muted-foreground" />
                                          ) : isAssigned ? (
                                            <button
                                              type="button"
                                              className="flex items-center justify-center"
                                              onClick={(e) => {
                                                e.stopPropagation();
                                                userModel && handleDeleteModel(userModel);
                                              }}
                                              disabled={isPending}
                                            >
                                              <div className="w-4 h-4 rounded-full bg-green-500 flex items-center justify-center hover:bg-green-600 transition-colors">
                                                <span className="text-white text-xs">✓</span>
                                              </div>
                                              <span className="sr-only">{t('Remove Model')}</span>
                                            </button>
                                          ) : (
                                            <button
                                              type="button"
                                              className="flex items-center justify-center"
                                              onClick={(e) => {
                                                e.stopPropagation();
                                                handleAddModel(model);
                                              }}
                                              disabled={isPending}
                                            >
                                              <div className="w-4 h-4 rounded-full border-2 border-muted-foreground/30 hover:border-primary transition-colors" />
                                              <span className="sr-only">{t('Add Model')}</span>
                                            </button>
                                          )}
                                          <span className={isAssigned ? "font-medium" : "text-muted-foreground"}>
                                            {model.name}
                                          </span>
                                          {isAssigned && userModel && (
                                            <span className="text-muted-foreground">
                                              {userModel.counts === -1 ? '∞' : userModel.counts} {t('counts')},
                                              {userModel.tokens === -1 ? ' ∞' : ` ${userModel.tokens}`} {t('tokens')}
                                            </span>
                                          )}
                                        </div>
                                      </div>
                                    );
                                  })}
                                </div>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
