import { useEffect, useState, useMemo } from 'react';

import Link from 'next/link';
import Image from 'next/image';

import useTranslation from '@/hooks/useTranslation';

import { getUserModels } from '@/apis/clientApis';

import { AdminModelDto } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';

import { IconArrowDown, IconMoneybag, IconSearch, IconX } from '@/components/Icons';

interface ProviderGroup {
  providerId: number;
  providerName: string;
  providerIcon: string;
  models: AdminModelDto[];
}

const ModelPricesPage = () => {
  const { t } = useTranslation();
  const [groups, setGroups] = useState<ProviderGroup[]>([]);
  const [selectedProviderId, setSelectedProviderId] = useState<number>(-1); // -1 = All
  const [searchQuery, setSearchQuery] = useState('');
  const [showFreeOnly, setShowFreeOnly] = useState(false);
  const [loading, setLoading] = useState(true);
  const [sortBy, setSortBy] = useState<'name' | 'inputFresh' | 'inputCached' | 'output'>('output');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');

  useEffect(() => {
    getUserModels()
      .then((models) => {
        const enabledModels = models.filter((m) => m.enabled);
        const groupMap = new Map<number, ProviderGroup>();

        enabledModels.forEach((model) => {
          const provider = feModelProviders.find(
            (p) => p.id === model.modelProviderId,
          );
          if (!groupMap.has(model.modelProviderId)) {
            groupMap.set(model.modelProviderId, {
              providerId: model.modelProviderId,
              providerName: provider?.name ?? `Provider ${model.modelProviderId}`,
              providerIcon: provider?.icon ?? '/icons/logo.png',
              models: [],
            });
          }
          groupMap.get(model.modelProviderId)!.models.push(model);
        });

        const sortedGroups = Array.from(groupMap.values()).sort((a, b) =>
          a.providerName.localeCompare(b.providerName),
        );
        setGroups(sortedGroups);
      })
      .finally(() => setLoading(false));
  }, []);

  // Get all models if selectedProviderId === 0, otherwise get models from selected provider
  const selectedGroup = useMemo(() => {
    if (selectedProviderId === -1) {
      // Combine all models from all providers
      const allModels = groups.flatMap((g) => g.models);
      return {
        providerId: -1,
        providerName: t('All'),
        providerIcon: '',
        models: allModels,
      };
    }
    return groups.find((g) => g.providerId === selectedProviderId) ?? null;
  }, [selectedProviderId, groups, t]);

  // Filter models based on search query and free-only toggle
  const filteredModels = useMemo(() => {
    if (!selectedGroup) return [];

    let filtered = selectedGroup.models;

    // Apply free-only filter
    if (showFreeOnly) {
      filtered = filtered.filter(
        (model) =>
          model.inputFreshTokenPrice1M === 0 &&
          model.inputCachedTokenPrice1M === 0 &&
          model.outputTokenPrice1M === 0,
      );
    }

    // Apply search query filter
    if (!searchQuery.trim()) return filtered;

    const query = searchQuery.toLowerCase();
    return filtered.filter(
      (model) => model.name.toLowerCase().includes(query),
    );
  }, [selectedGroup, searchQuery, showFreeOnly]);

  // Apply sorting to the filtered models
  const sortedModels = useMemo(() => {
    const arr = [...filteredModels];

    const getValue = (m: AdminModelDto) => {
      switch (sortBy) {
        case 'name':
          return m.name?.toLowerCase() ?? '';
        case 'inputFresh':
          return m.inputFreshTokenPrice1M ?? 0;
        case 'inputCached':
          return m.inputCachedTokenPrice1M ?? 0;
        case 'output':
        default:
          return m.outputTokenPrice1M ?? 0;
      }
    };

    arr.sort((a, b) => {
      const va = getValue(a) as any;
      const vb = getValue(b) as any;

      if (typeof va === 'string' && typeof vb === 'string') {
        const cmp = va.localeCompare(vb);
        return sortDir === 'asc' ? cmp : -cmp;
      }

      // numeric compare
      const na = Number(va ?? 0);
      const nb = Number(vb ?? 0);
      if (na === nb) return 0;
      return sortDir === 'asc' ? na - nb : nb - na;
    });

    return arr;
  }, [filteredModels, sortBy, sortDir]);

  const formatPrice = (price: number) => {
    if (price === 0) return t('Free');
    return price.toFixed(4);
  };

  return (
    <div className="container max-w-screen-xl mx-auto py-6 px-4 sm:px-6 h-screen flex flex-col">
      <h1 className="text-2xl font-bold mb-6 flex items-center gap-2">
        <Link
          href="/"
          className="inline-flex items-center justify-center whitespace-nowrap rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 hover:bg-accent hover:text-accent-foreground h-10 w-10"
        >
          <IconArrowDown className="rotate-90" size={20} />
        </Link>
        <IconMoneybag size={22} />
        {t('Model Prices')}
      </h1>

      {loading ? (
        <div className="flex items-center justify-center flex-1 text-muted-foreground">
          {t('Loading...')}
        </div>
      ) : groups.length === 0 ? (
        <div className="flex items-center justify-center flex-1 text-muted-foreground">
          {t('No models available')}
        </div>
      ) : (
        <div className="flex flex-1 gap-4 overflow-hidden">
          {/* Left panel: provider list */}
          <aside className="w-52 shrink-0 flex flex-col gap-1 overflow-y-auto rounded-lg border bg-card p-2">
            {/* All button */}
            <button
              onClick={() => {
                setSelectedProviderId(-1);
                setSearchQuery('');
              }}
              className={`flex items-center gap-2 rounded-md px-3 py-2 text-sm text-left transition-colors hover:bg-accent hover:text-accent-foreground ${
                selectedProviderId === -1
                  ? 'bg-accent text-accent-foreground font-medium'
                  : ''
              }`}
            >
              <span className="truncate">{t('All')}</span>
              <span className="ml-auto text-xs text-muted-foreground shrink-0">
                {groups.reduce((sum, g) => sum + g.models.length, 0)}
              </span>
            </button>

            {/* Provider buttons */}
            {groups.map((group) => (
              <button
                key={group.providerId}
                onClick={() => {
                  setSelectedProviderId(group.providerId);
                  setSearchQuery('');
                }}
                className={`flex items-center gap-2 rounded-md px-3 py-2 text-sm text-left transition-colors hover:bg-accent hover:text-accent-foreground ${
                  selectedProviderId === group.providerId
                    ? 'bg-accent text-accent-foreground font-medium'
                    : ''
                }`}
              >
                <Image
                  src={group.providerIcon}
                  alt={group.providerName}
                  width={18}
                  height={18}
                  className="shrink-0 rounded-sm object-contain"
                  onError={(e) => {
                    (e.currentTarget as HTMLImageElement).style.display = 'none';
                  }}
                />
                <span className="truncate">{group.providerName}</span>
                <span className="ml-auto text-xs text-muted-foreground shrink-0">
                  {group.models.length}
                </span>
              </button>
            ))}
          </aside>

          {/* Right panel: model price table */}
          <main className="flex-1 flex flex-col overflow-hidden rounded-lg border bg-card">
            {/* Search box and filters */}
            <div className="shrink-0 flex items-center gap-2 px-4 py-3 border-b bg-card">
              <IconSearch size={18} className="text-muted-foreground shrink-0" />
              <input
                type="text"
                placeholder={t('Search models...')}
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="flex-1 bg-transparent outline-none text-sm placeholder:text-muted-foreground"
              />
              {searchQuery && (
                <button
                  onClick={() => setSearchQuery('')}
                  className="p-1 hover:bg-accent rounded transition-colors"
                >
                  <IconX size={16} className="text-muted-foreground" />
                </button>
              )}
              <div className="shrink-0 h-5 w-px bg-border" />
              <button
                onClick={() => setShowFreeOnly(!showFreeOnly)}
                className={`flex items-center gap-2 px-3 py-1 rounded text-sm transition-colors whitespace-nowrap ${
                  showFreeOnly
                    ? 'bg-accent text-accent-foreground font-medium'
                    : 'hover:bg-accent hover:text-accent-foreground text-muted-foreground'
                }`}
                title={t('Show only free models')}
              >
                <span>{t('Free Only')}</span>
              </button>
            </div>

            {/* Models table */}
            <div className="flex-1 overflow-auto">
              {sortedModels.length === 0 ? (
                <div className="flex items-center justify-center h-full text-muted-foreground text-sm">
                  {searchQuery ? t('No models found') : t('No models available')}
                </div>
              ) : (
                <table className="w-full text-sm">
                  <thead className="sticky top-0 bg-card border-b z-10">
                    <tr>
                      <th
                        className="px-4 py-3 text-left font-semibold cursor-pointer"
                        onClick={() => {
                          if (sortBy === 'name') setSortDir(sortDir === 'asc' ? 'desc' : 'asc');
                          else {
                            setSortBy('name');
                            setSortDir('asc');
                          }
                        }}
                        aria-sort={sortBy === 'name' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
                      >
                        <span className="inline-flex items-center gap-2">
                          {t('Model Name')}
                          <IconArrowDown
                            size={14}
                            className={`transition-transform ${sortBy === 'name' ? (sortDir === 'desc' ? '' : 'rotate-180') : 'opacity-30'}`}
                          />
                        </span>
                      </th>

                      <th
                        className="px-4 py-3 text-right font-semibold whitespace-nowrap cursor-pointer"
                        onClick={() => {
                          if (sortBy === 'inputFresh') setSortDir(sortDir === 'asc' ? 'desc' : 'asc');
                          else {
                            setSortBy('inputFresh');
                            setSortDir('desc');
                          }
                        }}
                        aria-sort={sortBy === 'inputFresh' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
                      >
                        <span className="inline-flex items-center gap-2">
                          {t('Input Price (/ 1M tokens)')}
                          <IconArrowDown
                            size={14}
                            className={`transition-transform ${sortBy === 'inputFresh' ? (sortDir === 'desc' ? '' : 'rotate-180') : 'opacity-30'}`}
                          />
                        </span>
                      </th>

                      <th
                        className="px-4 py-3 text-right font-semibold whitespace-nowrap cursor-pointer"
                        onClick={() => {
                          if (sortBy === 'inputCached') setSortDir(sortDir === 'asc' ? 'desc' : 'asc');
                          else {
                            setSortBy('inputCached');
                            setSortDir('desc');
                          }
                        }}
                        aria-sort={sortBy === 'inputCached' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
                      >
                        <span className="inline-flex items-center gap-2">
                          {t('Cached Input Price (/ 1M tokens)')}
                          <IconArrowDown
                            size={14}
                            className={`transition-transform ${sortBy === 'inputCached' ? (sortDir === 'desc' ? '' : 'rotate-180') : 'opacity-30'}`}
                          />
                        </span>
                      </th>

                      <th
                        className="px-4 py-3 text-right font-semibold whitespace-nowrap cursor-pointer"
                        onClick={() => {
                          if (sortBy === 'output') setSortDir(sortDir === 'asc' ? 'desc' : 'asc');
                          else {
                            setSortBy('output');
                            setSortDir('desc');
                          }
                        }}
                        aria-sort={sortBy === 'output' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
                      >
                        <span className="inline-flex items-center gap-2">
                          {t('Output Price (/ 1M tokens)')}
                          <IconArrowDown
                            size={14}
                            className={`transition-transform ${sortBy === 'output' ? (sortDir === 'desc' ? '' : 'rotate-180') : 'opacity-30'}`}
                          />
                        </span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedModels.map((model, idx) => (
                      <tr
                        key={model.modelId}
                        className={idx % 2 === 0 ? '' : 'bg-muted/40'}
                      >
                        <td className="px-4 py-3 font-medium">{model.name}</td>
                        <td className="px-4 py-3 text-right tabular-nums">
                          {formatPrice(model.inputFreshTokenPrice1M)}
                        </td>
                        <td className="px-4 py-3 text-right tabular-nums">
                          {formatPrice(model.inputCachedTokenPrice1M)}
                        </td>
                        <td className="px-4 py-3 text-right tabular-nums">
                          {formatPrice(model.outputTokenPrice1M)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </main>
        </div>
      )}
    </div>
  );
};

export default ModelPricesPage;
