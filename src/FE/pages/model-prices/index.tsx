import { useEffect, useMemo, useState } from 'react';

import Link from 'next/link';

import useTranslation from '@/hooks/useTranslation';

import { getUserModels } from '@/apis/clientApis';
import { ApiType } from '@/constants/modelDefaults';
import { useIsMobile } from '@/hooks/useMobile';

import { AdminModelDto } from '@/types/adminApis';
import { feModelProviders } from '@/types/model';

import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { IconArrowDown, IconMoneybag, IconSearch, IconX } from '@/components/Icons';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';

import { cn } from '@/lib/utils';

interface ProviderGroup {
  providerId: number;
  providerName: string;
  models: AdminModelDto[];
}

const ALL_PROVIDER_ID = -1;

const API_TYPE_OPTIONS = [
  { value: ApiType.ChatCompletion },
  { value: ApiType.AnthropicMessages },
  { value: ApiType.Response },
  { value: ApiType.ImageGeneration },
] as const;

const getProviderName = (providerId: number) =>
  feModelProviders.find((provider) => provider.id === providerId)?.name ?? `Provider ${providerId}`;

const isFreeModel = (model: AdminModelDto) =>
  model.inputFreshTokenPrice1M === 0 &&
  model.inputCachedTokenPrice1M === 0 &&
  model.outputTokenPrice1M === 0;

const getApiTypeLabel = (apiType: number, t: (key: string) => string) => {
  switch (apiType) {
    case ApiType.ChatCompletion:
      return t('Chat Completion');
    case ApiType.Response:
      return t('Responses');
    case ApiType.ImageGeneration:
      return t('Image') === '镜像' ? '图片' : t('Image');
    case ApiType.AnthropicMessages:
      return t('Messages');
    default:
      return `API ${apiType}`;
  }
};

const getApiTypeBadgeClass = (apiType: number) => {
  switch (apiType) {
    case ApiType.ChatCompletion:
      return 'border-sky-200 bg-sky-50 text-sky-700';
    case ApiType.Response:
      return 'border-emerald-200 bg-emerald-50 text-emerald-700';
    case ApiType.ImageGeneration:
      return 'border-amber-200 bg-amber-50 text-amber-700';
    case ApiType.AnthropicMessages:
      return 'border-fuchsia-200 bg-fuchsia-50 text-fuchsia-700';
    default:
      return 'border-muted-foreground/20 bg-muted text-muted-foreground';
  }
};

const formatPrice = (price: number, t: (key: string) => string) => {
  if (price === 0) return t('Free');
  return price.toFixed(4);
};

const formatCachedInputPrice = (price: number, t: (key: string) => string) => {
  if (price === 0) return '-';
  return formatPrice(price, t);
};

const formatContextWindow = (contextWindow: number) => {
  if (!contextWindow || contextWindow <= 0) return '-';
  return `${Math.floor(contextWindow / 1000)}K`;
};

const getEmptyText = (
  hasModels: boolean,
  hasActiveFilters: boolean,
  t: (key: string) => string,
) => {
  if (!hasModels) return t('No models available');
  return hasActiveFilters ? t('No models found') : t('No models available');
};

const ModelPricesPage = () => {
  const { t } = useTranslation();
  const isMobile = useIsMobile();
  const [models, setModels] = useState<AdminModelDto[]>([]);
  const [selectedProviderId, setSelectedProviderId] = useState<number>(ALL_PROVIDER_ID);
  const [selectedApiTypes, setSelectedApiTypes] = useState<number[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [showFreeOnly, setShowFreeOnly] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getUserModels()
      .then((allModels) => {
        setModels(allModels.filter((model) => model.enabled));
      })
      .finally(() => setLoading(false));
  }, []);

  const providerGroups = useMemo(() => {
    const grouped = new Map<number, ProviderGroup>();

    models.forEach((model) => {
      if (!grouped.has(model.modelProviderId)) {
        grouped.set(model.modelProviderId, {
          providerId: model.modelProviderId,
          providerName: getProviderName(model.modelProviderId),
          models: [],
        });
      }

      grouped.get(model.modelProviderId)!.models.push(model);
    });

    return Array.from(grouped.values()).sort((left, right) =>
      left.providerName.localeCompare(right.providerName),
    );
  }, [models]);

  const selectedProviderModels = useMemo(() => {
    if (selectedProviderId === ALL_PROVIDER_ID) {
      return models;
    }

    return models.filter((model) => model.modelProviderId === selectedProviderId);
  }, [models, selectedProviderId]);

  const apiTypeCounts = useMemo(() => {
    const counts = new Map<number, number>(
      API_TYPE_OPTIONS.map((option) => [option.value, 0]),
    );

    selectedProviderModels.forEach((model) => {
      counts.set(model.apiType, (counts.get(model.apiType) ?? 0) + 1);
    });

    return counts;
  }, [selectedProviderModels]);

  const filteredModels = useMemo(() => {
    let filtered = selectedProviderModels;

    if (selectedApiTypes.length > 0) {
      filtered = filtered.filter((model) => selectedApiTypes.includes(model.apiType));
    }

    if (showFreeOnly) {
      filtered = filtered.filter(isFreeModel);
    }

    const query = searchQuery.trim().toLowerCase();
    if (!query) {
      return filtered;
    }

    return filtered.filter((model) => model.name.toLowerCase().includes(query));
  }, [searchQuery, selectedApiTypes, selectedProviderModels, showFreeOnly]);

  const totalModelCount = models.length;
  const selectedProviderName =
    selectedProviderId === ALL_PROVIDER_ID
      ? t('All')
      : providerGroups.find((group) => group.providerId === selectedProviderId)?.providerName ??
        getProviderName(selectedProviderId);
  const hasActiveFilters =
    selectedProviderId !== ALL_PROVIDER_ID ||
    selectedApiTypes.length > 0 ||
    showFreeOnly ||
    searchQuery.trim().length > 0;
  const emptyText = getEmptyText(totalModelCount > 0, hasActiveFilters, t);

  const searchInput = (
    <div className="relative min-w-0 flex-1">
      <IconSearch
        size={18}
        className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground"
      />
      <Input
        type="text"
        placeholder={t('Search models...')}
        value={searchQuery}
        onChange={(e) => setSearchQuery(e.target.value)}
        className="pl-9 pr-10"
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
  );

  const desktopSidebar = (
    <Card className="hidden w-64 shrink-0 overflow-hidden md:flex md:min-h-0 md:flex-col">
      <CardContent className="min-h-0 flex-1 p-3">
        <div className="space-y-5">
          <section className="space-y-2">
            <div className="px-1">
              <h2 className="text-sm font-semibold text-foreground">{t('Model Provider')}</h2>
            </div>

            <div className="max-h-[284px] overflow-y-auto pr-1">
              <div className="space-y-1">
                <Button
                  variant={selectedProviderId === ALL_PROVIDER_ID ? 'secondary' : 'ghost'}
                  className="h-10 w-full justify-start px-3 py-2 text-left"
                  onClick={() => setSelectedProviderId(ALL_PROVIDER_ID)}
                >
                  <span className="truncate">{t('All')}</span>
                  <Badge
                    variant="outline"
                    className={cn(
                      'ml-auto min-w-8 justify-center border-transparent text-xs',
                      selectedProviderId === ALL_PROVIDER_ID
                        ? 'bg-background text-foreground'
                        : 'bg-muted text-muted-foreground',
                    )}
                  >
                    {totalModelCount}
                  </Badge>
                </Button>

                {providerGroups.map((group) => {
                  const isSelected = selectedProviderId === group.providerId;

                  return (
                    <Button
                      key={group.providerId}
                      variant={isSelected ? 'secondary' : 'ghost'}
                      className="h-10 w-full justify-start gap-2 px-3 py-2 text-left"
                      onClick={() => setSelectedProviderId(group.providerId)}
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
            </div>
          </section>

          <section className="space-y-2">
            <div className="px-1">
              <h2 className="text-sm font-semibold text-foreground">{t('API Type')}</h2>
            </div>

            <ToggleGroup
              type="multiple"
              value={selectedApiTypes.map(String)}
              onValueChange={(values) => setSelectedApiTypes(values.map(Number))}
              className="flex-wrap justify-start gap-2"
            >
              {API_TYPE_OPTIONS.map((option) => (
                <ToggleGroupItem
                  key={option.value}
                  value={String(option.value)}
                  variant="outline"
                  className="h-auto rounded-full px-3 py-1.5 text-xs data-[state=on]:border-primary data-[state=on]:bg-primary data-[state=on]:text-primary-foreground"
                  aria-label={getApiTypeLabel(option.value, t)}
                >
                  {getApiTypeLabel(option.value, t)} ({apiTypeCounts.get(option.value) ?? 0})
                </ToggleGroupItem>
              ))}
            </ToggleGroup>
          </section>
        </div>
      </CardContent>
    </Card>
  );

  const desktopResults = (
    <Card className="hidden min-h-0 flex-1 overflow-hidden md:flex md:flex-col">
      <div className="flex shrink-0 flex-wrap items-center gap-3 border-b px-4 py-3">
        <div className="min-w-0 flex-1">{searchInput}</div>

        <Button
          type="button"
          variant={showFreeOnly ? 'secondary' : 'outline'}
          size="sm"
          onClick={() => setShowFreeOnly((value) => !value)}
          title={t('Show only free models')}
        >
          {t('Free Only')}
        </Button>
      </div>

      <CardContent className="min-h-0 flex-1 overflow-hidden p-0 [&>div]:h-full">
        <Table>
          <TableHeader className="sticky top-0 z-10 [&_tr]:border-b">
            <TableRow className="hover:bg-transparent">
              <TableHead className="bg-card font-semibold text-foreground">
                {t('Model Name')}
              </TableHead>
              <TableHead className="whitespace-nowrap bg-card text-right font-semibold text-foreground">
                {t('Context Window')}
              </TableHead>
              <TableHead className="whitespace-nowrap bg-card text-right font-semibold text-foreground">
                {t('Input Price (/ 1M tokens)')}
              </TableHead>
              <TableHead className="whitespace-nowrap bg-card text-right font-semibold text-foreground">
                {t('Cached Input Price (/ 1M tokens)')}
              </TableHead>
              <TableHead className="whitespace-nowrap bg-card text-right font-semibold text-foreground">
                {t('Output Price (/ 1M tokens)')}
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody isEmpty={filteredModels.length === 0} emptyText={emptyText}>
            {filteredModels.map((model, index) => (
              <TableRow key={model.modelId} className={index % 2 === 0 ? undefined : 'bg-muted/40'}>
                <TableCell className="font-medium text-foreground">{model.name}</TableCell>
                <TableCell className="text-right tabular-nums">
                  {model.apiType === ApiType.ImageGeneration
                    ? ''
                    : formatContextWindow(model.contextWindow)}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatPrice(model.inputFreshTokenPrice1M, t)}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatCachedInputPrice(model.inputCachedTokenPrice1M, t)}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatPrice(model.outputTokenPrice1M, t)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );

  const mobileResults = (
    <div className="space-y-4 md:hidden">
      <Card>
        <CardContent className="space-y-4 p-4">
          <div className="space-y-2">
            <div className="text-sm font-medium text-foreground">{t('Model Provider')}</div>
            <Select
              value={selectedProviderId === ALL_PROVIDER_ID ? 'all' : String(selectedProviderId)}
              onValueChange={(value) =>
                setSelectedProviderId(value === 'all' ? ALL_PROVIDER_ID : Number(value))
              }
            >
              <SelectTrigger
                value={selectedProviderId === ALL_PROVIDER_ID ? '' : String(selectedProviderId)}
                onReset={() => setSelectedProviderId(ALL_PROVIDER_ID)}
              >
                {selectedProviderName}
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">
                  {t('All')} ({totalModelCount})
                </SelectItem>
                {providerGroups.map((group) => (
                  <SelectItem key={group.providerId} value={String(group.providerId)}>
                    {group.providerName} ({group.models.length})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <div className="text-sm font-medium text-foreground">{t('API Type')}</div>
            <ToggleGroup
              type="multiple"
              value={selectedApiTypes.map(String)}
              onValueChange={(values) => setSelectedApiTypes(values.map(Number))}
              className="flex-wrap justify-start gap-2"
            >
              {API_TYPE_OPTIONS.map((option) => (
                <ToggleGroupItem
                  key={option.value}
                  value={String(option.value)}
                  variant="outline"
                  className="h-auto rounded-full px-3 py-1.5 text-xs data-[state=on]:border-primary data-[state=on]:bg-primary data-[state=on]:text-primary-foreground"
                  aria-label={getApiTypeLabel(option.value, t)}
                >
                  {getApiTypeLabel(option.value, t)} ({apiTypeCounts.get(option.value) ?? 0})
                </ToggleGroupItem>
              ))}
            </ToggleGroup>
          </div>

          <div className="space-y-2">
            <div className="text-sm font-medium text-foreground">{t('Model Name')}</div>
            {searchInput}
          </div>

          <Button
            type="button"
            variant={showFreeOnly ? 'secondary' : 'outline'}
            className="w-full"
            onClick={() => setShowFreeOnly((value) => !value)}
            title={t('Show only free models')}
          >
            {t('Free Only')}
          </Button>
        </CardContent>
      </Card>

      {filteredModels.length === 0 ? (
        <Card className="flex items-center justify-center p-6 text-center text-muted-foreground">
          {emptyText}
        </Card>
      ) : (
        <div className="space-y-3">
          {filteredModels.map((model) => (
            <Card key={model.modelId}>
              <CardContent className="space-y-4 p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 space-y-2">
                    <div className="break-words text-base font-semibold text-foreground">
                      {model.name}
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="inline-flex min-w-0 items-center gap-2 rounded-full bg-muted px-2.5 py-1 text-xs text-muted-foreground">
                        <ModelProviderIcon
                          providerId={model.modelProviderId}
                          className="h-4 w-4 shrink-0 rounded-sm"
                        />
                        <span className="truncate">{getProviderName(model.modelProviderId)}</span>
                      </div>
                      <Badge
                        variant="outline"
                        className={cn(
                          'border px-2.5 py-1 text-xs font-medium',
                          getApiTypeBadgeClass(model.apiType),
                        )}
                      >
                        {getApiTypeLabel(model.apiType, t)}
                      </Badge>
                    </div>
                  </div>

                  <div className="flex shrink-0 flex-col items-end gap-2">
                    {model.apiType !== ApiType.ImageGeneration && (
                      <div className="text-xs text-muted-foreground">
                        {formatContextWindow(model.contextWindow)}
                      </div>
                    )}
                    {isFreeModel(model) && (
                      <Badge variant="secondary" className="shrink-0">
                        {t('Free')}
                      </Badge>
                    )}
                  </div>
                </div>

                <div className="rounded-lg bg-muted/35 px-3 py-2">
                  <table className="w-full table-fixed text-sm">
                    <colgroup>
                      <col className="w-[72%]" />
                      <col className="w-[28%]" />
                    </colgroup>
                    <tbody>
                      <tr>
                        <td className="py-1 pr-3 text-xs text-muted-foreground">
                          {t('Input Price (/ 1M tokens)')}
                        </td>
                        <td className="py-1 text-right font-semibold tabular-nums text-foreground">
                          {formatPrice(model.inputFreshTokenPrice1M, t)}
                        </td>
                      </tr>
                      {model.inputCachedTokenPrice1M !== 0 && (
                        <tr>
                          <td className="py-1 pr-3 text-xs text-muted-foreground">
                            {t('Cached Input Price (/ 1M tokens)')}
                          </td>
                          <td className="py-1 text-right font-semibold tabular-nums text-foreground">
                            {formatPrice(model.inputCachedTokenPrice1M, t)}
                          </td>
                        </tr>
                      )}
                      <tr>
                        <td className="py-1 pr-3 text-xs text-muted-foreground">
                          {t('Output Price (/ 1M tokens)')}
                        </td>
                        <td className="py-1 text-right font-semibold tabular-nums text-foreground">
                          {formatPrice(model.outputTokenPrice1M, t)}
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );

  return (
    <div className="container mx-auto flex min-h-screen max-w-screen-xl flex-col px-4 py-6 sm:px-6 md:h-screen md:overflow-hidden">
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
      ) : totalModelCount === 0 ? (
        <Card className="flex flex-1 items-center justify-center text-muted-foreground">
          {t('No models available')}
        </Card>
      ) : (
        <>
          {isMobile ? (
            mobileResults
          ) : (
            <div className="hidden min-h-0 flex-1 gap-4 overflow-hidden md:flex">
              {desktopSidebar}
              {desktopResults}
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default ModelPricesPage;
