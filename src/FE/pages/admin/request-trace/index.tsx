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
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
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

const FILTER_CONTROL_WIDTH_CLASS = 'w-[180px]';

type Filters = {
  start: string;
  end: string;
  url: string;
  traceId: string;
  username: string;
  direction: '' | '0' | '1';
};

const formatDateParam = (date: Date) => date.toISOString().split('T')[0];

const buildQuery = (page: number, filters: Filters, columns: ColumnKey[]) => {
  const query: Record<string, string> = {};

  if (page > 1) query.page = page.toString();
  if (filters.start) query.start = filters.start;
  if (filters.end) query.end = filters.end;
  if (filters.url) query.url = filters.url;
  if (filters.traceId) query.traceId = filters.traceId;
  if (filters.username) query.username = filters.username;
  if (filters.direction) query.direction = filters.direction;
  const columnsQuery = buildColumnQuery(columns, DEFAULT_COLUMNS);
  if (columnsQuery) {
    query.columns = columnsQuery;
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

    const pageValue = parseQueryPage(getFirstQueryValue(router.query.page));
    const nextFilters: Filters = {
      start: getFirstQueryValue(router.query.start) || '',
      end: getFirstQueryValue(router.query.end) || '',
      url: getFirstQueryValue(router.query.url) || '',
      traceId: getFirstQueryValue(router.query.traceId) || '',
      username: getFirstQueryValue(router.query.username) || '',
      direction:
        getFirstQueryValue(router.query.direction) === '0' || getFirstQueryValue(router.query.direction) === '1'
          ? (getFirstQueryValue(router.query.direction) as '0' | '1')
          : '',
    };
    const nextColumns = parseColumnQuery(
      getFirstQueryValue(router.query.columns),
      ALL_COLUMNS,
      DEFAULT_COLUMNS,
    );

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
        pageSize: UNIFIED_TABLE_PAGE_SIZE,
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

  const visibleColumns: UnifiedTableColumn<RequestTraceListItem, ColumnKey>[] = ALL_COLUMNS
    .filter((column) => columns.includes(column.key))
    .map((column) => ({
      key: column.key,
      title: t(column.title),
      cell: (row) => renderColumnValue(row, column.key),
    }));

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
    <>
      <UnifiedTable
        filters={
          <>
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
            ),
          },
          {
            key: 'delete',
            element: (
              <DeletePopover
                onDelete={handleDeleteByQuery}
                tooltip={t('Delete by current filters')!}
                description={t('This will delete ALL records matching the current filters, not just selected rows. Continue?')!}
              />
            ),
          },
          {
            key: 'columns',
            element: (
              <UnifiedColumnSelector
                allColumns={ALL_COLUMNS}
                selectedColumns={columns}
                onToggleColumn={toggleColumn}
              />
            ),
          },
          {
            key: 'compare',
            element: (
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
            ),
          },
        ]}
        columns={visibleColumns}
        rows={data.rows}
        loading={loading}
        page={page}
        totalCount={data.count}
        rowKey={(row) => row.id}
        onPageChange={(nextPage) => {
          setPage(nextPage);
          pushQuery(nextPage, filters, columns);
        }}
        onRowClick={(row) => toggleSelect(row.id, !selectedIds.includes(row.id))}
        rowClassName={(row) =>
          cn(
            'transition-colors',
            selectedIds.includes(row.id) && 'border-l-2 border-l-primary bg-primary/10',
          )
        }
        mobileContent={
          loading ? (
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
      />

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
    </>
  );
}
