import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import { getUsage, getUsageStat, getUserApiKey, getUserModels } from '@/apis/clientApis';
import ExportButton from '@/components/Button/ExportButtom';
import { IconRefresh } from '@/components/Icons';
import DateTimePopover from '@/components/Popover/DateTimePopover';
import Tips from '@/components/Tips/Tips';
import {
  UnifiedColumnSelector,
  UnifiedTable,
  UnifiedTableColumn,
  buildColumnQuery,
  getFirstQueryValue,
  parseColumnQuery,
  parseQueryPage,
  UNIFIED_TABLE_PAGE_SIZE,
} from '@/components/table/UnifiedTable';
import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import {
  TableCell,
  TableFooter,
  TableRow,
} from '@/components/ui/table';
import { useTextFilterDraft } from '@/components/table/useTextFilterDraft';
import useTranslation from '@/hooks/useTranslation';
import { useUserInfo } from '@/providers/UserProvider';
import { UsageSource } from '@/types/chat';
import {
  GetUsageParams,
  GetUsageResult,
  GetUsageStatResult,
  GetUserApiKeyResult,
} from '@/types/clientApis';
import { feModelProviders } from '@/types/model';
import { PageResult } from '@/types/page';
import { formatNumberAsMoney, toFixed } from '@/utils/common';
import { formatDate, formatDateTime, getTz } from '@/utils/date';
import { getUserSession } from '@/utils/user';

type UsageTableMode = 'user' | 'admin';

type Provider = {
  modelProviderId: number;
  name: string;
};

type UsageTableFilters = {
  source: string;
  kid: string;
  provider: string;
  start: string;
  end: string;
  user: string;
  modelKey: string;
  model: string;
};

type TextFilters = Pick<UsageTableFilters, 'user' | 'modelKey' | 'model'>;

type UsageColumnKey =
  | 'date'
  | 'account'
  | 'model'
  | 'tokens'
  | 'cost'
  | 'totalCost'
  | 'ip'
  | 'finishReason'
  | 'duration';

export interface UsageTableProps {
  mode: UsageTableMode;
  fixedSource?: UsageSource;
  basePath?: string;
}

const formatDateParam = (date: Date) => formatDate(date.toLocaleDateString());

const pickTextFilters = (filters: UsageTableFilters): TextFilters => ({
  user: filters.user,
  modelKey: filters.modelKey,
  model: filters.model,
});

const buildUsageQuery = (
  mode: UsageTableMode,
  page: number,
  filters: UsageTableFilters,
  columns: UsageColumnKey[],
  defaultColumns: UsageColumnKey[],
  fixedSource?: UsageSource,
  basePath?: string,
) => {
  const query: Record<string, string> = {};

  if (!basePath) {
    query.t = 'usage';
  }

  if (page > 1) {
    query.page = page.toString();
  }

  if (fixedSource === undefined && filters.source) {
    query.source = filters.source;
  }

  if (mode === 'user' && filters.kid) {
    query.kid = filters.kid;
  }

  if (filters.provider) {
    query.provider = filters.provider;
  }

  if (filters.start) {
    query.start = filters.start;
  }

  if (filters.end) {
    query.end = filters.end;
  }

  if (mode === 'admin' && filters.user) {
    query.user = filters.user;
  }

  if (mode === 'admin' && filters.modelKey) {
    query['model-key'] = filters.modelKey;
  }

  if (mode === 'admin' && filters.model) {
    query.model = filters.model;
  }

  const columnsQuery = buildColumnQuery(columns, defaultColumns);
  if (columnsQuery) {
    query.columns = columnsQuery;
  }

  return query;
};

const UsageTable = ({ mode, fixedSource, basePath }: UsageTableProps) => {
  const { t } = useTranslation();
  const router = useRouter();
  const user = useUserInfo();

  const [rows, setRows] = useState<GetUsageResult[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [stats, setStats] = useState<GetUsageStatResult>({} as GetUsageStatResult);
  const [loading, setLoading] = useState(false);
  const [providers, setProviders] = useState<Provider[]>([]);
  const [apiKeys, setApiKeys] = useState<GetUserApiKeyResult[]>([]);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<UsageTableFilters>({
    source: '',
    kid: '',
    provider: '',
    start: '',
    end: '',
    user: '',
    modelKey: '',
    model: '',
  });
  const lastFetchKeyRef = useRef('');

  const allColumns = useMemo<UnifiedTableColumn<GetUsageResult, UsageColumnKey>[]>(
    () => {
      const columns: UnifiedTableColumn<GetUsageResult, UsageColumnKey>[] = [
        {
          key: 'date',
          title: t('Date'),
          cell: (row) => formatDateTime(row.usagedCreatedAt),
        },
      ];

      if (mode === 'admin') {
        columns.push({
          key: 'account',
          title: t('Account'),
          cell: (row) => row.userName,
        });
      }

      columns.push(
        {
          key: 'model',
          title: t('Model'),
          cell: (row) => (
            <div className="flex items-center gap-1">
              <ModelProviderIcon
                className={mode === 'admin' ? 'h-5 w-5' : 'h-4 w-4'}
                providerName={row.modelProviderName}
              />
              <span className="whitespace-nowrap">{row.modelName}</span>
            </div>
          ),
        },
        {
          key: 'tokens',
          title: t('Input/Output Tokens'),
          cell: (row) => `${row.inputTokens}/${row.outputTokens}`,
        },
        {
          key: 'cost',
          title: t('Input/Output Cost'),
          cell: (row) => `${toFixed(row.inputCost)}/${toFixed(row.outputCost)}`,
        },
        {
          key: 'totalCost',
          title: t('Total Cost'),
          cell: (row) => toFixed(row.inputCost + row.outputCost),
        },
        {
          key: 'ip',
          title: t('IP'),
          cell: (row) => row.ip,
        },
        {
          key: 'finishReason',
          title: t('Finish Reason'),
          cell: (row) => row.finishReason,
        },
        {
          key: 'duration',
          title: t('Total Duration(ms)'),
          cell: (row) => (row.totalDurationMs / 1000).toLocaleString(),
        },
      );

      return columns;
    },
    [mode, t],
  );

  const defaultColumns = useMemo<UsageColumnKey[]>(
    () => allColumns.map((column) => column.key),
    [allColumns],
  );
  const [selectedColumns, setSelectedColumns] = useState<UsageColumnKey[]>(defaultColumns);

  const visibleColumns = useMemo(
    () => allColumns.filter((column) => selectedColumns.includes(column.key)),
    [allColumns, selectedColumns],
  );

  useEffect(() => {
    getUserModels().then((data) => {
      const uniqueProviders = Array.from(
        new Set(data.map((model) => model.modelProviderId)),
      ).map((providerId) => {
        const provider = feModelProviders.find((item) => item.id === providerId);
        return {
          modelProviderId: providerId,
          name: provider?.name || `Provider ${providerId}`,
        };
      });
      setProviders(uniqueProviders);
    });

    if (mode === 'user') {
      getUserApiKey().then((data) => {
        setApiKeys(data);
      });
    }
  }, [mode]);

  const pushQuery = useCallback(
    (nextPage: number, nextFilters: UsageTableFilters, nextColumns: UsageColumnKey[]) => {
      if (!router.isReady) {
        return;
      }

      router.push(
        {
          pathname: basePath || router.pathname,
          query: buildUsageQuery(
            mode,
            nextPage,
            nextFilters,
            nextColumns,
            defaultColumns,
            fixedSource,
            basePath,
          ),
        },
        undefined,
        { shallow: true },
      );
    },
    [basePath, defaultColumns, fixedSource, mode, router],
  );

  useEffect(() => {
    if (!router.isReady) {
      return;
    }

    const nextFilters: UsageTableFilters = {
      source:
        fixedSource !== undefined
          ? String(fixedSource)
          : getFirstQueryValue(router.query.source) || '',
      kid: getFirstQueryValue(router.query.kid) || '',
      provider: getFirstQueryValue(router.query.provider) || '',
      start: getFirstQueryValue(router.query.start) || '',
      end: getFirstQueryValue(router.query.end) || '',
      user: getFirstQueryValue(router.query.user) || '',
      modelKey: getFirstQueryValue(router.query['model-key']) || '',
      model: getFirstQueryValue(router.query.model) || '',
    };
    const nextPage = parseQueryPage(
      getFirstQueryValue(router.query.page) || getFirstQueryValue(router.query.p),
    );
    const nextColumns = parseColumnQuery(
      getFirstQueryValue(router.query.columns),
      allColumns,
      defaultColumns,
    );

    setPage((prev) => (prev === nextPage ? prev : nextPage));
    setFilters((prev) =>
      JSON.stringify(prev) === JSON.stringify(nextFilters) ? prev : nextFilters,
    );
    setSelectedColumns((prev) =>
      prev.join(',') === nextColumns.join(',') ? prev : nextColumns,
    );
  }, [allColumns, defaultColumns, fixedSource, router.isReady, router.query]);

  const getUsageParams = useCallback(
    (currentPage: number, currentFilters: UsageTableFilters): GetUsageParams => ({
      kid: mode === 'user' ? currentFilters.kid || undefined : undefined,
      user: mode === 'admin' ? currentFilters.user || undefined : undefined,
      provider: currentFilters.provider || undefined,
      modelKey: mode === 'admin' ? currentFilters.modelKey || undefined : undefined,
      model: mode === 'admin' ? currentFilters.model || undefined : undefined,
      start: currentFilters.start || undefined,
      end: currentFilters.end || undefined,
      page: currentPage,
      pageSize: UNIFIED_TABLE_PAGE_SIZE,
      tz: getTz(),
      source:
        currentFilters.source !== ''
          ? (Number(currentFilters.source) as UsageSource)
          : undefined,
    }),
    [mode],
  );

  const refresh = useCallback(
    (force = false) => {
      if (!router.isReady) {
        return;
      }

      const params = getUsageParams(page, filters);
      const fetchKey = JSON.stringify(params);
      if (!force && fetchKey === lastFetchKeyRef.current) {
        return;
      }

      lastFetchKeyRef.current = fetchKey;
      setLoading(true);

      const tasks = [
        getUsage(params).then((result: PageResult<GetUsageResult[]>) => {
          setRows(result.rows);
          setTotalCount(result.count);
        }),
      ];

      if (mode === 'user') {
        tasks.push(
          getUsageStat({ ...params, page: undefined, pageSize: undefined }).then(
            (result) => {
              setStats(result);
            },
          ),
        );
      }

      Promise.all(tasks)
        .catch((error) => {
          console.error(error);
          toast.error(
            t('Operation failed, Please try again later, or contact technical personnel'),
          );
          lastFetchKeyRef.current = '';
        })
        .finally(() => {
          setLoading(false);
        });
    },
    [filters, getUsageParams, mode, page, router.isReady, t],
  );

  useEffect(() => {
    refresh();
  }, [refresh]);

  const { draft, setDraft, flushDraft, hasPendingDraft } = useTextFilterDraft({
    committed: pickTextFilters(filters),
    onCommit: (nextTextFilters) => {
      pushQuery(
        1,
        {
          ...filters,
          ...nextTextFilters,
        },
        selectedColumns,
      );
    },
  });

  const updateImmediateFilter = (partial: Partial<UsageTableFilters>) => {
    const nextFilters = { ...filters, ...draft, ...partial };
    pushQuery(1, nextFilters, selectedColumns);
  };

  const updateTextFilter = (partial: Partial<TextFilters>) => {
    setDraft((prev) => ({
      ...prev,
      ...partial,
    }));
  };

  const toggleColumn = (key: UsageColumnKey, checked: boolean) => {
    const nextSet = new Set(selectedColumns);
    if (checked) {
      nextSet.add(key);
    } else {
      nextSet.delete(key);
      if (nextSet.size === 0) {
        return;
      }
    }

    const nextColumns = allColumns
      .map((column) => column.key)
      .filter((columnKey) => nextSet.has(columnKey));

    pushQuery(
      page,
      {
        ...filters,
        ...draft,
      },
      nextColumns,
    );
  };

  const exportParams = useMemo(() => {
    const params: Record<string, string | number | undefined> = {
      token: getUserSession(),
      tz: getTz(),
      kid: mode === 'user' ? filters.kid || undefined : undefined,
      user: mode === 'admin' ? filters.user || undefined : undefined,
      provider: filters.provider || undefined,
      'model-key': mode === 'admin' ? filters.modelKey || undefined : undefined,
      model: mode === 'admin' ? filters.model || undefined : undefined,
      start: filters.start || undefined,
      end: filters.end || undefined,
      source:
        filters.source !== ''
          ? Number(filters.source)
          : undefined,
    };

    return params;
  }, [filters, mode]);

  return (
    <UnifiedTable
      filters={
        <>
          {mode === 'user' && fixedSource === undefined && (
            <div className="w-[180px]">
              <Select
                value={filters.source}
                onValueChange={(value) => updateImmediateFilter({ source: value })}
              >
                <SelectTrigger
                  className="w-full"
                  value={filters.source}
                  onReset={() => updateImmediateFilter({ source: '' })}
                >
                  <SelectValue placeholder={t('Select Source')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={String(UsageSource.WebChat)}>
                    {t('WebChat')}
                  </SelectItem>
                  <SelectItem value={String(UsageSource.Api)}>
                    {t('Api')}
                  </SelectItem>
                  <SelectItem value={String(UsageSource.Summary)}>
                    {t('Summary')}
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>
          )}

          {mode === 'user' && (
            <div className="w-[180px]">
              <Select
                value={filters.kid}
                onValueChange={(value) => updateImmediateFilter({ kid: value })}
              >
                <SelectTrigger
                  className="w-full"
                  value={filters.kid}
                  onReset={() => updateImmediateFilter({ kid: '' })}
                >
                  <SelectValue placeholder={t('Select API Key')} />
                </SelectTrigger>
                <SelectContent>
                  {apiKeys.map((apiKey) => {
                    const date = formatDate(apiKey.createdAt);
                    const displayText = apiKey.comment
                      ? `${apiKey.comment}(${date})`
                      : `(${date})`;

                    return (
                      <SelectItem
                        key={apiKey.id.toString()}
                        value={apiKey.id.toString()}
                      >
                        {displayText}
                      </SelectItem>
                    );
                  })}
                </SelectContent>
              </Select>
            </div>
          )}

          <div className="w-[180px]">
            <Select
              value={filters.provider}
              onValueChange={(value) => updateImmediateFilter({ provider: value })}
            >
              <SelectTrigger
                className="w-full"
                value={filters.provider}
                onReset={() => updateImmediateFilter({ provider: '' })}
              >
                <SelectValue placeholder={t('Select Provider')} />
              </SelectTrigger>
              <SelectContent>
                {providers.map((provider) => (
                  <SelectItem key={provider.name} value={provider.name}>
                    {provider.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {mode === 'admin' && (
            <Input
              className="w-[180px] placeholder:text-neutral-400"
              placeholder={t('Account')}
              value={draft.user}
              onChange={(event) =>
                updateTextFilter({ user: event.target.value })
              }
            />
          )}

          {mode === 'admin' && (
            <Input
              className="w-[180px] placeholder:text-neutral-400"
              placeholder={t('Model Key')}
              value={draft.modelKey}
              onChange={(event) =>
                updateTextFilter({ modelKey: event.target.value })
              }
            />
          )}

          {mode === 'admin' && (
            <Input
              className="w-[180px] placeholder:text-neutral-400"
              placeholder={t('Model')}
              value={draft.model}
              onChange={(event) =>
                updateTextFilter({ model: event.target.value })
              }
            />
          )}

          <DateTimePopover
            value={filters.start}
            className="w-[180px]"
            placeholder={t('Start date')!}
            onSelect={(date: Date) =>
              updateImmediateFilter({ start: formatDateParam(date) })
            }
            onReset={filters.start ? () => updateImmediateFilter({ start: '' }) : undefined}
          />

          <DateTimePopover
            value={filters.end}
            className="w-[180px]"
            placeholder={t('End date')!}
            onSelect={(date: Date) =>
              updateImmediateFilter({ end: formatDateParam(date) })
            }
            onReset={filters.end ? () => updateImmediateFilter({ end: '' }) : undefined}
          />

          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => {
              if (mode === 'admin' && hasPendingDraft) {
                flushDraft();
                return;
              }

              refresh(true);
            }}
            disabled={loading}
            aria-label={t('Refresh')}
            title={t('Refresh')}
          >
            <IconRefresh size={18} />
          </Button>
        </>
      }
      actions={[
        {
          key: 'export',
          element: (
            <Tips
              trigger={
                <div>
                  <ExportButton
                    exportUrl="/api/usage/excel"
                    params={exportParams}
                    className="h-9 w-9"
                    disabled={loading}
                  />
                </div>
              }
              side="bottom"
              content={t('Export to Excel')}
            />
          ),
        },
        {
          key: 'columns',
          element: (
            <UnifiedColumnSelector
              allColumns={allColumns.map((column) => ({
                key: column.key,
                title: column.title,
              }))}
              selectedColumns={selectedColumns}
              onToggleColumn={toggleColumn}
            />
          ),
        },
      ]}
      columns={visibleColumns}
      rows={rows}
      loading={loading}
      page={page}
      totalCount={totalCount}
      rowKey={(row) =>
        `${row.usagedCreatedAt}-${row.modelName}-${row.ip}-${row.inputTokens}-${row.outputTokens}`
      }
      onPageChange={(nextPage) => {
        pushQuery(
          nextPage,
          {
            ...filters,
            ...draft,
          },
          selectedColumns,
        );
      }}
      footer={
        mode === 'user' && totalCount > 0 ? (
          <TableFooter className="bg-card">
            <TableRow>
              {visibleColumns.map((column, index) => (
                <TableCell key={column.key}>
                  {column.key === 'tokens'
                    ? `${formatNumberAsMoney(stats?.sumInputTokens)}/${formatNumberAsMoney(stats?.sumOutputTokens)}`
                    : column.key === 'cost'
                      ? `${toFixed(stats?.sumInputCost)}/${toFixed(stats?.sumOutputCost)}`
                      : column.key === 'totalCost'
                        ? toFixed(stats?.sumTotalCost)
                        : index === 0
                          ? t('Total')
                          : ''}
                </TableCell>
              ))}
            </TableRow>
          </TableFooter>
        ) : undefined
      }
      mobileContent={
        loading ? (
          <div className="space-y-2">
            {Array.from({ length: 4 }).map((_, index) => (
              <Skeleton key={index} className="h-28 w-full" />
            ))}
          </div>
        ) : rows.length === 0 ? (
          <div className="py-4 text-center text-sm text-muted-foreground">
            {t('No data')}
          </div>
        ) : (
          <div className="space-y-2">
            {mode === 'user' && (
              <div className="rounded-md border-none bg-card p-3 shadow-sm">
                <div className="flex items-center justify-between text-xs">
                  <div className="font-medium">{t('Input/Output Tokens')}</div>
                  <div>
                    {stats?.sumInputTokens}/{stats?.sumOutputTokens}
                  </div>
                </div>
                <div className="mt-1 flex items-center justify-between text-xs">
                  <div className="font-medium">{t('Input/Output Cost')}</div>
                  <div>
                    {toFixed(stats?.sumInputCost)}/{toFixed(stats?.sumOutputCost)}
                  </div>
                </div>
                <div className="mt-1 flex items-center justify-between text-xs">
                  <div className="font-medium">{t('Total Cost')}</div>
                  <div>{toFixed(stats?.sumTotalCost)}</div>
                </div>
              </div>
            )}
            {rows.map((row, index) => (
              <div
                key={`${row.usagedCreatedAt}-${row.modelName}-${index}`}
                className="space-y-1 rounded-md border-none bg-card p-3 shadow-sm"
              >
                {visibleColumns.map((column) => (
                  <div
                    key={column.key}
                    className="flex items-start justify-between gap-3 text-xs"
                  >
                    <div className="shrink-0 font-medium text-muted-foreground">
                      {column.title}
                    </div>
                    <div className="text-right text-foreground">{column.cell(row)}</div>
                  </div>
                ))}
              </div>
            ))}
          </div>
        )
      }
    />
  );
};

export default UsageTable;
