import { useEffect, useState, useMemo } from 'react';

import Link from 'next/link';

import useTranslation from '@/hooks/useTranslation';

import { getUserModels } from '@/apis/clientApis';

import { AdminModelDto } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';

import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { IconArrowDown, IconMoneybag, IconSearch, IconX } from '@/components/Icons';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import { cn } from '@/lib/utils';

interface ProviderGroup {
  providerId: number;
  providerName: string;
  models: AdminModelDto[];
}

const ModelPricesPage = () => {
  const { t } = useTranslation();
  const [groups, setGroups] = useState<ProviderGroup[]>([]);
  const [selectedProviderId, setSelectedProviderId] = useState<number>(-1); // -1 = All
  const [searchQuery, setSearchQuery] = useState('');
  const [showFreeOnly, setShowFreeOnly] = useState(false);
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

  const totalModelCount = useMemo(
    () => groups.reduce((sum, group) => sum + group.models.length, 0),
    [groups],
  );

  const formatPrice = (price: number) => {
    if (price === 0) return t('Free');
    return price.toFixed(4);
  };

  return (
    <div className="container max-w-screen-xl mx-auto py-6 px-4 sm:px-6 h-screen flex flex-col">
      <h1 className="mb-6 flex items-center gap-2 text-2xl font-bold">
        <Button asChild variant="ghost" size="icon" className="rounded-md">
          <Link href="/" aria-label={t('Back')}>
            <IconArrowDown className="rotate-90" size={20} />
          </Link>
        </Button>
        <IconMoneybag size={22} />
        {t('Model Prices')}
      </h1>

      {loading ? (
        <Card className="flex flex-1 items-center justify-center text-muted-foreground">
          {t('Loading...')}
        </Card>
      ) : groups.length === 0 ? (
        <Card className="flex flex-1 items-center justify-center text-muted-foreground">
          {t('No models available')}
        </Card>
      ) : (
        <div className="flex flex-1 gap-4 overflow-hidden">
          <Card className="flex w-56 shrink-0 flex-col overflow-hidden">
            <CardContent className="min-h-0 flex-1 p-2">
              <ScrollArea className="h-full">
                <div className="space-y-1 pr-2">
                  <Button
                    variant={selectedProviderId === -1 ? 'secondary' : 'ghost'}
                    className="h-auto w-full justify-start px-3 py-2 text-left"
                    onClick={() => {
                      setSelectedProviderId(-1);
                      setSearchQuery('');
                    }}
                  >
                    <span className="truncate">{t('All')}</span>
                    <Badge
                      variant="outline"
                      className={cn(
                        'ml-auto min-w-8 justify-center border-transparent text-xs',
                        selectedProviderId === -1
                          ? 'bg-background text-foreground'
                          : 'bg-muted text-muted-foreground',
                      )}
                    >
                      {totalModelCount}
                    </Badge>
                  </Button>

                  {groups.map((group) => {
                    const isSelected = selectedProviderId === group.providerId;

                    return (
                      <Button
                        key={group.providerId}
                        variant={isSelected ? 'secondary' : 'ghost'}
                        className="h-auto w-full justify-start gap-2 px-3 py-2 text-left"
                        onClick={() => {
                          setSelectedProviderId(group.providerId);
                          setSearchQuery('');
                        }}
                      >
                        <ModelProviderIcon
                          providerId={group.providerId}
                          className="h-[18px] w-[18px] shrink-0 rounded-sm"
                        />
                        <span className="truncate">{group.providerName}</span>
                        <Badge
                          variant="outline"
                          className={cn(
                            'ml-auto min-w-8 justify-center border-transparent text-xs',
                            isSelected
                              ? 'bg-background text-foreground'
                              : 'bg-muted text-muted-foreground',
                          )}
                        >
                          {group.models.length}
                        </Badge>
                      </Button>
                    );
                  })}
                </div>
              </ScrollArea>
            </CardContent>
          </Card>

          <Card className="flex min-h-0 flex-1 flex-col overflow-hidden">
            <div className="flex shrink-0 flex-wrap items-center gap-3 border-b px-4 py-3">
              <div className="relative min-w-[240px] flex-1">
                <IconSearch
                  size={18}
                  className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground"
                />
                <Input
                  type="text"
                  placeholder={t('Search models...')}
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pr-10 pl-9"
                />
                {searchQuery && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="absolute right-1 top-1/2 h-8 w-8 -translate-y-1/2"
                    onClick={() => setSearchQuery('')}
                    aria-label={t('Clear search')}
                  >
                    <IconX size={16} className="text-muted-foreground" />
                  </Button>
                )}
              </div>

              <Button
                type="button"
                variant={showFreeOnly ? 'secondary' : 'outline'}
                size="sm"
                onClick={() => setShowFreeOnly(!showFreeOnly)}
                title={t('Show only free models')}
              >
                {t('Free Only')}
              </Button>
            </div>

            <CardContent className="min-h-0 flex-1 p-0 [&>div]:h-full">
              <Table>
                <TableHeader className="sticky top-0 z-10 [&_tr]:border-b">
                  <TableRow className="hover:bg-transparent">
                    <TableHead className="bg-card font-semibold text-foreground">
                      {t('Model Name')}
                    </TableHead>
                    <TableHead className="bg-card text-right font-semibold text-foreground whitespace-nowrap">
                      {t('Input Price (/ 1M tokens)')}
                    </TableHead>
                    <TableHead className="bg-card text-right font-semibold text-foreground whitespace-nowrap">
                      {t('Cached Input Price (/ 1M tokens)')}
                    </TableHead>
                    <TableHead className="bg-card text-right font-semibold text-foreground whitespace-nowrap">
                      {t('Output Price (/ 1M tokens)')}
                    </TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody
                  isEmpty={filteredModels.length === 0}
                  emptyText={searchQuery ? t('No models found') : t('No models available')}
                >
                  {filteredModels.map((model, idx) => (
                    <TableRow
                      key={model.modelId}
                      className={idx % 2 === 0 ? undefined : 'bg-muted/40'}
                    >
                      <TableCell className="font-medium text-foreground">
                        {model.name}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatPrice(model.inputFreshTokenPrice1M)}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatPrice(model.inputCachedTokenPrice1M)}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatPrice(model.outputTokenPrice1M)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
};

export default ModelPricesPage;
