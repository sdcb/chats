import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/router';

import { exportUsers, getUsers } from '@/apis/adminApis';
import ExportButton from '@/components/Button/ExportButtom';
import EditUserBalanceModal from '@/components/admin/Users/EditUserBalanceModel';
import UserModal from '@/components/admin/Users/UserModal';
import { IconPencil, IconRefresh, IconUserPlus } from '@/components/Icons';
import { UnifiedColumnSelector, UnifiedTable, UnifiedTableColumn, buildColumnQuery, getFirstQueryValue, parseColumnQuery, parseQueryPage, UNIFIED_TABLE_PAGE_SIZE } from '@/components/table/UnifiedTable';
import Tips from '@/components/Tips/Tips';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import useDebounce from '@/hooks/useDebounce';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';
import { GetUsersResult } from '@/types/adminApis';
import { PageResult } from '@/types/page';
import { toFixed } from '@/utils/common';

type UserLoginTypeFilter = '' | 'password' | 'phone' | 'keycloak';

type UserTableColumnKey =
  | 'id'
  | 'username'
  | 'account'
  | 'loginType'
  | 'role'
  | 'phone'
  | 'email'
  | 'balance'
  | 'modelCount'
  | 'actions';

type Filters = {
  id: string;
  username: string;
  phone: string;
  email: string;
  loginType: UserLoginTypeFilter;
};

const ALL_COLUMN_KEYS: UserTableColumnKey[] = [
  'id',
  'username',
  'account',
  'loginType',
  'role',
  'phone',
  'email',
  'balance',
  'modelCount',
  'actions',
];

const DEFAULT_COLUMNS: UserTableColumnKey[] = [
  'id',
  'username',
  'role',
  'phone',
  'email',
  'balance',
  'modelCount',
  'actions',
];

const EMPTY_FILTERS: Filters = {
  id: '',
  username: '',
  phone: '',
  email: '',
  loginType: '',
};

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

const areFiltersEqual = (left: Filters, right: Filters) =>
  left.id === right.id &&
  left.username === right.username &&
  left.phone === right.phone &&
  left.email === right.email &&
  left.loginType === right.loginType;

const parseLoginTypeFilter = (value: string | undefined): UserLoginTypeFilter =>
  value === 'password' || value === 'phone' || value === 'keycloak' ? value : '';

export default function Users() {
  const { t } = useTranslation();
  const router = useRouter();

  const [isOpenModal, setIsOpenModal] = useState({
    edit: false,
    create: false,
    recharge: false,
  });
  const [selectedUser, setSelectedUser] = useState<GetUsersResult | null>(null);
  const [users, setUsers] = useState<PageResult<GetUsersResult[]>>({
    count: 0,
    rows: [],
  });
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<Filters>(EMPTY_FILTERS);
  const [selectedColumns, setSelectedColumns] =
    useState<UserTableColumnKey[]>(DEFAULT_COLUMNS);
  const lastFetchKeyRef = useRef('');

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

  const handleClose = useCallback(() => {
    setIsOpenModal({
      edit: false,
      create: false,
      recharge: false,
    });
    setSelectedUser(null);
  }, []);

  const pushQuery = useCallback(
    (nextPage: number, nextFilters: Filters, nextColumns: UserTableColumnKey[]) => {
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
      getUsers(params)
        .then((data) => {
          setUsers(data);
        })
        .finally(() => {
          setLoading(false);
        });
    },
    [filters.email, filters.id, filters.loginType, filters.phone, filters.username, page, router.isReady],
  );

  useEffect(() => {
    refresh();
  }, [refresh]);

  const handleSuccessful = useCallback(() => {
    handleClose();
    lastFetchKeyRef.current = '';
    refresh(true);
  }, [handleClose, refresh]);

  const updateFiltersWithDebounce = useDebounce(
    (nextFilters: Filters, nextColumns: UserTableColumnKey[]) => {
      pushQuery(1, nextFilters, nextColumns);
    },
    600,
  );

  const handleTextFilterChange = useCallback(
    (key: keyof Omit<Filters, 'loginType'>, value: string) => {
      const nextFilters = {
        ...filters,
        [key]: value,
      };
      setFilters(nextFilters);
      setPage(1);
      updateFiltersWithDebounce(nextFilters, selectedColumns);
    },
    [filters, selectedColumns, updateFiltersWithDebounce],
  );

  const handleLoginTypeChange = useCallback(
    (value: UserLoginTypeFilter) => {
      const nextFilters = {
        ...filters,
        loginType: value,
      };
      setFilters(nextFilters);
      setPage(1);
      pushQuery(1, nextFilters, selectedColumns);
    },
    [filters, pushQuery, selectedColumns],
  );

  const handleShowAddModal = () => {
    setIsOpenModal({
      edit: false,
      create: true,
      recharge: false,
    });
  };

  const handleShowEditModal = (user: GetUsersResult) => {
    setSelectedUser(user);
    setIsOpenModal({
      edit: true,
      create: false,
      recharge: false,
    });
  };

  const handleShowRechargeModal = (user: GetUsersResult) => {
    setSelectedUser(user);
    setIsOpenModal({
      edit: false,
      create: false,
      recharge: true,
    });
  };

  const toggleColumn = (key: UserTableColumnKey, checked: boolean) => {
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
    setSelectedColumns(nextColumns);
    pushQuery(page, filters, nextColumns);
  };

  const allColumns = useMemo<
    UnifiedTableColumn<GetUsersResult, UserTableColumnKey>[]
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
        key: 'role',
        title: t('Role'),
        cell: (item) => item.role || '-',
      },
      {
        key: 'phone',
        title: t('Phone'),
        cell: (item) => item.phone || '-',
      },
      {
        key: 'email',
        title: t('E-Mail'),
        cell: (item) => item.email || '-',
      },
      {
        key: 'balance',
        title: t('Balance'),
        cell: (item) => (
          <button
            type="button"
            className="cursor-pointer text-left underline-offset-4 hover:underline"
            onClick={(event) => {
              event.stopPropagation();
              handleShowRechargeModal(item);
            }}
          >
            {toFixed(+item.balance)}
          </button>
        ),
      },
      {
        key: 'modelCount',
        title: t('Model Count'),
        cell: (item) => (
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Link
                  href={`/admin/user-models?username=${encodeURIComponent(item.username)}`}
                  className="cursor-pointer text-primary underline hover:text-primary/80"
                >
                  {item.userModelCount}
                </Link>
              </TooltipTrigger>
              <TooltipContent>
                <p>{t('Click to enter management page')}</p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        ),
      },
      {
        key: 'actions',
        title: t('Actions'),
        cell: (item) => (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-8 w-8 p-0"
            title={t('Edit User')}
            onClick={(event) => {
              event.stopPropagation();
              handleShowEditModal(item);
            }}
          >
            <IconPencil size={16} />
          </Button>
        ),
      },
    ],
    [getLoginTypeLabel, t],
  );

  const visibleColumns = useMemo(
    () => allColumns.filter((column) => selectedColumns.includes(column.key)),
    [allColumns, selectedColumns],
  );

  const exportColumns = selectedColumns
    .filter((column) => column !== 'actions')
    .join('~');

  return (
    <>
      <UnifiedTable
        filters={
          <>
            <Input
              className="w-[140px]"
              placeholder={t('User Id')!}
              value={filters.id}
              onChange={(event) => handleTextFilterChange('id', event.target.value)}
            />
            <Input
              className="w-[180px]"
              placeholder={t('User Name')!}
              value={filters.username}
              onChange={(event) =>
                handleTextFilterChange('username', event.target.value)
              }
            />
            <Input
              className="w-[180px]"
              placeholder={t('Phone')!}
              value={filters.phone}
              onChange={(event) => handleTextFilterChange('phone', event.target.value)}
            />
            <Input
              className="w-[200px]"
              placeholder={t('E-Mail')!}
              value={filters.email}
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
                  {filters.loginType
                    ? getLoginTypeLabel(filters.loginType)
                    : t('All')}
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
                      exportUrl={exportUsers({
                        id: filters.id || undefined,
                        username: filters.username || undefined,
                        phone: filters.phone || undefined,
                        email: filters.email || undefined,
                        loginType: filters.loginType || undefined,
                        columns: exportColumns || undefined,
                      })}
                      params={{}}
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
            key: 'add-user',
            element: (
              <Tips
                trigger={
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-9 w-9"
                    onClick={handleShowAddModal}
                    aria-label={t('Add User')}
                    title={t('Add User')}
                  >
                    <IconUserPlus size={18} />
                  </Button>
                }
                side="bottom"
                content={t('Add User')}
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
        rows={users.rows}
        loading={loading}
        page={page}
        totalCount={users.count}
        rowKey={(item) => item.id}
        onPageChange={(nextPage) => {
          setPage(nextPage);
          pushQuery(nextPage, filters, selectedColumns);
        }}
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
                <div key={item.id} className="space-y-2 rounded-md bg-card p-3 shadow-sm">
                  {visibleColumns
                    .filter((column) => column.key !== 'actions')
                    .map((column) => (
                      <div
                        key={column.key}
                        className="flex items-start justify-between gap-3 text-xs"
                      >
                        <div className="shrink-0 font-medium text-muted-foreground">
                          {column.title}
                        </div>
                        <div className="text-right text-foreground">
                          {column.cell(item)}
                        </div>
                      </div>
                    ))}
                  <div className="flex justify-end gap-2 pt-2">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => handleShowRechargeModal(item)}
                    >
                      {t('Balance')}
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => handleShowEditModal(item)}
                    >
                      <IconPencil size={14} className="mr-1" />
                      {t('Edit User')}
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )
        }
      />

      <UserModal
        user={selectedUser}
        onSuccessful={handleSuccessful}
        onClose={handleClose}
        isOpen={isOpenModal.create || isOpenModal.edit}
      />
      <EditUserBalanceModal
        onSuccessful={handleSuccessful}
        onClose={handleClose}
        userId={selectedUser?.id}
        userBalance={selectedUser?.balance}
        isOpen={isOpenModal.recharge}
      />
    </>
  );
}
