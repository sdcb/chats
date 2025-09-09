import React, { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';
import { AdminModelDto, GetModelKeysResult } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';
import { Button } from '@/components/ui/button';
import { LabelSwitch } from '@/components/ui/label-switch';
import { IconPlus } from '@/components/Icons';
import { getModelKeys, getModels, deleteModelKeys, deleteModels } from '@/apis/adminApis';
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

  const filteredProviders = useMemo(() => {
    return grouped.filter((g) => showAllProviders || g.keys.length > 0);
  }, [grouped, showAllProviders]);

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
          filteredProviders.map((provider) => (
            <ModelProvider
              key={provider.providerId}
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
