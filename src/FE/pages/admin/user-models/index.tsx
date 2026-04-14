import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { useRouter } from 'next/router';

import { getUsersForPermission } from '@/apis/adminApis';
import UserModelTree from '@/components/admin/user-models/UserModelTree';
import { IconRefresh } from '@/components/Icons';
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
import { useTextFilterDraft } from '@/components/table/useTextFilterDraft';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { Skeleton } from '@/components/ui/skeleton';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import { UserModelPermissionUserDto } from '@/types/adminApis';
import { PageResult } from '@/types/page';

type UserLoginTypeFilter = '' | 'password' | 'phone' | 'keycloak';

type UserModelTableColumnKey =
  | 'id'
  | 'username'
  | 'email'
  | 'modelCount'
  | 'phone'
  | 'account'
  | 'loginType'
  | 'status';

type Filters = {
  id: string;
  username: string;
  phone: string;
  email: string;
  loginType: UserLoginTypeFilter;
};

type TextFilters = Omit<Filters, 'loginType'>;

const ALL_COLUMN_KEYS: UserModelTableColumnKey[] = [
  'id',
  'username',
  'email',
  'modelCount',
  'phone',
  'account',
  'loginType',
  'status',
];

const DEFAULT_COLUMNS: UserModelTableColumnKey[] = [
  'id',
  'username',
  'email',
  'modelCount',
];

const EMPTY_FILTERS: Filters = {
  id: '',
  username: '',
  phone: '',
  email: '',
  loginType: '',
};

const areFiltersEqual = (left: Filters, right: Filters) =>
  left.id === right.id &&
  left.username === right.username &&
  left.phone === right.phone &&
  left.email === right.email &&
  left.loginType === right.loginType;

const parseLoginTypeFilter = (value: string | undefined): UserLoginTypeFilter =>
  value === 'password' || value === 'phone' || value === 'keycloak' ? value : '';

const pickTextFilters = (filters: Filters): TextFilters => ({
  id: filters.id,
  username: filters.username,
  phone: filters.phone,
  email: filters.email,
});

const getProviderBadgeTone = (provider: string | null | undefined) => {
  if (!provider) {
    return 'bg-muted text-muted-foreground';
  }

  const normalized = provider.toLowerCase();
  if (normalized === 'keycloak') {
    return 'bg-blue-100 text-blue-800 dark:bg-blue-950/40 dark:text-blue-300';
  }
  if (normalized === 'phone') {
    return 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300';
  }

  return 'bg-muted text-muted-foreground';
};

const getUserLoginType = (provider: string | null | undefined): Exclude<
  UserLoginTypeFilter,
  ''
> => {
  if (!provider) {
    return 'password';
  }

  const normalized = provider.toLowerCase();
  if (normalized === 'phone') {
    return 'phone';
  }
  if (normalized === 'keycloak') {
    return 'keycloak';
  }

  return 'password';
};

export default function UserModelsPage() {
  const { t } = useTranslation();
  const router = useRouter();

  const [users, setUsers] = useState<PageResult<UserModelPermissionUserDto[]>>({
    count: 0,
    rows: [],
  });
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<Filters>(EMPTY_FILTERS);
  const [selectedColumns, setSelectedColumns] =
    useState<UserModelTableColumnKey[]>(DEFAULT_COLUMNS);
  const [selectedUserId, setSelectedUserId] = useState<number | null>(null);
  const lastFetchKeyRef = useRef('');
  const autoOpenedUsernameRef = useRef('');

  const getLoginTypeLabel = useCallback(
    (value: UserLoginTypeFilter | null | undefined) => {
      switch (value) {
        case 'phone':
          return t('Phone');
        case 'keycloak':
          return 'Keycloak';
        case 'password':
        default:
          return t('Account password login');
      }
    },
    [t],
  );

  const pushQuery = useCallback(
    (
      nextPage: number,
      nextFilters: Filters,
      nextColumns: UserModelTableColumnKey[],
    ) => {
      if (!router.isReady) {
        return;
      }

      const query: Record<string, string> = {};
      if (nextPage > 1) {
        query.page = nextPage.toString();
      }
      if (nextFilters.id) {
        query.id = nextFilters.id;
      }
      if (nextFilters.username) {
        query.username = nextFilters.username;
      }
      if (nextFilters.phone) {
        query.phone = nextFilters.phone;
      }
      if (nextFilters.email) {
        query.email = nextFilters.email;
      }
      if (nextFilters.loginType) {
        query.loginType = nextFilters.loginType;
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

    const nextPage = parseQueryPage(getFirstQueryValue(router.query.page));
    const nextFilters: Filters = {
      id: getFirstQueryValue(router.query.id) || '',
      username: getFirstQueryValue(router.query.username) || '',
      phone: getFirstQueryValue(router.query.phone) || '',
      email: getFirstQueryValue(router.query.email) || '',
      loginType: parseLoginTypeFilter(getFirstQueryValue(router.query.loginType)),
    };
    const nextColumns = parseColumnQuery(
      getFirstQueryValue(router.query.columns),
      ALL_COLUMN_KEYS.map((key) => ({ key })),
      DEFAULT_COLUMNS,
    );

    setPage((prev) => (prev === nextPage ? prev : nextPage));
    setFilters((prev) => (areFiltersEqual(prev, nextFilters) ? prev : nextFilters));
    setSelectedColumns((prev) =>
      prev.join(',') === nextColumns.join(',') ? prev : nextColumns,
    );
  }, [router.isReady, router.query]);

  const refresh = useCallback(
    (force = false) => {
      if (!router.isReady) {
        return;
      }

      const params = {
        page,
        pageSize: UNIFIED_TABLE_PAGE_SIZE,
        id: filters.id || undefined,
        username: filters.username || undefined,
        phone: filters.phone || undefined,
        email: filters.email || undefined,
        loginType: filters.loginType || undefined,
      };
      const fetchKey = JSON.stringify(params);
      if (!force && fetchKey === lastFetchKeyRef.current) {
        return;
      }

      lastFetchKeyRef.current = fetchKey;
      setLoading(true);
      getUsersForPermission(params)
        .then((data) => {
          setUsers(data);
        })
        .finally(() => {
          setLoading(false);
        });
    },
    [
      filters.email,
      filters.id,
      filters.loginType,
      filters.phone,
      filters.username,
      page,
      router.isReady,
    ],
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

  const handleTextFilterChange = useCallback(
    (key: keyof TextFilters, value: string) => {
      setDraft((prev) => ({
        ...prev,
        [key]: value,
      }));
    },
    [setDraft],
  );

  const handleLoginTypeChange = useCallback(
    (value: UserLoginTypeFilter) => {
      pushQuery(
        1,
        {
          ...filters,
          ...draft,
          loginType: value,
        },
        selectedColumns,
      );
    },
    [draft, filters, pushQuery, selectedColumns],
  );

  const handleUserModelCountChange = useCallback((userId: string, modelCount: number) => {
    const parsedId = parseInt(userId, 10);
    setUsers((prev) => ({
      ...prev,
      rows: prev.rows.map((user) =>
        user.id === parsedId ? { ...user, userModelCount: modelCount } : user,
      ),
    }));
  }, []);

  const toggleColumn = (key: UserModelTableColumnKey, checked: boolean) => {
    const nextSet = new Set(selectedColumns);
    if (checked) {
      nextSet.add(key);
    } else {
      nextSet.delete(key);
      if (nextSet.size === 0) {
        return;
      }
    }

    const nextColumns = ALL_COLUMN_KEYS.filter((column) => nextSet.has(column));
    pushQuery(
      page,
      {
        ...filters,
        ...draft,
      },
      nextColumns,
    );
  };

  const openUserDrawer = useCallback((user: UserModelPermissionUserDto) => {
    setSelectedUserId(user.id);
  }, []);

  const allColumns = useMemo<
    UnifiedTableColumn<UserModelPermissionUserDto, UserModelTableColumnKey>[]
  >(
    () => [
      {
        key: 'id',
        title: t('User Id'),
        cell: (item) => item.id,
      },
      {
        key: 'username',
        title: t('User Name'),
        cell: (item) => (
          <div className="flex items-center gap-2">
            <div
              className={cn(
                'h-2 w-2 rounded-full',
                item.enabled ? 'bg-green-400' : 'bg-gray-400',
              )}
            />
            <span>{item.username}</span>
          </div>
        ),
      },
      {
        key: 'email',
        title: t('E-Mail'),
        cell: (item) => item.email || '-',
      },
      {
        key: 'modelCount',
        title: t('Model Count'),
        cell: (item) => (
          <button
            type="button"
            className="cursor-pointer text-left text-primary underline underline-offset-4 hover:text-primary/80"
            onClick={(event) => {
              event.stopPropagation();
              openUserDrawer(item);
            }}
          >
            {item.userModelCount}
          </button>
        ),
      },
      {
        key: 'phone',
        title: t('Phone'),
        cell: (item) => item.phone || '-',
      },
      {
        key: 'account',
        title: t('Account'),
        cell: (item) => item.account || '-',
      },
      {
        key: 'loginType',
        title: t('Login Type'),
        cell: (item) => {
          const loginType = getUserLoginType(item.provider);
          return (
            <Badge className={cn('capitalize', getProviderBadgeTone(item.provider))}>
              {getLoginTypeLabel(loginType)}
            </Badge>
          );
        },
      },
      {
        key: 'status',
        title: t('Status'),
        cell: (item) =>
          item.enabled ? (
            <Badge className="bg-green-600">{t('Enabled')}</Badge>
          ) : (
            <Badge variant="secondary">{t('Disabled')}</Badge>
          ),
      },
    ],
    [getLoginTypeLabel, openUserDrawer, t],
  );

  const visibleColumns = useMemo(
    () => allColumns.filter((column) => selectedColumns.includes(column.key)),
    [allColumns, selectedColumns],
  );

  const selectedUser = useMemo(
    () => users.rows.find((item) => item.id === selectedUserId) || null,
    [selectedUserId, users.rows],
  );

  useEffect(() => {
    if (selectedUserId === null) {
      return;
    }

    if (users.rows.length > 0 && !users.rows.some((item) => item.id === selectedUserId)) {
      setSelectedUserId(null);
    }
  }, [selectedUserId, users.rows]);

  const focusUsername = getFirstQueryValue(router.query.username) || '';

  useEffect(() => {
    autoOpenedUsernameRef.current = '';
  }, [focusUsername]);

  useEffect(() => {
    if (!focusUsername || users.rows.length === 0) {
      return;
    }

    if (autoOpenedUsernameRef.current === focusUsername) {
      return;
    }

    const normalized = focusUsername.trim().toLowerCase();
    const matchedUser =
      users.rows.find(
        (item) =>
          item.username.toLowerCase() === normalized ||
          item.account.toLowerCase() === normalized,
      ) || users.rows[0];

    if (matchedUser) {
      setSelectedUserId(matchedUser.id);
    }
    autoOpenedUsernameRef.current = focusUsername;
  }, [focusUsername, users.rows]);

  return (
    <>
      <UnifiedTable
        filters={
          <>
            <Input
              className="w-[140px]"
              placeholder={t('User Id')!}
              value={draft.id}
              onChange={(event) => handleTextFilterChange('id', event.target.value)}
            />
            <Input
              className="w-[180px]"
              placeholder={t('User Name')!}
              value={draft.username}
              onChange={(event) =>
                handleTextFilterChange('username', event.target.value)
              }
            />
            <Input
              className="w-[180px]"
              placeholder={t('Phone')!}
              value={draft.phone}
              onChange={(event) => handleTextFilterChange('phone', event.target.value)}
            />
            <Input
              className="w-[200px]"
              placeholder={t('E-Mail')!}
              value={draft.email}
              onChange={(event) => handleTextFilterChange('email', event.target.value)}
            />
            <div className="w-[180px]">
              <Select
                value={filters.loginType}
                onValueChange={(value) =>
                  handleLoginTypeChange(value as UserLoginTypeFilter)
                }
              >
                <SelectTrigger
                  onReset={() => handleLoginTypeChange('')}
                  value={filters.loginType}
                >
                  {filters.loginType ? getLoginTypeLabel(filters.loginType) : t('All')}
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="password">
                    {t('Account password login')}
                  </SelectItem>
                  <SelectItem value="phone">{t('Phone')}</SelectItem>
                  <SelectItem value="keycloak">Keycloak</SelectItem>
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
        rows={users.rows}
        loading={loading}
        page={page}
        totalCount={users.count}
        rowKey={(item) => item.id}
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
        onRowClick={openUserDrawer}
        mobileContent={
          loading ? (
            <div className="space-y-2">
              {Array.from({ length: 4 }).map((_, index) => (
                <Skeleton key={index} className="h-32 w-full" />
              ))}
            </div>
          ) : users.rows.length === 0 ? (
            <div className="py-4 text-center text-sm text-muted-foreground">
              {t('No data')}
            </div>
          ) : (
            <div className="space-y-2">
              {users.rows.map((item) => (
                <div
                  key={item.id}
                  className="space-y-2 rounded-md bg-card p-3 text-left shadow-sm"
                  onClick={() => openUserDrawer(item)}
                >
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

      <Sheet
        open={selectedUser !== null}
        onOpenChange={(open) => {
          if (!open) {
            setSelectedUserId(null);
          }
        }}
      >
        <SheetContent side="right" className="w-full p-0 sm:max-w-4xl">
          {selectedUser && (
            <div className="flex h-full flex-col">
              <SheetHeader className="border-b px-6 py-5">
                <SheetTitle>{selectedUser.username}</SheetTitle>
                <SheetDescription>
                  {selectedUser.account}
                  {selectedUser.email ? ` · ${selectedUser.email}` : ''}
                </SheetDescription>
              </SheetHeader>
              <div className="flex-1 overflow-y-auto px-6 py-5">
                <UserModelTree
                  key={selectedUser.id}
                  user={selectedUser}
                  isExpanded
                  onToggle={() => undefined}
                  onUserModelCountChange={handleUserModelCountChange}
                  contentOnly
                />
              </div>
            </div>
          )}
        </SheetContent>
      </Sheet>
    </>
  );
}
