import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';
import { AdminModelDto, ModelProviderDto, GetModelKeysResult } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';
import { 
  getModelProviders, 
  getModelKeysByProvider, 
  getModelsByKey 
} from '@/apis/adminApis';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import ModelUserList from './ModelUserList';

interface IProps {
  focusModelId?: number;
}

export default function ByModelTab({ focusModelId }: IProps) {
  const { t } = useTranslation();
  const [providers, setProviders] = useState<ModelProviderDto[]>([]);
  const [keysByProvider, setKeysByProvider] = useState<Record<number, GetModelKeysResult[]>>({});
  const [modelsByKey, setModelsByKey] = useState<Record<number, AdminModelDto[]>>({});
  const [loading, setLoading] = useState(true);
  const [selectedProvider, setSelectedProvider] = useState<number | 'all'>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [expandedModelId, setExpandedModelId] = useState<number | null>(focusModelId || null);

  useEffect(() => {
    init();
  }, []);

  useEffect(() => {
    if (focusModelId) {
      setExpandedModelId(focusModelId);
    }
  }, [focusModelId]);

  const init = async () => {
    try {
      setLoading(true);
      const providersData = await getModelProviders();
      setProviders(providersData);

      // 加载所有提供商的密钥和模型
      for (const provider of providersData) {
        const keys = await getModelKeysByProvider(provider.providerId);
        setKeysByProvider(prev => ({ ...prev, [provider.providerId]: keys }));

        for (const key of keys) {
          const models = await getModelsByKey(key.id);
          setModelsByKey(prev => ({ ...prev, [key.id]: models }));
        }
      }
    } catch (error) {
      console.error('Failed to load models:', error);
      toast.error(t('Failed to load models'));
    } finally {
      setLoading(false);
    }
  };

  const allModels = React.useMemo(() => {
    const models: Array<AdminModelDto & { keyName: string; providerName: string }> = [];
    
    Object.entries(modelsByKey).forEach(([keyId, keyModels]) => {
      const numKeyId = parseInt(keyId, 10);
      let keyInfo: GetModelKeysResult | undefined;
      let providerInfo: ModelProviderDto | undefined;

      // Find key and provider info
      for (const [providerId, keys] of Object.entries(keysByProvider)) {
        const key = keys.find(k => k.id === numKeyId);
        if (key) {
          keyInfo = key;
          providerInfo = providers.find(p => p.providerId === parseInt(providerId, 10));
          break;
        }
      }

      if (keyInfo && providerInfo) {
        const currentProviderInfo = providerInfo;
        const feProvider = feModelProviders.find(fp => fp.id === currentProviderInfo.providerId);
        keyModels.forEach(model => {
          models.push({
            ...model,
            keyName: keyInfo!.name,
            providerName: feProvider?.name || `Provider ${currentProviderInfo.providerId}`,
          });
        });
      }
    });

    return models;
  }, [modelsByKey, keysByProvider, providers]);

  const filteredModels = React.useMemo(() => {
    let filtered = allModels;

    if (selectedProvider !== 'all') {
      filtered = filtered.filter(m => m.modelProviderId === selectedProvider);
    }

    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(m => 
        m.name.toLowerCase().includes(query) ||
        m.keyName.toLowerCase().includes(query) ||
        m.providerName.toLowerCase().includes(query)
      );
    }

    return filtered;
  }, [allModels, selectedProvider, searchQuery]);

  const handleToggleModel = (modelId: number) => {
    if (expandedModelId === modelId) {
      setExpandedModelId(null);
    } else {
      setExpandedModelId(modelId);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex gap-4">
        <Input
          className="max-w-[300px]"
          placeholder={t('Search models...')!}
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
        />
        <Select 
          value={selectedProvider.toString()} 
          onValueChange={(value) => setSelectedProvider(value === 'all' ? 'all' : parseInt(value, 10))}
        >
          <SelectTrigger className="w-[200px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('All Providers')}</SelectItem>
            {providers.map(provider => {
              const feProvider = feModelProviders.find(fp => fp.id === provider.providerId);
              return (
                <SelectItem key={provider.providerId} value={provider.providerId.toString()}>
                  {feProvider?.name || `Provider ${provider.providerId}`}
                </SelectItem>
              );
            })}
          </SelectContent>
        </Select>
      </div>

      <Card>
        {loading ? (
          <div className="p-8 text-center text-muted-foreground">
            {t('Loading...')}
          </div>
        ) : filteredModels.length === 0 ? (
          <div className="p-8 text-center text-muted-foreground">
            {t('No models found')}
          </div>
        ) : (
          <div className="divide-y">
            {filteredModels.map((model) => (
              <ModelUserList
                key={model.modelId}
                model={model}
                providerName={model.providerName}
                keyName={model.keyName}
                isExpanded={expandedModelId === model.modelId}
                onToggle={() => handleToggleModel(model.modelId)}
                onUpdate={() => init()}
              />
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}
