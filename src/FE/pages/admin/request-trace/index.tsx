import React, { useCallback, useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import ExportButton from '@/components/Button/ExportButtom';
import { IconColumns, IconCompare, IconEye, IconRefresh } from '@/components/Icons';
import RequestTraceCompareDialog from '@/components/admin/request-trace/RequestTraceCompareDialog';
import DateTimePopover from '@/components/Popover/DateTimePopover';
import DeletePopover from '@/components/Popover/DeletePopover';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
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
} from '@/apis/adminApis';
import RequestTraceDetailsDialog from '@/components/admin/request-trace/RequestTraceDetailsDialog';
import {
  ALL_COLUMNS,
  ColumnKey,
  DEFAULT_COLUMNS,
  getDirectionLabel,
  getDurationMs,
} from '@/components/admin/request-trace/requestTraceColumns';
import { cn } from '@/lib/utils';
import { RequestTraceListItem } from '@/types/adminApis';
import { PageResult } from '@/types/page';
import { getTz } from '@/utils/date';
import { formatAbsoluteTime, formatRelativeWithinHour } from '@/utils/relativeTime';
import { getUserSession } from '@/utils/user';
import PaginationContainer from '@/components/Pagination/Pagination';

const PAGE_SIZE = 20;
const FILTER_CONTROL_WIDTH_CLASS = 'w-[180px]';
const COLUMN_QUERY_SEPARATOR = '~';

type Filters = {
  start: string;
  end: string;
  url: string;
  traceId: string;
  username: string;
  direction: '' | '0' | '1';
};

const formatDateParam = (date: Date) => date.toISOString().split('T')[0];

const firstQuery = (value: string | string[] | undefined) =>
  Array.isArray(value) ? value[0] : value;

const parseColumns = (value: string | undefined): ColumnKey[] => {
  if (!value) {
    return DEFAULT_COLUMNS;
  }

  const keys = value
    .split(COLUMN_QUERY_SEPARATOR)
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
    query.columns = columns.join(COLUMN_QUERY_SEPARATOR);
  }

  return query;
};

const toServerColumns = (columns: ColumnKey[]) =>
  columns.filter((x) => x !== 'durationMs').join(',');

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
    const nextSet = new Set(columns);
    if (checked) {
      nextSet.add(key);
    } else {
      nextSet.delete(key);
      if (nextSet.size === 0) {
        return;
      }
    }

    const next = ALL_COLUMNS.map((column) => column.key).filter((columnKey) => nextSet.has(columnKey));

    setColumns(next);
    pushQuery(page, filters, next);
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

  const renderTimestampValue = (value: string | null) => {
    if (!value) {
      return '-';
    }

    return (
      <Tips
        trigger={<span>{formatRelativeWithinHour(value, now, t)}</span>}
        side="top"
        content={formatAbsoluteTime(value)}
      />
    );
  };

  const renderBooleanValue = (value: boolean) => (value ? t('Yes') : t('No'));

  const renderStatusCodeValue = (value: number | null) => {
    if (value == null) {
      return '-';
    }

    const toneClassName =
      value >= 200 && value < 300
        ? 'bg-emerald-100 text-emerald-800 ring-emerald-200 dark:bg-emerald-950/40 dark:text-emerald-300 dark:ring-emerald-900/60'
        : value >= 400
          ? 'bg-red-100 text-red-800 ring-red-200 dark:bg-red-950/40 dark:text-red-300 dark:ring-red-900/60'
          : 'bg-amber-100 text-amber-800 ring-amber-200 dark:bg-amber-950/40 dark:text-amber-300 dark:ring-amber-900/60';

    return (
      <span
        className={cn(
          'inline-flex min-w-12 items-center justify-center rounded px-1.5 py-0.5 text-xs font-medium ring-1 ring-inset',
          toneClassName,
        )}
      >
        {value}
      </span>
    );
  };

  const renderColumnValue = (row: RequestTraceListItem, key: ColumnKey) => {
    switch (key) {
      case 'id':
        return (
          <button
            type="button"
            className="text-left text-primary underline underline-offset-4 hover:text-primary/80 break-all"
            onClick={(e) => { e.stopPropagation(); setDetailsId(row.id); }}
          >
            {row.id}
          </button>
        );
      case 'startedAt':
        return renderTimestampValue(row.startedAt);
      case 'requestBodyAt':
        return renderTimestampValue(row.requestBodyAt);
      case 'responseHeaderAt':
        return renderTimestampValue(row.responseHeaderAt);
      case 'responseBodyAt':
        return renderTimestampValue(row.responseBodyAt);
      case 'direction':
        return getDirectionLabel(row.direction, t);
      case 'method':
        return row.method;
      case 'url':
        return <span className="break-all">{row.url}</span>;
      case 'userId':
        return row.userId ?? '-';
      case 'traceId':
        return row.traceId ? (
          <button
            type="button"
            className="text-left underline underline-offset-4 hover:text-foreground break-all"
            onClick={(e) => { e.stopPropagation(); setDetailsId(row.id); }}
          >
            {row.traceId}
          </button>
        ) : '-';
      case 'userName':
        return row.userName || '-';
      case 'requestContentType':
        return row.requestContentType || '-';
      case 'responseContentType':
        return row.responseContentType || '-';
      case 'statusCode':
        return renderStatusCodeValue(row.statusCode);
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
      case 'requestBodyLength':
        return row.requestBodyLength;
      case 'responseBodyLength':
        return row.responseBodyLength ?? '-';
      case 'hasPayload':
        return renderBooleanValue(row.hasPayload);
      case 'hasRequestBodyRaw':
        return renderBooleanValue(row.hasRequestBodyRaw);
      case 'hasResponseBodyRaw':
        return renderBooleanValue(row.hasResponseBodyRaw);
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
      <Card className="p-3 border-none">
        <div className="flex flex-wrap items-end gap-3">
          <div className={FILTER_CONTROL_WIDTH_CLASS}>
            <Select
              value={filters.direction}
              onValueChange={(val) => handleDirectionChange(val as '' | '0' | '1')}
            >
              <SelectTrigger onReset={() => handleDirectionChange('')} value={filters.direction}>
                {filters.direction
                  ? filters.direction === '0'
                    ? t('Inbound')
                    : t('Outbound')
                  : `${t('Inbound')} + ${t('Outbound')}`}
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="0">{t('Inbound')}</SelectItem>
                <SelectItem value="1">{t('Outbound')}</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <DateTimePopover
            value={filters.start}
            className={FILTER_CONTROL_WIDTH_CLASS}
            placeholder={t('Start date')!}
            onSelect={(date) => updateFilter('start', formatDateParam(date))}
            onReset={filters.start ? () => updateFilter('start', '') : undefined}
          />
          <DateTimePopover
            value={filters.end}
            className={FILTER_CONTROL_WIDTH_CLASS}
            placeholder={t('End date')!}
            onSelect={(date) => updateFilter('end', formatDateParam(date))}
            onReset={filters.end ? () => updateFilter('end', '') : undefined}
          />
          <Input
            className={FILTER_CONTROL_WIDTH_CLASS}
            placeholder={t('Search by url')!}
            value={filters.url}
            onChange={(event) => updateFilter('url', event.target.value, true)}
          />
          <Input
            className={FILTER_CONTROL_WIDTH_CLASS}
            placeholder={t('Search by traceId')!}
            value={filters.traceId}
            onChange={(event) => updateFilter('traceId', event.target.value, true)}
          />
          <Input
            className={FILTER_CONTROL_WIDTH_CLASS}
            placeholder={t('Search by username')!}
            value={filters.username}
            onChange={(event) => updateFilter('username', event.target.value, true)}
          />

          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => refresh(true)}
            disabled={loading}
            aria-label={t('Refresh')}
            title={t('Refresh')}
          >
            <IconRefresh size={18} />
          </Button>

          <div className="ml-auto flex items-center gap-2 self-end">
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

            <DeletePopover
              onDelete={handleDeleteByQuery}
              tooltip={t('Delete by current filters')!}
              description={t('This will delete ALL records matching the current filters, not just selected rows. Continue?')!}
            />

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <div className="hidden sm:block">
                  <Tips
                    trigger={
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-9 w-9"
                        aria-label={t('Select columns')}
                        title={t('Select columns')}
                      >
                        <IconColumns size={18} />
                      </Button>
                    }
                    side="bottom"
                    content={t('Select columns')}
                  />
                </div>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-56">
                <DropdownMenuLabel className="flex items-center gap-2">
                  <IconColumns size={16} />
                  <span>{t('Select columns')}</span>
                </DropdownMenuLabel>
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
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-9 w-9"
                    disabled={selectedIds.length !== 2}
                    onClick={() => setCompareOpen(true)}
                    aria-label={t('Compare')}
                  >
                    <IconCompare size={18} />
                  </Button>
                </div>
              }
              side="bottom"
              content={
                selectedIds.length === 2
                  ? `${t('Compare')} (2/2)`
                  : `${t('Click rows to select for compare')}${selectedIds.length > 0 ? ` (${selectedIds.length}/2)` : ''}`
              }
            />
          </div>
        </div>
      </Card>

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
            {data.rows.map((row) => {
              const isSelected = selectedIds.includes(row.id);
              return (
                <Card
                  key={row.id}
                  className={cn('p-3 space-y-1 cursor-pointer transition-colors', isSelected && 'border-primary bg-primary/5')}
                  onClick={() => toggleSelect(row.id, !isSelected)}
                >
                  <div className="flex items-center justify-between">
                    <div className="font-medium text-xs">#{row.id}</div>
                    {isSelected && <span className="text-xs text-primary font-medium">{t('Selected')}</span>}
                  </div>
                  <div className="text-xs"><span className="text-muted-foreground">{t('Method')}: </span>{row.method}</div>
                  <div className="text-xs break-all"><span className="text-muted-foreground">{t('Url')}: </span>{row.url}</div>
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
                        className="underline underline-offset-4 hover:text-foreground"
                        onClick={(e) => { e.stopPropagation(); setDetailsId(row.id); }}
                      >
                        {row.traceId}
                      </button>
                    ) : (
                      '-'
                    )}
                  </div>
                  <div className="flex justify-end pt-1">
                    <Button size="sm" variant="ghost" onClick={(e) => { e.stopPropagation(); setDetailsId(row.id); }}>
                      <IconEye size={14} className="mr-1" />
                      {t('Details')}
                    </Button>
                  </div>
                </Card>
              );
            })}
          </div>
        )}
      </div>

      <div className="hidden sm:block">
        <Card className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                {visibleColumnDefs.map((column) => (
                  <TableHead key={column.key} className="px-1 py-1 first:pl-4 last:pr-4 text-foreground">{t(column.title)}</TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody isLoading={loading} isEmpty={data.rows.length === 0}>
              {data.rows.map((row) => {
                const isSelected = selectedIds.includes(row.id);
                return (
                  <TableRow
                    key={row.id}
                    className={cn('cursor-pointer transition-colors', isSelected && 'bg-primary/10 border-l-2 border-l-primary')}
                    onClick={() => toggleSelect(row.id, !isSelected)}
                  >
                    {visibleColumnDefs.map((column) => (
                      <TableCell key={column.key} className="px-1 py-1 first:pl-4 last:pr-4 text-muted-foreground">{renderColumnValue(row, column.key)}</TableCell>
                    ))}
                  </TableRow>
                );
              })}
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
