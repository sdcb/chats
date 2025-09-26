import {
  Dispatch,
  ReactNode,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import DateTimePopover from '@/components/Popover/DateTimePopover';
import DeletePopover from '@/components/Popover/DeletePopover';
import ExportButton from '@/components/Button/ExportButtom';
import PaginationContainer from '@/components/Pagiation/Pagiation';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import useDebounce from '@/hooks/useDebounce';
import useTranslation from '@/hooks/useTranslation';
import {
  SecurityLogExportParams,
  SecurityLogQueryParams,
} from '@/types/adminApis';
import { PageResult } from '@/types/page';
import { getTz } from '@/utils/date';
import { getUserSession } from '@/utils/user';

export type SecurityLogTab = 'password' | 'keycloak' | 'sms';

const PAGE_SIZE = 20;

const formatDateParam = (date: Date) => date.toISOString().split('T')[0];

type FiltersState = {
  start: string;
  end: string;
  username: string;
};

type ColumnConfig<T> = {
  header: ReactNode;
  className?: string;
  cell: (row: T) => React.ReactNode;
};

type SecurityLogPanelProps<T> = {
  tab: SecurityLogTab;
  fetchList: (params: SecurityLogQueryParams) => Promise<PageResult<T[]>>;
  clearList: (params: SecurityLogExportParams) => Promise<number>;
  exportUrl: string;
  columns: ColumnConfig<T>[];
  getRowKey: (row: T) => string | number;
  renderEmpty?: () => React.ReactNode;
  pageSize?: number;
  childrenAboveTable?: React.ReactNode;
};

type PushQueryParams = {
  tabValue: SecurityLogTab;
  pageValue: number;
  filters: FiltersState;
};

const buildQueryObject = ({ tabValue, pageValue, filters }: PushQueryParams) => {
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

  return query;
};

const useQueryState = (
  tab: SecurityLogTab,
  pageSize: number,
): [
  FiltersState,
  Dispatch<React.SetStateAction<FiltersState>>,
  number,
  Dispatch<React.SetStateAction<number>>,
  (params: PushQueryParams) => void,
] => {
  const router = useRouter();
  const [filters, setFilters] = useState<FiltersState>({ start: '', end: '', username: '' });
  const [page, setPage] = useState(1);

  const pushQuery = useCallback(
    ({ tabValue, pageValue, filters: nextFilters }: PushQueryParams) => {
      if (!router.isReady) {
        return;
      }

      const query = buildQueryObject({ tabValue, pageValue, filters: nextFilters });

      router.push(
        {
          pathname: router.pathname,
          query,
        },
        undefined,
        { shallow: true },
      );
    },
    [router],
  );

  useEffect(() => {
    if (!router.isReady) {
      return;
    }

    const { page: pageQueryParam, start, end, username } = router.query;

    const pageQueryValue = Array.isArray(pageQueryParam)
      ? pageQueryParam[0]
      : pageQueryParam;
    const parsedPage = pageQueryValue ? parseInt(pageQueryValue, 10) || 1 : 1;

    setPage((prev) => (prev === parsedPage ? prev : parsedPage));

    const startQuery = typeof start === 'string' ? start : '';
    const endQuery = typeof end === 'string' ? end : '';
    const usernameQuery = typeof username === 'string' ? username : '';

    setFilters((prev) =>
      prev.start === startQuery &&
      prev.end === endQuery &&
      prev.username === usernameQuery
        ? prev
        : { start: startQuery, end: endQuery, username: usernameQuery },
    );
  }, [router.isReady, router.query]);

  useEffect(() => {
    setPage(1);
  }, [pageSize]);

  return [filters, setFilters, page, setPage, pushQuery];
};

const TableSkeleton = ({
  columns,
  rowCount = 10,
}: {
  columns: ColumnConfig<any>[];
  rowCount?: number;
}) => (
  <Table>
    <TableHeader>
      <TableRow>
        {columns.map((column, index) => (
          <TableHead key={index} className={column.className}>
            {column.header}
          </TableHead>
        ))}
      </TableRow>
    </TableHeader>
    <TableBody>
      {Array.from({ length: rowCount }).map((_, rowIndex) => (
        <TableRow key={`skeleton-${rowIndex}`}>
          {columns.map((column, columnIndex) => (
            <TableCell key={columnIndex} className={column.className}>
              <Skeleton className="h-5 w-full" />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </TableBody>
  </Table>
);

const SecurityLogPanel = <T,>({
  tab,
  fetchList,
  clearList,
  exportUrl,
  columns,
  getRowKey,
  renderEmpty,
  pageSize = PAGE_SIZE,
  childrenAboveTable,
}: SecurityLogPanelProps<T>) => {
  const { t } = useTranslation();
  const router = useRouter();

  const [filters, setFilters, page, setPage, pushQuery] = useQueryState(
    tab,
    pageSize,
  );
  const [data, setData] = useState<PageResult<T[]>>({ rows: [], count: 0 });
  const [loading, setLoading] = useState(false);
  const lastFetchKeyRef = useRef('');

  const refresh = useCallback(
    (options?: { force?: boolean }) => {
      if (!router.isReady) {
        return;
      }

      const params: SecurityLogQueryParams = {
        page,
        pageSize,
        tz: getTz(),
        start: filters.start || undefined,
        end: filters.end || undefined,
        username: filters.username || undefined,
      };

      const fetchKey = JSON.stringify({
        tab,
        ...params,
        start: params.start ?? '',
        end: params.end ?? '',
        username: params.username ?? '',
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
    [fetchList, filters.end, filters.start, filters.username, page, pageSize, router.isReady, t, tab],
  );

  useEffect(() => {
    if (!router.isReady) {
      return;
    }

    refresh();
  }, [refresh, router.isReady]);

  const debouncedUsernameSync = useDebounce(
    (
      value: string,
      tabValue: SecurityLogTab,
      startValue: string,
      endValue: string,
    ) => {
      pushQuery({
        tabValue,
        pageValue: 1,
        filters: { start: startValue, end: endValue, username: value },
      });
    },
    600,
  );

  const handlePageChange = (pageValue: number) => {
    setPage(pageValue);
    pushQuery({ tabValue: tab, pageValue, filters });
  };

  const handleStartChange = (date: Date) => {
    const value = formatDateParam(date);
    const nextFilters = { ...filters, start: value };
    setFilters(nextFilters);
    setPage(1);
    pushQuery({ tabValue: tab, pageValue: 1, filters: nextFilters });
  };

  const handleEndChange = (date: Date) => {
    const value = formatDateParam(date);
    const nextFilters = { ...filters, end: value };
    setFilters(nextFilters);
    setPage(1);
    pushQuery({ tabValue: tab, pageValue: 1, filters: nextFilters });
  };

  const handleResetStart = () => {
    const nextFilters = { ...filters, start: '' };
    setFilters(nextFilters);
    setPage(1);
    pushQuery({ tabValue: tab, pageValue: 1, filters: nextFilters });
  };

  const handleResetEnd = () => {
    const nextFilters = { ...filters, end: '' };
    setFilters(nextFilters);
    setPage(1);
    pushQuery({ tabValue: tab, pageValue: 1, filters: nextFilters });
  };

  const handleUsernameChange = (value: string) => {
    const nextFilters = { ...filters, username: value };
    setFilters(nextFilters);
    setPage(1);
    debouncedUsernameSync(value, tab, nextFilters.start, nextFilters.end);
  };

  const getExportParams = useCallback(() => {
    const params: Record<string, string | number> = {
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

    return params;
  }, [filters.end, filters.start, filters.username]);

  const handleClear = useCallback(async () => {
    const params: SecurityLogExportParams = {
      tz: getTz(),
      start: filters.start || undefined,
      end: filters.end || undefined,
      username: filters.username || undefined,
    };

    try {
      await clearList(params);
      toast.success(t('Deleted successful'));
      // 强制生成新的 fetchKey 以触发刷新
      lastFetchKeyRef.current = '';
      refresh({ force: true });
    } catch (error) {
      console.error(error);
      toast.error(
        t('Operation failed, Please try again later, or contact technical personnel'),
      );
      throw error;
    }
  }, [clearList, filters.end, filters.start, filters.username, refresh, t]);

  const rows = useMemo(() => data?.rows ?? [], [data?.rows]);
  const totalCount = data?.count ?? 0;

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-wrap items-center gap-3">
          <DateTimePopover
            value={filters.start}
            placeholder={t('Start date')!}
            onSelect={handleStartChange}
            onReset={filters.start ? handleResetStart : undefined}
          />
          <DateTimePopover
            value={filters.end}
            placeholder={t('End date')!}
            onSelect={handleEndChange}
            onReset={filters.end ? handleResetEnd : undefined}
          />
          <Input
            className="w-[240px]"
            placeholder={t('Search by username')!}
            value={filters.username}
            onChange={(event) => handleUsernameChange(event.target.value)}
          />
        </div>
        <div className="flex items-center gap-2 self-end lg:self-auto">
          <ExportButton
            exportUrl={exportUrl}
            params={getExportParams()}
            className="h-9 w-9"
            disabled={loading}
          />
          <DeletePopover onDelete={handleClear} />
        </div>
      </div>

      {childrenAboveTable}

      <Card>
        {loading ? (
          <div className="py-6">
            <TableSkeleton columns={columns} />
          </div>
        ) : rows.length === 0 ? (
          <div className="py-10 text-center text-sm text-muted-foreground">
            {renderEmpty ? renderEmpty() : t('No data')}
          </div>
        ) : (
          <>
            <Table>
              <TableHeader>
                <TableRow>
                  {columns.map((column, index) => (
                    <TableHead key={index} className={column.className}>
                      {column.header}
                    </TableHead>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={getRowKey(row)}>
                    {columns.map((column, index) => (
                      <TableCell key={index} className={column.className}>
                        {column.cell(row)}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            {rows.length > 0 && (
              <PaginationContainer
                page={page}
                pageSize={pageSize}
                currentCount={rows.length}
                totalCount={totalCount}
                onPagingChange={handlePageChange}
              />
            )}
          </>
        )}
      </Card>
    </div>
  );
};

export default SecurityLogPanel;
