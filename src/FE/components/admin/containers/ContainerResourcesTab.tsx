import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { getUserSession } from '@/utils/user';

import { ContainerTemplate, UserContainerResource } from '@/types/containers';
import { PageResult } from '@/types/page';

import ExportButton from '@/components/Button/ExportButtom';
import {
  IconDoorIn,
  IconEdit,
  IconPlus,
  IconRefresh,
  IconStopFilled,
} from '@/components/Icons';
import DeletePopover from '@/components/Popover/DeletePopover';
import Tips from '@/components/Tips/Tips';
import IconActionButton from '@/components/common/IconActionButton';
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import {
  USER_CONTAINER_RESOURCES_EXPORT_URL,
  createContainer,
  deleteContainer,
  getUserContainerResources,
  listContainerTemplates,
  startContainer,
  stopContainer,
  updateContainer,
} from '@/apis/containersApi';

type ResourceRow = AdminContainerResource | UserContainerResource;
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
  | 'chatAccessCount'
  | 'actions';

type Props = {
  mode?: 'admin' | 'user';
  settingsTab?: string;
};

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
const USER_DEFAULT_COLUMNS: ResourceColumnKey[] = DEFAULT_COLUMNS.filter(
  (column) => !column.startsWith('owner'),
);
const getStatus = (resource: ResourceRow) =>
  resource.deletedAt ? 'deleted' : resource.stoppedAt ? 'stopped' : 'active';
const isUserResource = (
  resource: ResourceRow,
): resource is UserContainerResource => 'encryptedId' in resource;

export default function ContainerResourcesTab({
  mode = 'admin',
  settingsTab,
}: Props) {
  const isUserMode = mode === 'user';
  const { t } = useTranslation();
  const router = useRouter();
  const defaultColumns = isUserMode ? USER_DEFAULT_COLUMNS : DEFAULT_COLUMNS;
  const [data, setData] = useState<PageResult<ResourceRow[]>>({
    rows: [],
    count: 0,
  });
  const [loading, setLoading] = useState(false);
  const lastFetchKeyRef = useRef('');
  const requestIdRef = useRef(0);
  const [editing, setEditing] = useState<UserContainerResource | null>(null);
  const [editForm, setEditForm] = useState({
    cpuCores: '',
    memoryBytes: '',
    maxProcesses: '',
    backendNetworkName: '',
  });
  const [saving, setSaving] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [templates, setTemplates] = useState<ContainerTemplate[]>([]);
  const [templateId, setTemplateId] = useState<number | null>(null);
  const [name, setName] = useState('');
  const [creating, setCreating] = useState(false);
  const changeStateRef = useRef<
    (resource: UserContainerResource) => Promise<void>
  >(async () => undefined);
  const removeRef = useRef<(resource: UserContainerResource) => Promise<void>>(
    async () => undefined,
  );

  const filters = useMemo<ContainerResourceFilters>(() => {
    const status = getFirstQueryValue(router.query.status);
    const permanent = getFirstQueryValue(router.query.permanent);
    return {
      id: getFirstQueryValue(router.query.id) || '',
      query: getFirstQueryValue(router.query.query) || '',
      owner: isUserMode ? '' : getFirstQueryValue(router.query.owner) || '',
      runtimeNodeId: getFirstQueryValue(router.query.runtimeNodeId) || '',
      status:
        status === 'active' || status === 'stopped' || status === 'deleted'
          ? status
          : '',
      permanent: permanent === 'true' || permanent === 'false' ? permanent : '',
    };
  }, [
    isUserMode,
    router.query.id,
    router.query.owner,
    router.query.permanent,
    router.query.query,
    router.query.runtimeNodeId,
    router.query.status,
  ]);
  const page = parseQueryPage(getFirstQueryValue(router.query.page));

  const beginEdit = useCallback((resource: UserContainerResource) => {
    setEditing(resource);
    setEditForm({
      cpuCores: resource.cpuCores?.toString() ?? '',
      memoryBytes: resource.memoryBytes?.toString() ?? '',
      maxProcesses: resource.maxProcesses?.toString() ?? '',
      backendNetworkName: resource.backendNetworkName ?? '',
    });
  }, []);

  const allColumns = useMemo<
    UnifiedTableColumn<ResourceRow, ResourceColumnKey>[]
  >(() => {
    const columns: UnifiedTableColumn<ResourceRow, ResourceColumnKey>[] = [
      { key: 'id', title: t('Id'), cell: (row) => row.id },
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
        cell: (row) => ('ownerUserId' in row ? row.ownerUserId : EMPTY_VALUE),
      },
      {
        key: 'ownerUserName',
        title: t('Owner Username'),
        cell: (row) =>
          ('ownerUserName' in row ? row.ownerUserName : EMPTY_VALUE) ||
          EMPTY_VALUE,
      },
      {
        key: 'ownerDisplayName',
        title: t('Owner Display Name'),
        cell: (row) =>
          ('ownerDisplayName' in row ? row.ownerDisplayName : EMPTY_VALUE) ||
          EMPTY_VALUE,
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
    ];
    if (isUserMode)
      columns.push({
        key: 'actions',
        title: t('Actions'),
        cell: (row) => {
          if (!isUserResource(row)) return null;
          const status = getStatus(row);
          return (
            <div className="flex items-center gap-1">
              <IconActionButton
                label={t('Edit')}
                icon={<IconEdit size={15} />}
                className="h-8 w-8"
                disabled={status === 'deleted'}
                onClick={() => beginEdit(row)}
              />
              <IconActionButton
                label={status === 'stopped' ? t('Start') : t('Stop')}
                icon={
                  status === 'stopped' ? (
                    <IconDoorIn size={15} />
                  ) : (
                    <IconStopFilled size={15} />
                  )
                }
                className="h-8 w-8"
                disabled={status === 'deleted'}
                onClick={() =>
                  void changeStateRef.current(row).catch(() => undefined)
                }
              />
              <DeletePopover
                onDelete={() => removeRef.current(row)}
                tooltip={t('Delete')}
                className="h-8 w-8"
                iconSize={15}
              />
            </div>
          );
        },
      });
    return columns;
  }, [beginEdit, isUserMode, t]);

  const selectedColumns = useMemo(() => {
    const parsed = parseColumnQuery(
      getFirstQueryValue(router.query.columns),
      allColumns,
      defaultColumns,
    );
    // A URL copied from the administrator view may contain owner columns;
    // never expose those columns in the user settings view.
    const allowed = parsed.filter(
      (column) =>
        column !== 'actions' && (!isUserMode || !column.startsWith('owner')),
    );
    return allowed.length > 0 ? allowed : defaultColumns;
  }, [allColumns, defaultColumns, isUserMode, router.query.columns]);
  const visibleColumns = useMemo(() => {
    const dataColumns = allColumns.filter(
      (column) =>
        column.key !== 'actions' && selectedColumns.includes(column.key),
    );
    const actionsColumn = allColumns.find((column) => column.key === 'actions');
    return actionsColumn ? [...dataColumns, actionsColumn] : dataColumns;
  }, [allColumns, selectedColumns]);

  const pushQuery = useCallback(
    (
      nextPage: number,
      nextFilters: ContainerResourceFilters,
      nextColumns: ResourceColumnKey[],
    ) => {
      if (!router.isReady) return;
      const query: Record<string, string> = {};
      if (isUserMode && settingsTab) query.t = settingsTab;
      if (nextPage > 1) query.page = String(nextPage);
      if (nextFilters.id) query.id = nextFilters.id;
      if (nextFilters.query) query.query = nextFilters.query;
      if (!isUserMode && nextFilters.owner) query.owner = nextFilters.owner;
      if (nextFilters.runtimeNodeId)
        query.runtimeNodeId = nextFilters.runtimeNodeId;
      if (nextFilters.status) query.status = nextFilters.status;
      if (nextFilters.permanent) query.permanent = nextFilters.permanent;
      const columnsQuery = buildColumnQuery(nextColumns, defaultColumns);
      if (columnsQuery) query.columns = columnsQuery;
      void router.push({ pathname: router.pathname, query }, undefined, {
        shallow: true,
      });
    },
    [defaultColumns, isUserMode, router, settingsTab],
  );

  const committedTextFilters = useMemo(
    () => ({
      id: filters.id,
      query: filters.query,
      owner: filters.owner,
      runtimeNodeId: filters.runtimeNodeId,
    }),
    [filters.id, filters.owner, filters.query, filters.runtimeNodeId],
  );
  const commitTextFilters = useCallback(
    (next: {
      id: string;
      query: string;
      owner: string;
      runtimeNodeId: string;
    }) => pushQuery(1, { ...filters, ...next }, selectedColumns),
    [filters, pushQuery, selectedColumns],
  );
  const { draft, setDraft, flushDraft, hasPendingDraft } = useTextFilterDraft({
    committed: committedTextFilters,
    onCommit: commitTextFilters,
  });

  const loadResources = useCallback(
    async (force = false) => {
      if (!router.isReady) return;
      const fetchKey = JSON.stringify({ page, filters, mode });
      if (!force && fetchKey === lastFetchKeyRef.current) return;
      lastFetchKeyRef.current = fetchKey;
      const requestId = ++requestIdRef.current;
      setLoading(true);
      try {
        const result = isUserMode
          ? await getUserContainerResources({
              ...filters,
              page,
              pageSize: UNIFIED_TABLE_PAGE_SIZE,
            })
          : await getAdminContainerResources({
              ...filters,
              page,
              pageSize: UNIFIED_TABLE_PAGE_SIZE,
            });
        if (requestId === requestIdRef.current)
          setData(result as PageResult<ResourceRow[]>);
      } catch (error) {
        if (requestId === requestIdRef.current) lastFetchKeyRef.current = '';
        console.error(error);
      } finally {
        if (requestId === requestIdRef.current) setLoading(false);
      }
    },
    [filters, isUserMode, mode, page, router.isReady],
  );
  useEffect(() => {
    void loadResources();
  }, [loadResources]);

  const updateFilters = (next: ContainerResourceFilters) =>
    pushQuery(
      1,
      { ...next, ...draft, owner: isUserMode ? '' : draft.owner },
      selectedColumns,
    );
  const toggleColumn = (key: ResourceColumnKey, checked: boolean) => {
    if (key === 'actions') return;
    const nextSet = new Set(selectedColumns);
    if (checked) nextSet.add(key);
    else if (nextSet.size > 1) nextSet.delete(key);
    else return;
    const nextColumns = allColumns
      .map((column) => column.key)
      .filter((column) => column !== 'actions' && nextSet.has(column));
    pushQuery(
      page,
      { ...filters, ...draft, owner: isUserMode ? '' : draft.owner },
      nextColumns,
    );
  };

  const saveEdit = async () => {
    if (!editing) return;
    setSaving(true);
    try {
      await updateContainer(editing.encryptedId, {
        cpuCores: editForm.cpuCores ? Number(editForm.cpuCores) : null,
        memoryBytes: editForm.memoryBytes ? Number(editForm.memoryBytes) : null,
        maxProcesses: editForm.maxProcesses
          ? Number(editForm.maxProcesses)
          : null,
        backendNetworkName: editForm.backendNetworkName || null,
      });
      setEditing(null);
      await loadResources(true);
    } finally {
      setSaving(false);
    }
  };
  const changeState = async (resource: UserContainerResource) => {
    if (getStatus(resource) === 'deleted') return;
    if (resource.stoppedAt) await startContainer(resource.encryptedId);
    else await stopContainer(resource.encryptedId);
    await loadResources(true);
  };
  const remove = async (resource: UserContainerResource) => {
    await deleteContainer(resource.encryptedId);
    await loadResources(true);
  };
  changeStateRef.current = changeState;
  removeRef.current = remove;
  const openCreate = async () => {
    setCreateOpen(true);
    if (templates.length > 0) return;
    try {
      const items = await listContainerTemplates();
      setTemplates(items);
      setTemplateId(items[0]?.id ?? null);
    } catch (error) {
      console.error(error);
      setTemplates([]);
    }
  };
  const createPermanent = async () => {
    if (templateId == null) return;
    setCreating(true);
    try {
      await createContainer({
        name: name.trim() || null,
        isPermanent: true,
        templateId,
      });
      setName('');
      setCreateOpen(false);
      await loadResources(true);
    } finally {
      setCreating(false);
    }
  };

  const exportParams = useMemo(
    () => ({
      token: getUserSession(),
      id: filters.id || undefined,
      query: filters.query || undefined,
      owner: !isUserMode && filters.owner ? filters.owner : undefined,
      runtimeNodeId: filters.runtimeNodeId || undefined,
      status: filters.status || undefined,
      permanent: filters.permanent || undefined,
      columns: selectedColumns.join('~'),
    }),
    [filters, isUserMode, selectedColumns],
  );
  const actions = [
    ...(isUserMode
      ? [
          {
            key: 'create',
            element: (
              <Tips
                trigger={
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-9 w-9"
                    aria-label={t('Create permanent Docker')}
                    title={t('Create permanent Docker')}
                    onClick={() => void openCreate()}
                  >
                    <IconPlus size={18} />
                  </Button>
                }
                side="bottom"
                content={t('Create permanent Docker')}
              />
            ),
          },
        ]
      : []),
    {
      key: 'export',
      element: (
        <Tips
          trigger={
            <div>
              <ExportButton
                exportUrl={
                  isUserMode
                    ? USER_CONTAINER_RESOURCES_EXPORT_URL
                    : ADMIN_CONTAINER_RESOURCES_EXPORT_URL
                }
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
          allColumns={allColumns
            .filter(
              (column) =>
                column.key !== 'actions' &&
                (!isUserMode || !column.key.startsWith('owner')),
            )
            .map((column) => ({ key: column.key, title: column.title }))}
          selectedColumns={selectedColumns.filter(
            (column) => column !== 'actions',
          )}
          onToggleColumn={toggleColumn}
        />
      ),
    },
  ];

  return (
    <>
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
            {!isUserMode && (
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
            )}
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
                  onReset={() => updateFilters({ ...filters, status: '' })}
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
                  onReset={() => updateFilters({ ...filters, permanent: '' })}
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
        actions={actions}
        columns={visibleColumns}
        rows={data.rows}
        loading={loading}
        page={page}
        totalCount={data.count}
        rowKey={(row) => row.id}
        onPageChange={(nextPage) =>
          pushQuery(
            nextPage,
            { ...filters, ...draft, owner: isUserMode ? '' : draft.owner },
            selectedColumns,
          )
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
      {isUserMode && (
        <>
          <Dialog open={createOpen} onOpenChange={setCreateOpen}>
            <DialogContent className="max-w-lg">
              <DialogHeader>
                <DialogTitle>{t('Create permanent Docker')}</DialogTitle>
                <DialogDescription>
                  {t(
                    'Choose a template and an optional name for the new container.',
                  )}
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="user-container-template">
                    {t('Template')}
                  </Label>
                  <select
                    id="user-container-template"
                    className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                    value={templateId ?? ''}
                    onChange={(event) =>
                      setTemplateId(Number(event.target.value))
                    }
                    disabled={templates.length === 0}
                  >
                    {templates.map((template) => (
                      <option key={template.id} value={template.id}>
                        {template.name} · {template.runtimeNodeAIName}
                      </option>
                    ))}
                  </select>
                  {templates.length === 0 && (
                    <p className="text-sm text-muted-foreground">
                      {t('No container templates are available.')}
                    </p>
                  )}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="user-container-name">{t('Name')}</Label>
                  <Input
                    id="user-container-name"
                    value={name}
                    onChange={(event) => setName(event.target.value)}
                    placeholder={t('Optional container name')}
                  />
                </div>
                {templateId != null &&
                  (() => {
                    const selected = templates.find(
                      (template) => template.id === templateId,
                    );
                    return selected ? (
                      <p className="text-sm text-muted-foreground">
                        {selected.image} · CPU {selected.cpuCores} ·{' '}
                        {formatBytes(selected.memoryBytes)} · PID{' '}
                        {selected.maxProcesses} ·{' '}
                        {selected.backendNetworkName ??
                          t('Backend default network')}
                      </p>
                    ) : null;
                  })()}
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setCreateOpen(false)}>
                  {t('Cancel')}
                </Button>
                <Button
                  disabled={creating || templateId == null}
                  onClick={() => void createPermanent().catch(() => undefined)}
                >
                  {creating ? t('Creating...') : t('Create')}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
          <Dialog
            open={editing !== null}
            onOpenChange={(open) => !open && setEditing(null)}
          >
            <DialogContent className="max-w-lg">
              <DialogHeader>
                <DialogTitle>{t('Edit container')}</DialogTitle>
                <DialogDescription>{editing?.name}</DialogDescription>
              </DialogHeader>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="edit-container-cpu">{t('CPU cores')}</Label>
                  <Input
                    id="edit-container-cpu"
                    type="number"
                    value={editForm.cpuCores}
                    onChange={(event) =>
                      setEditForm((current) => ({
                        ...current,
                        cpuCores: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-container-memory">
                    {t('Memory bytes')}
                  </Label>
                  <Input
                    id="edit-container-memory"
                    type="number"
                    value={editForm.memoryBytes}
                    onChange={(event) =>
                      setEditForm((current) => ({
                        ...current,
                        memoryBytes: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-container-processes">
                    {t('Max processes')}
                  </Label>
                  <Input
                    id="edit-container-processes"
                    type="number"
                    value={editForm.maxProcesses}
                    onChange={(event) =>
                      setEditForm((current) => ({
                        ...current,
                        maxProcesses: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-container-network">{t('Network')}</Label>
                  <Input
                    id="edit-container-network"
                    value={editForm.backendNetworkName}
                    onChange={(event) =>
                      setEditForm((current) => ({
                        ...current,
                        backendNetworkName: event.target.value,
                      }))
                    }
                  />
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setEditing(null)}>
                  {t('Cancel')}
                </Button>
                <Button
                  disabled={saving}
                  onClick={() => void saveEdit().catch(() => undefined)}
                >
                  {saving ? t('Saving...') : t('Save')}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </>
      )}
    </>
  );
}
