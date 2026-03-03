import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import ExportButton from '@/components/Button/ExportButtom';
import { IconEye } from '@/components/Icons';
import DateTimePopover from '@/components/Popover/DateTimePopover';
import DeletePopover from '@/components/Popover/DeletePopover';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
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
  clearRequestTraceList,
  getRequestTraceList,
  getRequestTraceDetails,
} from '@/apis/adminApis';
import RequestTraceDetailsDialog from '@/components/admin/request-trace/RequestTraceDetailsDialog';
import { cn } from '@/lib/utils';
import { RequestTraceDetails, RequestTraceListItem } from '@/types/adminApis';
import { PageResult } from '@/types/page';
import { formatDateTime, getTz } from '@/utils/date';
import { formatAbsoluteTime, formatRelativeWithinHour } from '@/utils/relativeTime';
import { getUserSession } from '@/utils/user';
import PaginationContainer from '@/components/Pagination/Pagination';

const PAGE_SIZE = 20;

type ColumnKey =
  | 'startedAt'
  | 'direction'
  | 'url'
  | 'statusCode'
  | 'durationMs'
  | 'traceId'
  | 'userName'
  | 'source'
  | 'errorType'
  | 'rawRequestBodyBytes'
  | 'rawResponseBodyBytes';

type Filters = {
  start: string;
  end: string;
  url: string;
  traceId: string;
  username: string;
  direction: '' | '0' | '1';
};

const ALL_COLUMNS: { key: ColumnKey; title: string }[] = [
  { key: 'traceId', title: 'Trace Id' },
  { key: 'startedAt', title: 'Started At' },
  { key: 'direction', title: 'Direction' },
  { key: 'url', title: 'Url' },
  { key: 'statusCode', title: 'Status Code' },
  { key: 'durationMs', title: 'Duration (ms)' },
  { key: 'userName', title: 'User Name' },
  { key: 'source', title: 'Source' },
  { key: 'errorType', title: 'Error Type' },
  { key: 'rawRequestBodyBytes', title: 'Request Bytes' },
  { key: 'rawResponseBodyBytes', title: 'Response Bytes' },
];

const DEFAULT_COLUMNS: ColumnKey[] = [
  'traceId',
  'startedAt',
  'direction',
  'url',
  'statusCode',
  'durationMs',
  'userName',
];

const formatDateParam = (date: Date) => date.toISOString().split('T')[0];

const firstQuery = (value: string | string[] | undefined) =>
  Array.isArray(value) ? value[0] : value;

const parseColumns = (value: string | undefined): ColumnKey[] => {
  if (!value) {
    return DEFAULT_COLUMNS;
  }

  const keys = value
    .split(',')
    .map((x) => x.trim())
    .filter((x): x is ColumnKey => ALL_COLUMNS.some((col) => col.key === x));

  return keys.length > 0 ? keys : DEFAULT_COLUMNS;
};

const buildQuery = (page: number, filters: Filters, columns: ColumnKey[]) => {
  const query: Record<string, string> = {};

  if (page > 1) query.page = page.toString();
  if (filters.start) query.start = filters.start;
  if (filters.end) query.end = filters.end;
  if (filters.url) query.url = filters.url;
  if (filters.traceId) query.traceId = filters.traceId;
  if (filters.username) query.username = filters.username;
  if (filters.direction) query.direction = filters.direction;
  if (columns.join(',') !== DEFAULT_COLUMNS.join(',')) {
    query.columns = columns.join(',');
  }

  return query;
};

const getDurationMs = (row: RequestTraceListItem): number | null => {
  if (!row.responseHeaderAt) {
    return null;
  }

  const start = Date.parse(row.startedAt);
  const responseHeader = Date.parse(row.responseHeaderAt);
  if (Number.isNaN(start) || Number.isNaN(responseHeader)) {
    return null;
  }

  return Math.max(0, responseHeader - start);
};

const toServerColumns = (columns: ColumnKey[]) =>
  columns.filter((x) => x !== 'durationMs').join(',');

const getDirectionLabel = (value: number, t: (key: string) => string) => {
  if (value === 0) return t('Inbound');
  if (value === 1) return t('Outbound');
  return String(value);
};

const RequestTraceCompareDialog = ({
  ids,
  open,
  onOpenChange,
}: {
  ids: string[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) => {
  const { t } = useTranslation();
  const [left, setLeft] = useState<RequestTraceDetails | null>(null);
  const [right, setRight] = useState<RequestTraceDetails | null>(null);
  const [loading, setLoading] = useState(false);
  const [hideSame, setHideSame] = useState(false);

  useEffect(() => {
    if (!open || ids.length !== 2) {
      return;
    }

    setLoading(true);
    Promise.all([getRequestTraceDetails(ids[0]), getRequestTraceDetails(ids[1])])
      .then(([a, b]) => {
        setLeft(a);
        setRight(b);
      })
      .catch((error) => {
        console.error(error);
        toast.error(t('Operation failed, Please try again later, or contact technical personnel'));
      })
      .finally(() => {
        setLoading(false);
      });
  }, [open, ids, t]);

  const rows = useMemo(() => {
    if (!left || !right) return [] as { label: string; a: string; b: string }[];

    const base: { label: string; a: string; b: string }[] = [
      { label: t('Started At'), a: formatDateTime(left.startedAt), b: formatDateTime(right.startedAt) },
      { label: t('Direction'), a: String(left.direction), b: String(right.direction) },
      { label: t('User Name'), a: left.userName || '', b: right.userName || '' },
      { label: t('Trace Id'), a: left.traceId || '', b: right.traceId || '' },
      { label: t('Method'), a: left.method || '', b: right.method || '' },
      { label: t('Url'), a: left.url || '', b: right.url || '' },
      { label: t('Status Code'), a: String(left.statusCode ?? ''), b: String(right.statusCode ?? '') },
      { label: t('Request Headers'), a: left.requestHeaders || '', b: right.requestHeaders || '' },
      { label: t('Response Headers'), a: left.responseHeaders || '', b: right.responseHeaders || '' },
      { label: t('Request Body'), a: left.requestBody || '', b: right.requestBody || '' },
      { label: t('Response Body'), a: left.responseBody || '', b: right.responseBody || '' },
      { label: t('Error Type'), a: left.errorType || '', b: right.errorType || '' },
      { label: t('Error Message'), a: left.errorMessage || '', b: right.errorMessage || '' },
    ];

    if (!hideSame) return base;
    return base.filter((x) => x.a !== x.b);
  }, [hideSame, left, right, t]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-6xl">
        <DialogHeader>
          <DialogTitle>{t('Request Trace Compare')}</DialogTitle>
        </DialogHeader>

        <div className="flex items-center justify-end gap-2 text-sm">
          <span>{t('Hide same values')}</span>
          <Switch checked={hideSame} onCheckedChange={setHideSame} />
        </div>

        {loading ? (
          <div className="space-y-2 py-4">
            {Array.from({ length: 6 }).map((_, idx) => (
              <Skeleton key={idx} className="h-8 w-full" />
            ))}
          </div>
        ) : (
          <div className="max-h-[70vh] overflow-auto">
            <div className="grid grid-cols-[220px_1fr_1fr] gap-2 text-xs font-medium text-muted-foreground sticky top-0 bg-background z-10 py-2">
              <div>{t('Field')}</div>
              <div>{left?.id ?? '-'}</div>
              <div>{right?.id ?? '-'}</div>
            </div>
            <div className="space-y-2">
              {rows.map((row) => {
                const different = row.a !== row.b;
                return (
                  <div key={row.label} className="grid grid-cols-[220px_1fr_1fr] gap-2">
                    <Card className={cn('p-2 text-xs', different && 'border-primary/60')}>{row.label}</Card>
                    <Card className={cn('p-2 text-xs whitespace-pre-wrap break-all', different && 'border-primary/60')}>{row.a || '-'}</Card>
                    <Card className={cn('p-2 text-xs whitespace-pre-wrap break-all', different && 'border-primary/60')}>{row.b || '-'}</Card>
                  </div>
                );
              })}
              {!loading && rows.length === 0 && (
                <div className="text-center py-8 text-sm text-muted-foreground">{t('No difference')}</div>
              )}
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
};

export default function RequestTracePage() {
  const { t } = useTranslation();
  const router = useRouter();
  const [now, setNow] = useState(() => new Date());

  const [filters, setFilters] = useState<Filters>({
    start: '',
    end: '',
    url: '',
    traceId: '',
    username: '',
    direction: '',
  });
  const [columns, setColumns] = useState<ColumnKey[]>(DEFAULT_COLUMNS);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState<PageResult<RequestTraceListItem[]>>({ rows: [], count: 0 });
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [detailsId, setDetailsId] = useState<string | null>(null);
  const [compareOpen, setCompareOpen] = useState(false);
  const lastFetchKeyRef = useRef('');

  useEffect(() => {
    const timer = window.setInterval(() => {
      setNow(new Date());
    }, 30 * 1000);

    return () => window.clearInterval(timer);
  }, []);

  const pushQuery = useCallback(
    (nextPage: number, nextFilters: Filters, nextColumns: ColumnKey[]) => {
      if (!router.isReady) return;
      router.push(
        {
          pathname: router.pathname,
          query: buildQuery(nextPage, nextFilters, nextColumns),
        },
        undefined,
        { shallow: true },
      );
    },
    [router],
  );

  useEffect(() => {
    if (!router.isReady) return;

    const pageValue = parseInt(firstQuery(router.query.page) || '1', 10) || 1;
    const nextFilters: Filters = {
      start: firstQuery(router.query.start) || '',
      end: firstQuery(router.query.end) || '',
      url: firstQuery(router.query.url) || '',
      traceId: firstQuery(router.query.traceId) || '',
      username: firstQuery(router.query.username) || '',
      direction:
        firstQuery(router.query.direction) === '0' || firstQuery(router.query.direction) === '1'
          ? (firstQuery(router.query.direction) as '0' | '1')
          : '',
    };
    const nextColumns = parseColumns(firstQuery(router.query.columns));

    setPage((prev) => (prev === pageValue ? prev : pageValue));
    setFilters((prev) =>
      prev.start === nextFilters.start &&
      prev.end === nextFilters.end &&
      prev.url === nextFilters.url &&
      prev.traceId === nextFilters.traceId &&
      prev.username === nextFilters.username &&
      prev.direction === nextFilters.direction
        ? prev
        : nextFilters,
    );
    setColumns((prev) => (prev.join(',') === nextColumns.join(',') ? prev : nextColumns));
  }, [router.isReady, router.query]);

  const refresh = useCallback(
    (force = false) => {
      if (!router.isReady) return;

      const params = {
        page,
        pageSize: PAGE_SIZE,
        tz: getTz(),
        start: filters.start || undefined,
        end: filters.end || undefined,
        url: filters.url || undefined,
        traceId: filters.traceId || undefined,
        username: filters.username || undefined,
        direction: filters.direction ? Number(filters.direction) : undefined,
      };

      const fetchKey = JSON.stringify(params);
      if (!force && fetchKey === lastFetchKeyRef.current) {
        return;
      }

      lastFetchKeyRef.current = fetchKey;
      setLoading(true);
      getRequestTraceList(params)
        .then((result) => {
          setData(result);
          setSelectedIds([]);
        })
        .catch((error) => {
          console.error(error);
          toast.error(t('Operation failed, Please try again later, or contact technical personnel'));
          lastFetchKeyRef.current = '';
        })
        .finally(() => setLoading(false));
    },
    [filters.direction, filters.end, filters.start, filters.traceId, filters.url, filters.username, page, router.isReady, t],
  );

  useEffect(() => {
    refresh();
  }, [refresh]);

  const debouncedTextSync = useDebounce((nextFilters: Filters) => {
    pushQuery(1, nextFilters, columns);
  }, 500);

  const updateFilter = (key: keyof Filters, value: string, debounce = false) => {
    const next = { ...filters, [key]: value };
    setFilters(next);
    setPage(1);
    if (debounce) {
      debouncedTextSync(next);
      return;
    }
    pushQuery(1, next, columns);
  };

  const toggleColumn = (key: ColumnKey, checked: boolean) => {
    let next = columns;
    if (checked) {
      next = Array.from(new Set([...columns, key]));
    } else {
      next = columns.filter((x) => x !== key);
      if (next.length === 0) {
        return;
      }
    }

    setColumns(next);
    pushQuery(1, filters, next);
  };

  const handleDirectionChange = (value: '' | '0' | '1') => {
    const next = { ...filters, direction: value };
    setFilters(next);
    setPage(1);
    pushQuery(1, next, columns);
  };

  const toggleSelect = (id: string, checked: boolean) => {
    setSelectedIds((prev) => {
      if (checked) {
        if (prev.includes(id)) return prev;
        if (prev.length >= 2) {
          return [prev[1], id];
        }
        return [...prev, id];
      }
      return prev.filter((x) => x !== id);
    });
  };

  const visibleColumnDefs = ALL_COLUMNS.filter((x) => columns.includes(x.key));

  const renderColumnValue = (row: RequestTraceListItem, key: ColumnKey) => {
    switch (key) {
      case 'startedAt':
        return (
          <Tips
            trigger={<span>{formatRelativeWithinHour(row.startedAt, now, t)}</span>}
            side="top"
            content={formatAbsoluteTime(row.startedAt)}
          />
        );
      case 'direction':
        return getDirectionLabel(row.direction, t);
      case 'url':
        return <span className="break-all">{row.method} {row.url}</span>;
      case 'traceId':
        return row.traceId ? (
          <button
            type="button"
            className="text-primary underline underline-offset-4 hover:text-primary/80"
            onClick={() => setDetailsId(row.id)}
          >
            {row.traceId}
          </button>
        ) : '-';
      case 'userName':
        return row.userName || '-';
      case 'statusCode':
        return row.statusCode ?? '-';
      case 'durationMs':
        return getDurationMs(row) ?? '-';
      case 'source':
        return row.source || '-';
      case 'errorType':
        return row.errorType || '-';
      case 'rawRequestBodyBytes':
        return row.rawRequestBodyBytes;
      case 'rawResponseBodyBytes':
        return row.rawResponseBodyBytes ?? '-';
      default:
        return '-';
    }
  };

  const handleDeleteByQuery = async () => {
    await clearRequestTraceList({
      tz: getTz(),
      start: filters.start || undefined,
      end: filters.end || undefined,
      url: filters.url || undefined,
      traceId: filters.traceId || undefined,
      username: filters.username || undefined,
      direction: filters.direction ? Number(filters.direction) : undefined,
      columns: toServerColumns(columns),
    });
    toast.success(t('Deleted successful'));
    lastFetchKeyRef.current = '';
    refresh(true);
  };

  const exportParams = {
    token: getUserSession(),
    tz: getTz(),
    start: filters.start || undefined,
    end: filters.end || undefined,
    url: filters.url || undefined,
    traceId: filters.traceId || undefined,
    username: filters.username || undefined,
    direction: filters.direction ? Number(filters.direction) : undefined,
    columns: toServerColumns(columns),
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-wrap items-center gap-3">
          <DateTimePopover
            value={filters.start}
            placeholder={t('Start date')!}
            onSelect={(date) => updateFilter('start', formatDateParam(date))}
            onReset={filters.start ? () => updateFilter('start', '') : undefined}
          />
          <DateTimePopover
            value={filters.end}
            placeholder={t('End date')!}
            onSelect={(date) => updateFilter('end', formatDateParam(date))}
            onReset={filters.end ? () => updateFilter('end', '') : undefined}
          />
          <Input
            className="w-[240px]"
            placeholder={t('Search by url')!}
            value={filters.url}
            onChange={(event) => updateFilter('url', event.target.value, true)}
          />
          <Input
            className="w-[180px]"
            placeholder={t('Search by traceId')!}
            value={filters.traceId}
            onChange={(event) => updateFilter('traceId', event.target.value, true)}
          />
          <Input
            className="w-[180px]"
            placeholder={t('Search by username')!}
            value={filters.username}
            onChange={(event) => updateFilter('username', event.target.value, true)}
          />
          <div className="w-[160px]">
            <Select
              value={filters.direction}
              onValueChange={(val) => handleDirectionChange(val as '' | '0' | '1')}
            >
              <SelectTrigger onReset={() => handleDirectionChange('')} value={filters.direction}>
                {filters.direction
                  ? filters.direction === '0'
                    ? t('Inbound')
                    : t('Outbound')
                  : t('All')}
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="0">{t('Inbound')}</SelectItem>
                <SelectItem value="1">{t('Outbound')}</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <div className="flex items-center gap-2 self-end lg:self-auto">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm">{t('Columns')}</Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56">
              <DropdownMenuLabel>{t('Select columns')}</DropdownMenuLabel>
              {ALL_COLUMNS.map((column) => (
                <DropdownMenuCheckboxItem
                  key={column.key}
                  checked={columns.includes(column.key)}
                  onSelect={(event) => event.preventDefault()}
                  onCheckedChange={(checked) => toggleColumn(column.key, !!checked)}
                >
                  {t(column.title)}
                </DropdownMenuCheckboxItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>

          <Tips
            trigger={
              <div>
                <ExportButton
                  exportUrl="/api/admin/request-trace/export"
                  params={exportParams}
                  className="h-9 w-9"
                  disabled={loading}
                />
              </div>
            }
            side="bottom"
            content={t('Export to Excel')}
          />

          <DeletePopover onDelete={handleDeleteByQuery} tooltip={t('Delete by current filters')!} />

          <Button
            variant="outline"
            size="sm"
            disabled={selectedIds.length !== 2}
            onClick={() => setCompareOpen(true)}
          >
            {t('Compare')}
          </Button>
        </div>
      </div>

      <div className="block sm:hidden">
        {loading ? (
          <div className="space-y-2">
            {Array.from({ length: 4 }).map((_, idx) => (
              <Skeleton key={idx} className="h-24 w-full" />
            ))}
          </div>
        ) : data.rows.length === 0 ? (
          <div className="text-center py-4 text-sm text-muted-foreground">{t('No data')}</div>
        ) : (
          <div className="space-y-2">
            {data.rows.map((row) => (
              <Card key={row.id} className="p-3 space-y-1">
                <div className="flex items-center justify-between">
                  <div className="font-medium text-xs">#{row.id}</div>
                  <Checkbox
                    checked={selectedIds.includes(row.id)}
                    onCheckedChange={(checked) => toggleSelect(row.id, !!checked)}
                  />
                </div>
                <div className="text-xs break-all"><span className="text-muted-foreground">{t('Url')}: </span>{row.method} {row.url}</div>
                <div className="text-xs">
                  <span className="text-muted-foreground">{t('Started At')}: </span>
                  <Tips
                    trigger={<span>{formatRelativeWithinHour(row.startedAt, now, t)}</span>}
                    side="top"
                    content={formatAbsoluteTime(row.startedAt)}
                  />
                </div>
                <div className="text-xs"><span className="text-muted-foreground">{t('User Name')}: </span>{row.userName || '-'}</div>
                <div className="text-xs">
                  <span className="text-muted-foreground">{t('Trace Id')}: </span>
                  {row.traceId ? (
                    <button
                      type="button"
                      className="text-primary underline underline-offset-4 hover:text-primary/80"
                      onClick={() => setDetailsId(row.id)}
                    >
                      {row.traceId}
                    </button>
                  ) : (
                    '-'
                  )}
                </div>
                <div className="flex justify-end pt-1">
                  <Button size="sm" variant="ghost" onClick={() => setDetailsId(row.id)}>
                    <IconEye size={14} className="mr-1" />
                    {t('Details')}
                  </Button>
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>

      <div className="hidden sm:block">
        <Card className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-[42px]" />
                {visibleColumnDefs.map((column) => (
                  <TableHead key={column.key}>{t(column.title)}</TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody isLoading={loading} isEmpty={data.rows.length === 0}>
              {data.rows.map((row) => (
                <TableRow key={row.id}>
                  <TableCell>
                    <Checkbox
                      checked={selectedIds.includes(row.id)}
                      onCheckedChange={(checked) => toggleSelect(row.id, !!checked)}
                    />
                  </TableCell>
                  {visibleColumnDefs.map((column) => (
                    <TableCell key={column.key}>{renderColumnValue(row, column.key)}</TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>

          {data.rows.length > 0 && (
            <PaginationContainer
              page={page}
              pageSize={PAGE_SIZE}
              currentCount={data.rows.length}
              totalCount={data.count}
              onPagingChange={(nextPage) => {
                setPage(nextPage);
                pushQuery(nextPage, filters, columns);
              }}
            />
          )}
        </Card>
      </div>

      <RequestTraceDetailsDialog
        traceId={detailsId}
        open={detailsId !== null}
        onOpenChange={(open) => {
          if (!open) setDetailsId(null);
        }}
      />

      <RequestTraceCompareDialog
        ids={selectedIds}
        open={compareOpen}
        onOpenChange={setCompareOpen}
      />
    </div>
  );
}
