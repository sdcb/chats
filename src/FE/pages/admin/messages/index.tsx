import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { useRouter } from 'next/router';

import { getMessages } from '@/apis/adminApis';
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
import { IconRefresh } from '@/components/Icons';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { useTextFilterDraft } from '@/components/table/useTextFilterDraft';
import useTranslation from '@/hooks/useTranslation';
import { AdminChatsDto } from '@/types/adminApis';
import { PageResult } from '@/types/page';
import { formatDateTime } from '@/utils/date';
import { cn } from '@/lib/utils';

type MessageColumnKey = 'title' | 'model' | 'username' | 'createdAt' | 'status';

type Filters = {
  user: string;
  content: string;
};

const DEFAULT_COLUMNS: MessageColumnKey[] = [
  'title',
  'model',
  'username',
  'createdAt',
  'status',
];

export default function Messages() {
  const { t } = useTranslation();
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [messages, setMessages] = useState<PageResult<AdminChatsDto[]>>({
    count: 0,
    rows: [],
  });
  const [filters, setFilters] = useState<Filters>({ user: '', content: '' });
  const [page, setPage] = useState(1);
  const [selectedColumns, setSelectedColumns] = useState<MessageColumnKey[]>(DEFAULT_COLUMNS);
  const lastFetchKeyRef = useRef('');

  const pushQuery = useCallback(
    (nextPage: number, nextFilters: Filters, nextColumns: MessageColumnKey[]) => {
      const query: Record<string, string> = {};

      if (nextPage > 1) {
        query.page = nextPage.toString();
      }

      if (nextFilters.user) {
        query.user = nextFilters.user;
      }

      if (nextFilters.content) {
        query.content = nextFilters.content;
      }

      const columnsQuery = buildColumnQuery(nextColumns, DEFAULT_COLUMNS);
      if (columnsQuery) {
        query.columns = columnsQuery;
      }

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

    const nextFilters = {
      user: getFirstQueryValue(router.query.user) || '',
      content: getFirstQueryValue(router.query.content) || '',
    };
    const nextPage = parseQueryPage(
      getFirstQueryValue(router.query.page) || getFirstQueryValue(router.query.p),
    );
    const allColumns: Array<{ key: MessageColumnKey }> = DEFAULT_COLUMNS.map((key) => ({ key }));
    const nextColumns = parseColumnQuery(
      getFirstQueryValue(router.query.columns),
      allColumns,
      DEFAULT_COLUMNS,
    );

    setFilters((prev) =>
      prev.user === nextFilters.user && prev.content === nextFilters.content
        ? prev
        : nextFilters,
    );
    setPage((prev) => (prev === nextPage ? prev : nextPage));
    setSelectedColumns((prev) =>
      prev.join(',') === nextColumns.join(',') ? prev : nextColumns,
    );
  }, [router.isReady, router.query]);

  const refresh = useCallback(
    (force = false) => {
      const params = {
        page,
        pageSize: UNIFIED_TABLE_PAGE_SIZE,
        user: filters.user,
        content: filters.content,
      };
      const fetchKey = JSON.stringify(params);
      if (!force && fetchKey === lastFetchKeyRef.current) {
        return;
      }

      setLoading(true);
      lastFetchKeyRef.current = fetchKey;

      getMessages(params)
        .then((data) => {
          setMessages(data);
        })
        .finally(() => {
          setLoading(false);
        });
    },
    [filters.content, filters.user, page],
  );

  useEffect(() => {
    if (!router.isReady) {
      return;
    }

    refresh();
  }, [refresh, router.isReady]);

  const { draft, setDraft, flushDraft, hasPendingDraft } = useTextFilterDraft({
    committed: filters,
    onCommit: (nextFilters) => {
      pushQuery(1, nextFilters, selectedColumns);
    },
  });

  const updateFilter = (key: keyof Filters, value: string) => {
    setDraft((prev) => ({ ...prev, [key]: value }));
  };

  const toggleColumn = (key: MessageColumnKey, checked: boolean) => {
    const nextSet = new Set(selectedColumns);
    if (checked) {
      nextSet.add(key);
    } else {
      nextSet.delete(key);
      if (nextSet.size === 0) {
        return;
      }
    }

    const nextColumns = DEFAULT_COLUMNS.filter((column) => nextSet.has(column));
    pushQuery(page, draft, nextColumns);
  };

  const allColumns = useMemo<UnifiedTableColumn<AdminChatsDto, MessageColumnKey>[]>(
    () => [
      {
        key: 'title',
        title: t('Title'),
        cell: (item) => (
          <button
            type="button"
            onClick={() => window.open('/message/' + item.id, '_blank')}
            className="truncate text-left underline-offset-4 hover:underline"
          >
            {item.title}
          </button>
        ),
      },
      {
        key: 'model',
        title: t('Model'),
        cell: (item) => (
          <div className="flex overflow-hidden">
            {item.spans.map((span, index) => (
              <div
                key={`message-chat-icon-wrapper-${span.modelId}`}
                className={cn('relative flex-shrink-0', index > 0 && '-ml-2.5')}
                style={{ zIndex: item.spans.length - index }}
              >
                <Tips
                  trigger={
                    <div>
                      <ModelProviderIcon
                        className="cursor-pointer"
                        providerId={span.modelProviderId}
                      />
                    </div>
                  }
                  side="bottom"
                  content={span.modelName}
                />
              </div>
            ))}
          </div>
        ),
      },
      {
        key: 'username',
        title: t('User Name'),
        cell: (item) => item.username,
      },
      {
        key: 'createdAt',
        title: t('Created Time'),
        cell: (item) => formatDateTime(item.createdAt),
      },
      {
        key: 'status',
        title: t('Status'),
        cell: (item) => (
          <div className="flex flex-wrap items-center gap-2">
            {item.isDeleted && (
              <Badge variant="destructive">{t('Deleted')}</Badge>
            )}
            {item.isShared && (
              <Badge className="bg-green-600">{t('Shared')}</Badge>
            )}
            {!item.isDeleted && !item.isShared && '-'}
          </div>
        ),
      },
    ],
    [t],
  );

  const visibleColumns = useMemo(
    () => allColumns.filter((column) => selectedColumns.includes(column.key)),
    [allColumns, selectedColumns],
  );

  return (
    <UnifiedTable
      filters={
        <>
            <Input
              className="w-[180px]"
              placeholder={`${t('User Name')}...`}
              value={draft.user}
              onChange={(event) => updateFilter('user', event.target.value)}
            />
            <Input
              className="w-[180px]"
              placeholder={`${t('Message Content')}...`}
              value={draft.content}
              onChange={(event) => updateFilter('content', event.target.value)}
            />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => {
              if (hasPendingDraft) {
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
      rows={messages.rows}
      loading={loading}
      page={page}
      totalCount={messages.count}
      rowKey={(item) => item.id}
      onPageChange={(nextPage) => {
        pushQuery(nextPage, draft, selectedColumns);
      }}
      mobileContent={
        loading ? (
          <div className="space-y-2">
            {Array.from({ length: 4 }).map((_, index) => (
              <Skeleton key={index} className="h-28 w-full" />
            ))}
          </div>
        ) : messages.rows.length === 0 ? (
          <div className="py-4 text-center text-sm text-muted-foreground">
            {t('No data')}
          </div>
        ) : (
          <div className="space-y-2">
            {messages.rows.map((item) => (
              <div key={item.id} className="space-y-2 rounded-md bg-card p-3 shadow-sm">
                {visibleColumns.map((column) => (
                  <div
                    key={column.key}
                    className="flex items-start justify-between gap-3 text-xs"
                  >
                    <div className="shrink-0 font-medium text-muted-foreground">
                      {column.title}
                    </div>
                    <div className="text-right text-foreground">{column.cell(item)}</div>
                  </div>
                ))}
              </div>
            ))}
          </div>
        )
      }
    />
  );
}
