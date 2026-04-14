import { Dispatch, ReactNode, SetStateAction, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import ExportButton from '@/components/Button/ExportButtom';
import { IconRefresh } from '@/components/Icons';
import DateTimePopover from '@/components/Popover/DateTimePopover';
import DeletePopover from '@/components/Popover/DeletePopover';
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
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { useTextFilterDraft } from '@/components/table/useTextFilterDraft';
import useTranslation from '@/hooks/useTranslation';
import { PageResult } from '@/types/page';
import {
  SecurityLogExportParams,
  SecurityLogQueryParams,
} from '@/types/adminApis';
import { getTz } from '@/utils/date';
import { getUserSession } from '@/utils/user';

export type SecurityLogTab = 'password' | 'keycloak' | 'sms';

type FiltersState = {
  start: string;
  end: string;
  username: string;
  success: '' | 'true' | 'false';
};

type TextFilters = Pick<FiltersState, 'username'>;

type SecurityLogPanelProps<T, TColumnKey extends string = string> = {
  tab: SecurityLogTab;
  fetchList: (params: SecurityLogQueryParams) => Promise<PageResult<T[]>>;
  clearList: (params: SecurityLogExportParams) => Promise<number>;
  exportUrl: string;
  columns: UnifiedTableColumn<T, TColumnKey>[];
  defaultColumns?: TColumnKey[];
  getRowKey: (row: T) => string | number;
  renderEmpty?: () => ReactNode;
};

type PushQueryParams<TColumnKey extends string> = {
  tabValue: SecurityLogTab;
  pageValue: number;
  filters: FiltersState;
  columns: TColumnKey[];
};

const formatDateParam = (date: Date) => date.toISOString().split('T')[0];

const pickTextFilters = (filters: FiltersState): TextFilters => ({
  username: filters.username,
});

const buildQueryObject = <TColumnKey extends string>({
  tabValue,
  pageValue,
  filters,
  columns,
  defaultColumns,
}: PushQueryParams<TColumnKey> & {
  defaultColumns: TColumnKey[];
}) => {
  const query: Record<string, string> = {};

  if (tabValue !== 'password') {
    query.tab = tabValue;
  }

  if (pageValue > 1) {
    query.page = pageValue.toString();
  }

  if (filters.start) {
    query.start = filters.start;
  }

  if (filters.end) {
    query.end = filters.end;
  }

  if (filters.username) {
    query.username = filters.username;
  }

  if (filters.success) {
    query.success = filters.success;
  }

  const columnsQuery = buildColumnQuery(columns, defaultColumns);
  if (columnsQuery) {
    query.columns = columnsQuery;
  }

  return query;
};

const useQueryState = <TColumnKey extends string>(
  tab: SecurityLogTab,
  columns: UnifiedTableColumn<unknown, TColumnKey>[],
  defaultColumns: TColumnKey[],
): [
  FiltersState,
  Dispatch<SetStateAction<FiltersState>>,
  number,
  Dispatch<SetStateAction<number>>,
  TColumnKey[],
  Dispatch<SetStateAction<TColumnKey[]>>,
  (params: PushQueryParams<TColumnKey>) => void,
] => {
  const router = useRouter();
  const [filters, setFilters] = useState<FiltersState>({
    start: '',
    end: '',
    username: '',
    success: '',
  });
  const [page, setPage] = useState(1);
  const [selectedColumns, setSelectedColumns] = useState<TColumnKey[]>(defaultColumns);

  const pushQuery = useCallback(
    ({ tabValue, pageValue, filters: nextFilters, columns: nextColumns }: PushQueryParams<TColumnKey>) => {
      if (!router.isReady) {
        return;
      }

      const query = buildQueryObject({
        tabValue,
        pageValue,
        filters: nextFilters,
        columns: nextColumns,
        defaultColumns,
      });

      router.push(
        {
          pathname: router.pathname,
          query,
        },
        undefined,
        { shallow: true },
      );
    },
    [defaultColumns, router],
  );

  useEffect(() => {
    if (!router.isReady) {
      return;
    }

    setPage((prev) => {
      const nextPage = parseQueryPage(getFirstQueryValue(router.query.page));
      return prev === nextPage ? prev : nextPage;
    });

    const start = getFirstQueryValue(router.query.start) || '';
    const end = getFirstQueryValue(router.query.end) || '';
    const username = getFirstQueryValue(router.query.username) || '';
    const successValue = getFirstQueryValue(router.query.success);
    const success =
      successValue === 'true' || successValue === 'false' ? successValue : '';

    setFilters((prev) => {
      if (
        prev.start === start &&
        prev.end === end &&
        prev.username === username &&
        prev.success === success
      ) {
        return prev;
      }

      return { start, end, username, success };
    });

    const nextColumns = parseColumnQuery(
      getFirstQueryValue(router.query.columns),
      columns,
      defaultColumns,
    );
    setSelectedColumns((prev) =>
      prev.join(',') === nextColumns.join(',') ? prev : nextColumns,
    );
  }, [columns, defaultColumns, router.isReady, router.query]);

  useEffect(() => {
    setSelectedColumns(defaultColumns);
  }, [defaultColumns]);

  return [
    filters,
    setFilters,
    page,
    setPage,
    selectedColumns,
    setSelectedColumns,
    pushQuery,
  ];
};

const SecurityLogPanel = <T, TColumnKey extends string = string>({
  tab,
  fetchList,
  clearList,
  exportUrl,
  columns,
  defaultColumns,
  getRowKey,
  renderEmpty,
}: SecurityLogPanelProps<T, TColumnKey>) => {
  const { t } = useTranslation();
  const router = useRouter();
  const resolvedDefaultColumns = useMemo(
    () => defaultColumns ?? columns.map((column) => column.key),
    [columns, defaultColumns],
  );

  const [filters, setFilters, page, setPage, selectedColumns, setSelectedColumns, pushQuery] =
    useQueryState(
      tab,
      columns as UnifiedTableColumn<unknown, TColumnKey>[],
      resolvedDefaultColumns,
    );
  const [data, setData] = useState<PageResult<T[]>>({ rows: [], count: 0 });
  const [loading, setLoading] = useState(false);
  const lastFetchKeyRef = useRef('');

  const visibleColumns = useMemo(
    () => columns.filter((column) => selectedColumns.includes(column.key)),
    [columns, selectedColumns],
  );

  const refresh = useCallback(
    (options?: { force?: boolean }) => {
      if (!router.isReady) {
        return;
      }

      const params: SecurityLogQueryParams = {
        page,
        pageSize: UNIFIED_TABLE_PAGE_SIZE,
        tz: getTz(),
        start: filters.start || undefined,
        end: filters.end || undefined,
        username: filters.username || undefined,
        success: filters.success ? filters.success === 'true' : undefined,
      };

      const fetchKey = JSON.stringify({
        tab,
        ...params,
        success: filters.success,
      });

      if (!options?.force && fetchKey === lastFetchKeyRef.current) {
        return;
      }

      lastFetchKeyRef.current = fetchKey;
      setLoading(true);

      fetchList(params)
        .then((result) => {
          setData(result);
        })
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
    [fetchList, filters.end, filters.start, filters.success, filters.username, page, router.isReady, t, tab],
  );

  useEffect(() => {
    if (!router.isReady) {
      return;
    }

    refresh();
  }, [refresh, router.isReady]);

  const { draft, setDraft, flushDraft, hasPendingDraft } = useTextFilterDraft({
    committed: pickTextFilters(filters),
    onCommit: (nextTextFilters) => {
      pushQuery({
        tabValue: tab,
        pageValue: 1,
        filters: {
          ...filters,
          ...nextTextFilters,
        },
        columns: selectedColumns,
      });
    },
  });

  const handlePageChange = (pageValue: number) => {
    pushQuery({
      tabValue: tab,
      pageValue,
      filters: {
        ...filters,
        ...draft,
      },
      columns: selectedColumns,
    });
  };

  const updateFilters = (nextFilters: FiltersState) => {
    pushQuery({
      tabValue: tab,
      pageValue: 1,
      filters: {
        ...nextFilters,
        ...draft,
      },
      columns: selectedColumns,
    });
  };

  const handleUsernameChange = (value: string) => {
    setDraft({ username: value });
  };

  const handleToggleColumn = (key: TColumnKey, checked: boolean) => {
    const nextSet = new Set(selectedColumns);
    if (checked) {
      nextSet.add(key);
    } else {
      nextSet.delete(key);
      if (nextSet.size === 0) {
        return;
      }
    }

    const nextColumns = columns
      .map((column) => column.key)
      .filter((columnKey) => nextSet.has(columnKey));

    pushQuery({
      tabValue: tab,
      pageValue: page,
      filters: {
        ...filters,
        ...draft,
      },
      columns: nextColumns,
    });
  };

  const getExportParams = useCallback(() => {
    const params: Record<string, string | number | boolean> = {
      token: getUserSession(),
      tz: getTz(),
    };

    if (filters.start) {
      params.start = filters.start;
    }

    if (filters.end) {
      params.end = filters.end;
    }

    if (filters.username) {
      params.username = filters.username;
    }

    if (filters.success) {
      params.success = filters.success === 'true';
    }

    return params;
  }, [filters.end, filters.start, filters.success, filters.username]);

  const handleClear = useCallback(async () => {
    const params: SecurityLogExportParams = {
      tz: getTz(),
      start: filters.start || undefined,
      end: filters.end || undefined,
      username: filters.username || undefined,
      success: filters.success ? filters.success === 'true' : undefined,
    };

    try {
      await clearList(params);
      toast.success(t('Deleted successful'));
      lastFetchKeyRef.current = '';
      refresh({ force: true });
    } catch (error) {
      console.error(error);
      toast.error(
        t('Operation failed, Please try again later, or contact technical personnel'),
      );
      throw error;
    }
  }, [clearList, filters.end, filters.start, filters.success, filters.username, refresh, t]);

  return (
    <UnifiedTable
      filters={
        <>
          <DateTimePopover
            value={filters.start}
            className="w-[180px]"
            placeholder={t('Start date')!}
            onSelect={(date) =>
              updateFilters({ ...filters, start: formatDateParam(date) })
            }
            onReset={filters.start ? () => updateFilters({ ...filters, start: '' }) : undefined}
          />
          <DateTimePopover
            value={filters.end}
            className="w-[180px]"
            placeholder={t('End date')!}
            onSelect={(date) =>
              updateFilters({ ...filters, end: formatDateParam(date) })
            }
            onReset={filters.end ? () => updateFilters({ ...filters, end: '' }) : undefined}
          />
          <Input
            className="w-[180px]"
            placeholder={t('Search by username')!}
            value={draft.username}
            onChange={(event) => handleUsernameChange(event.target.value)}
          />
          <div className="w-[180px]">
            <Select
              value={filters.success}
              onValueChange={(value) =>
                updateFilters({
                  ...filters,
                  success: value as '' | 'true' | 'false',
                })
              }
            >
              <SelectTrigger
                onReset={() => updateFilters({ ...filters, success: '' })}
                value={filters.success}
              >
                {filters.success
                  ? filters.success === 'true'
                    ? t('Success')
                    : t('Unsuccessful')
                  : t('All')}
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="true">{t('Success')}</SelectItem>
                <SelectItem value="false">{t('Unsuccessful')}</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => {
              if (hasPendingDraft) {
                flushDraft();
                return;
              }

              refresh({ force: true });
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
                    exportUrl={exportUrl}
                    params={getExportParams()}
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
          key: 'delete',
          element: (
            <DeletePopover
              onDelete={handleClear}
              tooltip={t('Clear all logs')!}
            />
          ),
        },
        {
          key: 'columns',
          element: (
            <UnifiedColumnSelector
              allColumns={columns.map((column) => ({
                key: column.key,
                title: column.title,
              }))}
              selectedColumns={selectedColumns}
              onToggleColumn={handleToggleColumn}
            />
          ),
        },
      ]}
      columns={visibleColumns}
      rows={data.rows}
      loading={loading}
      page={page}
      totalCount={data.count}
      rowKey={getRowKey}
      onPageChange={handlePageChange}
      emptyText={renderEmpty ? renderEmpty() : t('No data')}
      mobileContent={
        loading ? (
          <div className="space-y-2">
            {Array.from({ length: 4 }).map((_, index) => (
              <Skeleton key={index} className="h-28 w-full" />
            ))}
          </div>
        ) : data.rows.length === 0 ? (
          <div className="py-4 text-center text-sm text-muted-foreground">
            {renderEmpty ? renderEmpty() : t('No data')}
          </div>
        ) : (
          <div className="space-y-2">
            {data.rows.map((row) => (
              <Card key={getRowKey(row)} className="space-y-2 border-none p-3 shadow-sm">
                {visibleColumns.map((column) => (
                  <div key={column.key} className="flex items-start justify-between gap-3 text-xs">
                    <div className="shrink-0 font-medium text-muted-foreground">
                      {column.title}
                    </div>
                    <div className="text-right text-foreground">
                      {column.cell(row)}
                    </div>
                  </div>
                ))}
              </Card>
            ))}
          </div>
        )
      }
    />
  );
};

export default SecurityLogPanel;
