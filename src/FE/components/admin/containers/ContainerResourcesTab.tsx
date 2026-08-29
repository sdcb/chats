import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { getUserSession } from '@/utils/user';

import { PageResult } from '@/types/page';

import ExportButton from '@/components/Button/ExportButtom';
import { IconRefresh } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import {
  UNIFIED_TABLE_PAGE_SIZE,
  UnifiedColumnSelector,
  UnifiedTable,
  UnifiedTableColumn,
  buildColumnQuery,
  getFirstQueryValue,
  parseColumnQuery,
  parseQueryPage,
} from '@/components/table/UnifiedTable';
import { useTextFilterDraft } from '@/components/table/useTextFilterDraft';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';

import {
  AdminContainerResource,
  ContainerResourceFilters,
  ContainerResourceStatus,
  EMPTY_VALUE,
  formatBytes,
  formatDateTime,
} from './types';

import {
  ADMIN_CONTAINER_RESOURCES_EXPORT_URL,
  getAdminContainerResources,
} from '@/apis/adminContainersApi';

type ResourceColumnKey =
  | 'id'
  | 'status'
  | 'ownerUserId'
  | 'ownerUserName'
  | 'ownerDisplayName'
  | 'ownerChatId'
  | 'ownerChatTitle'
  | 'ownerTurnId'
  | 'runtimeNodeId'
  | 'runtimeNodeName'
  | 'runtimeNodeAIName'
  | 'name'
  | 'image'
  | 'isPermanent'
  | 'backendResourceId'
  | 'ip'
  | 'shellPrefix'
  | 'cpuCores'
  | 'memoryBytes'
  | 'maxProcesses'
  | 'backendNetworkName'
  | 'createdAt'
  | 'updatedAt'
  | 'lastActiveAt'
  | 'stoppedAt'
  | 'deletedAt'
  | 'cleanupAt'
  | 'volumeDeclaredBytes'
  | 'volumeMountCount'
  | 'chatAccessCount';

type TextFilters = Pick<
  ContainerResourceFilters,
  'id' | 'query' | 'owner' | 'runtimeNodeId'
>;

const DEFAULT_COLUMNS: ResourceColumnKey[] = [
  'id',
  'status',
  'ownerUserName',
  'name',
  'image',
  'runtimeNodeName',
  'cpuCores',
  'memoryBytes',
  'maxProcesses',
  'backendNetworkName',
  'ip',
  'isPermanent',
  'ownerChatTitle',
  'updatedAt',
  'cleanupAt',
];

const EMPTY_FILTERS: ContainerResourceFilters = {
  id: '',
  query: '',
  owner: '',
  runtimeNodeId: '',
  status: '',
  permanent: '',
};

const getStatus = (resource: AdminContainerResource) =>
  resource.deletedAt ? 'deleted' : resource.stoppedAt ? 'stopped' : 'active';

export default function ContainerResourcesTab() {
  const { t } = useTranslation();
  const router = useRouter();
  const [data, setData] = useState<PageResult<AdminContainerResource[]>>({
    rows: [],
    count: 0,
  });
  const [loading, setLoading] = useState(false);
  const lastFetchKeyRef = useRef('');
  const requestIdRef = useRef(0);

  const filters = useMemo<ContainerResourceFilters>(() => {
    const status = getFirstQueryValue(router.query.status);
    const permanent = getFirstQueryValue(router.query.permanent);
    return {
      id: getFirstQueryValue(router.query.id) || '',
      query: getFirstQueryValue(router.query.query) || '',
      owner: getFirstQueryValue(router.query.owner) || '',
      runtimeNodeId: getFirstQueryValue(router.query.runtimeNodeId) || '',
      status:
        status === 'active' || status === 'stopped' || status === 'deleted'
          ? status
          : '',
      permanent: permanent === 'true' || permanent === 'false' ? permanent : '',
    };
  }, [
    router.query.id,
    router.query.owner,
    router.query.permanent,
    router.query.query,
    router.query.runtimeNodeId,
    router.query.status,
  ]);

  const page = parseQueryPage(getFirstQueryValue(router.query.page));

  const allColumns = useMemo<
    UnifiedTableColumn<AdminContainerResource, ResourceColumnKey>[]
  >(
    () => [
      {
        key: 'id',
        title: t('Id'),
        cell: (row) => row.id,
      },
      {
        key: 'status',
        title: t('Status'),
        cell: (row) => {
          const status = getStatus(row);
          return (
            <Badge
              variant={
                status === 'deleted'
                  ? 'destructive'
                  : status === 'stopped'
                  ? 'secondary'
                  : 'default'
              }
            >
              {status === 'deleted'
                ? t('Deleted')
                : status === 'stopped'
                ? t('Stopped')
                : t('Running')}
            </Badge>
          );
        },
      },
      {
        key: 'ownerUserId',
        title: t('Owner User ID'),
        cell: (row) => row.ownerUserId,
      },
      {
        key: 'ownerUserName',
        title: t('Owner Username'),
        cell: (row) => row.ownerUserName || EMPTY_VALUE,
      },
      {
        key: 'ownerDisplayName',
        title: t('Owner Display Name'),
        cell: (row) => row.ownerDisplayName || EMPTY_VALUE,
      },
      {
        key: 'ownerChatId',
        title: t('Owner Chat ID'),
        cell: (row) => row.ownerChatId ?? EMPTY_VALUE,
      },
      {
        key: 'ownerChatTitle',
        title: t('Owner Chat Title'),
        className: 'max-w-56',
        cell: (row) => row.ownerChatTitle || EMPTY_VALUE,
      },
      {
        key: 'ownerTurnId',
        title: t('Owner Turn ID'),
        cell: (row) => row.ownerTurnId ?? EMPTY_VALUE,
      },
      {
        key: 'runtimeNodeId',
        title: t('Runtime Node ID'),
        cell: (row) => row.runtimeNodeId,
      },
      {
        key: 'runtimeNodeName',
        title: t('Runtime node'),
        cell: (row) => row.runtimeNodeName || EMPTY_VALUE,
      },
      {
        key: 'runtimeNodeAIName',
        title: t('Runtime AI'),
        cell: (row) => row.runtimeNodeAIName || EMPTY_VALUE,
      },
      {
        key: 'name',
        title: t('Name'),
        className: 'max-w-48',
        cell: (row) => row.name,
      },
      {
        key: 'image',
        title: t('Image'),
        className: 'max-w-64',
        cell: (row) => <code className="break-all text-xs">{row.image}</code>,
      },
      {
        key: 'isPermanent',
        title: t('Lifetime'),
        cell: (row) => (row.isPermanent ? t('Permanent') : t('Temporary')),
      },
      {
        key: 'backendResourceId',
        title: t('Backend Resource ID'),
        className: 'max-w-64',
        cell: (row) => (
          <code className="break-all text-xs">{row.backendResourceId}</code>
        ),
      },
      {
        key: 'ip',
        title: t('IP Address'),
        cell: (row) => row.ip || EMPTY_VALUE,
      },
      {
        key: 'shellPrefix',
        title: t('Shell Prefix'),
        cell: (row) => row.shellPrefix || EMPTY_VALUE,
      },
      {
        key: 'cpuCores',
        title: t('CPU cores'),
        cell: (row) => row.cpuCores ?? EMPTY_VALUE,
      },
      {
        key: 'memoryBytes',
        title: t('Memory bytes'),
        cell: (row) => formatBytes(row.memoryBytes),
      },
      {
        key: 'maxProcesses',
        title: t('Max processes'),
        cell: (row) => row.maxProcesses ?? EMPTY_VALUE,
      },
      {
        key: 'backendNetworkName',
        title: t('Network'),
        cell: (row) => row.backendNetworkName || EMPTY_VALUE,
      },
      {
        key: 'createdAt',
        title: t('Created'),
        cell: (row) => formatDateTime(row.createdAt),
      },
      {
        key: 'updatedAt',
        title: t('Updated'),
        cell: (row) => formatDateTime(row.updatedAt),
      },
      {
        key: 'lastActiveAt',
        title: t('Last active'),
        cell: (row) => formatDateTime(row.lastActiveAt || undefined),
      },
      {
        key: 'stoppedAt',
        title: t('Stopped time'),
        cell: (row) => formatDateTime(row.stoppedAt || undefined),
      },
      {
        key: 'deletedAt',
        title: t('Deleted time'),
        cell: (row) => formatDateTime(row.deletedAt || undefined),
      },
      {
        key: 'cleanupAt',
        title: t('Cleanup time'),
        cell: (row) => formatDateTime(row.cleanupAt || undefined),
      },
      {
        key: 'volumeDeclaredBytes',
        title: t('Volume bytes'),
        cell: (row) => formatBytes(row.volumeDeclaredBytes),
      },
      {
        key: 'volumeMountCount',
        title: t('Volume mounts'),
        cell: (row) => row.volumeMountCount,
      },
      {
        key: 'chatAccessCount',
        title: t('Chat access count'),
        cell: (row) => row.chatAccessCount,
      },
    ],
    [t],
  );

  const selectedColumns = useMemo(
    () =>
      parseColumnQuery(
        getFirstQueryValue(router.query.columns),
        allColumns,
        DEFAULT_COLUMNS,
      ),
    [allColumns, router.query.columns],
  );

  const visibleColumns = useMemo(
    () => allColumns.filter((column) => selectedColumns.includes(column.key)),
    [allColumns, selectedColumns],
  );

  const pushQuery = useCallback(
    (
      nextPage: number,
      nextFilters: ContainerResourceFilters,
      nextColumns: ResourceColumnKey[],
    ) => {
      if (!router.isReady) return;

      // Resources is the first/default tab, so it intentionally has no tab
      // parameter in the URL.
      const query: Record<string, string> = {};
      if (nextPage > 1) query.page = String(nextPage);
      if (nextFilters.id) query.id = nextFilters.id;
      if (nextFilters.query) query.query = nextFilters.query;
      if (nextFilters.owner) query.owner = nextFilters.owner;
      if (nextFilters.runtimeNodeId)
        query.runtimeNodeId = nextFilters.runtimeNodeId;
      if (nextFilters.status) query.status = nextFilters.status;
      if (nextFilters.permanent) query.permanent = nextFilters.permanent;
      const columnsQuery = buildColumnQuery(nextColumns, DEFAULT_COLUMNS);
      if (columnsQuery) query.columns = columnsQuery;

      void router.push({ pathname: router.pathname, query }, undefined, {
        shallow: true,
      });
    },
    [router],
  );

  const committedTextFilters = useMemo<TextFilters>(
    () => ({
      id: filters.id,
      query: filters.query,
      owner: filters.owner,
      runtimeNodeId: filters.runtimeNodeId,
    }),
    [filters.id, filters.owner, filters.query, filters.runtimeNodeId],
  );

  const commitTextFilters = useCallback(
    (nextTextFilters: TextFilters) => {
      pushQuery(1, { ...filters, ...nextTextFilters }, selectedColumns);
    },
    [filters, pushQuery, selectedColumns],
  );

  const { draft, setDraft, flushDraft, hasPendingDraft } = useTextFilterDraft({
    committed: committedTextFilters,
    onCommit: commitTextFilters,
  });

  const loadResources = useCallback(
    async (force = false) => {
      if (!router.isReady) return;

      const fetchKey = JSON.stringify({ page, filters });
      if (!force && fetchKey === lastFetchKeyRef.current) return;

      lastFetchKeyRef.current = fetchKey;
      const requestId = ++requestIdRef.current;
      setLoading(true);
      try {
        const result = await getAdminContainerResources({
          ...filters,
          page,
          pageSize: UNIFIED_TABLE_PAGE_SIZE,
        });
        if (requestId === requestIdRef.current) setData(result);
      } catch (error) {
        if (requestId === requestIdRef.current) lastFetchKeyRef.current = '';
        console.error(error);
      } finally {
        if (requestId === requestIdRef.current) setLoading(false);
      }
    },
    [filters, page, router.isReady],
  );

  useEffect(() => {
    void loadResources();
  }, [loadResources]);

  const updateFilters = (nextFilters: ContainerResourceFilters) =>
    pushQuery(1, { ...nextFilters, ...draft }, selectedColumns);

  const toggleColumn = (key: ResourceColumnKey, checked: boolean) => {
    const nextSet = new Set(selectedColumns);
    if (checked) nextSet.add(key);
    else if (nextSet.size > 1) nextSet.delete(key);
    else return;

    const nextColumns = allColumns
      .map((column) => column.key)
      .filter((column) => nextSet.has(column));
    pushQuery(page, { ...filters, ...draft }, nextColumns);
  };

  const exportParams = useMemo(
    () => ({
      token: getUserSession(),
      id: filters.id || undefined,
      query: filters.query || undefined,
      owner: filters.owner || undefined,
      runtimeNodeId: filters.runtimeNodeId || undefined,
      status: filters.status || undefined,
      permanent: filters.permanent || undefined,
      columns: selectedColumns.join('~'),
    }),
    [filters, selectedColumns],
  );

  return (
    <UnifiedTable
      filters={
        <>
          <Input
            className="w-[110px]"
            inputMode="numeric"
            placeholder={t('Container ID')!}
            value={draft.id}
            onChange={(event) =>
              setDraft((current) => ({ ...current, id: event.target.value }))
            }
          />
          <Input
            className="w-[220px]"
            placeholder={t('Search containers')!}
            value={draft.query}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                query: event.target.value,
              }))
            }
          />
          <Input
            className="w-[180px]"
            placeholder={t('Owner')!}
            value={draft.owner}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                owner: event.target.value,
              }))
            }
          />
          <Input
            className="w-[150px]"
            inputMode="numeric"
            placeholder={t('Runtime Node ID')!}
            value={draft.runtimeNodeId}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                runtimeNodeId: event.target.value,
              }))
            }
          />
          <div className="w-[150px]">
            <Select
              value={filters.status}
              onValueChange={(value) =>
                updateFilters({
                  ...filters,
                  status: value as ContainerResourceStatus,
                })
              }
            >
              <SelectTrigger
                value={filters.status}
                onReset={() =>
                  updateFilters({ ...filters, status: EMPTY_FILTERS.status })
                }
              >
                {filters.status === 'active'
                  ? t('Running')
                  : filters.status === 'stopped'
                  ? t('Stopped')
                  : filters.status === 'deleted'
                  ? t('Deleted')
                  : t('All statuses')}
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="active">{t('Running')}</SelectItem>
                <SelectItem value="stopped">{t('Stopped')}</SelectItem>
                <SelectItem value="deleted">{t('Deleted')}</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="w-[150px]">
            <Select
              value={filters.permanent}
              onValueChange={(value) =>
                updateFilters({
                  ...filters,
                  permanent: value as '' | 'true' | 'false',
                })
              }
            >
              <SelectTrigger
                value={filters.permanent}
                onReset={() =>
                  updateFilters({
                    ...filters,
                    permanent: EMPTY_FILTERS.permanent,
                  })
                }
              >
                {filters.permanent === 'true'
                  ? t('Permanent')
                  : filters.permanent === 'false'
                  ? t('Temporary')
                  : t('All lifetimes')}
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="true">{t('Permanent')}</SelectItem>
                <SelectItem value="false">{t('Temporary')}</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <Button
            type="button"
            variant="outline"
            size="icon"
            disabled={loading}
            aria-label={t('Refresh')}
            title={t('Refresh')}
            onClick={() => {
              if (hasPendingDraft) flushDraft();
              else void loadResources(true);
            }}
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
                    exportUrl={ADMIN_CONTAINER_RESOURCES_EXPORT_URL}
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
      rows={data.rows}
      loading={loading}
      page={page}
      totalCount={data.count}
      rowKey={(row) => row.id}
      onPageChange={(nextPage) =>
        pushQuery(nextPage, { ...filters, ...draft }, selectedColumns)
      }
      emptyText={t('No container resources found.')}
      mobileContent={
        loading ? (
          <div className="space-y-2">
            {Array.from({ length: 4 }).map((_, index) => (
              <Skeleton key={index} className="h-40 w-full" />
            ))}
          </div>
        ) : data.rows.length === 0 ? (
          <div className="py-4 text-center text-sm text-muted-foreground">
            {t('No container resources found.')}
          </div>
        ) : (
          <div className="space-y-2">
            {data.rows.map((row) => (
              <Card
                key={row.id}
                className="space-y-2 border-none p-3 shadow-sm"
              >
                {visibleColumns.map((column) => (
                  <div
                    key={column.key}
                    className="flex items-start justify-between gap-3 text-xs"
                  >
                    <div className="shrink-0 font-medium text-muted-foreground">
                      {column.title}
                    </div>
                    <div className="min-w-0 break-all text-right text-foreground">
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
}
