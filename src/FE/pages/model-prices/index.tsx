import { useEffect, useState } from 'react';

import Link from 'next/link';
import Image from 'next/image';

import useTranslation from '@/hooks/useTranslation';

import { getUserModels } from '@/apis/clientApis';

import { AdminModelDto } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';

import { IconArrowDown, IconMoneybag } from '@/components/Icons';

interface ProviderGroup {
  providerId: number;
  providerName: string;
  providerIcon: string;
  models: AdminModelDto[];
}

const ModelPricesPage = () => {
  const { t } = useTranslation();
  const [groups, setGroups] = useState<ProviderGroup[]>([]);
  const [selectedProviderId, setSelectedProviderId] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);

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
        if (sortedGroups.length > 0) {
          setSelectedProviderId(sortedGroups[0].providerId);
        }
      })
      .finally(() => setLoading(false));
  }, []);

  const selectedGroup = groups.find((g) => g.providerId === selectedProviderId) ?? null;

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
            {groups.map((group) => (
              <button
                key={group.providerId}
                onClick={() => setSelectedProviderId(group.providerId)}
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
          <main className="flex-1 overflow-auto rounded-lg border bg-card">
            {selectedGroup ? (
              <table className="w-full text-sm">
                <thead className="sticky top-0 bg-card border-b z-10">
                  <tr>
                    <th className="px-4 py-3 text-left font-semibold">{t('Model Name')}</th>
                    <th className="px-4 py-3 text-right font-semibold whitespace-nowrap">
                      {t('Input Price (/ 1M tokens)')}
                    </th>
                    <th className="px-4 py-3 text-right font-semibold whitespace-nowrap">
                      {t('Cached Input Price (/ 1M tokens)')}
                    </th>
                    <th className="px-4 py-3 text-right font-semibold whitespace-nowrap">
                      {t('Output Price (/ 1M tokens)')}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {selectedGroup.models.map((model, idx) => (
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
            ) : null}
          </main>
        </div>
      )}
    </div>
  );
};

export default ModelPricesPage;
