import { useEffect, useRef, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconChevronDown } from '@/components/Icons';
import PaginationContainer from '@/components/Pagination/Pagination';
import { UNIFIED_TABLE_TEXT_FILTER_DEBOUNCE_MS } from '@/components/table/useTextFilterDraft';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';

import { QuotaUserOption, searchQuotaUsers } from '@/apis/adminContainersApi';
import { cn } from '@/lib/utils';

type Props = {
  value: string;
  onChange: (value: string) => void;
  existingUserIds: Set<number>;
  disabled?: boolean;
};

const USER_PAGE_SIZE = 20;

const getUserLabel = (user: QuotaUserOption) =>
  user.displayName && user.displayName !== user.userName
    ? `${user.displayName} (${user.userName})`
    : user.userName;

export default function QuotaUserPicker({
  value,
  onChange,
  existingUserIds,
  disabled = false,
}: Props) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [users, setUsers] = useState<QuotaUserOption[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState(false);
  const [selectedUser, setSelectedUser] = useState<QuotaUserOption | null>(
    null,
  );
  const requestIdRef = useRef(0);

  useEffect(() => {
    if (!open) return;

    const requestId = ++requestIdRef.current;
    let cancelled = false;
    const load = async () => {
      if (!cancelled && requestId === requestIdRef.current) {
        setLoadError(false);
      }
      setLoading(true);
      try {
        const result = await searchQuotaUsers(
          search.trim(),
          page,
          USER_PAGE_SIZE,
        );
        if (!cancelled && requestId === requestIdRef.current) {
          setUsers(result.rows);
          setTotalCount(result.count);
        }
      } catch (error) {
        if (!cancelled && requestId === requestIdRef.current) {
          console.error('Failed to load quota users:', error);
          setUsers([]);
          setTotalCount(0);
          setLoadError(true);
        }
      } finally {
        if (!cancelled && requestId === requestIdRef.current) setLoading(false);
      }
    };

    if (!search.trim()) {
      void load();
      return () => {
        cancelled = true;
      };
    }

    const timer = window.setTimeout(
      load,
      UNIFIED_TABLE_TEXT_FILTER_DEBOUNCE_MS,
    );
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [open, page, search]);

  const handleOpenChange = (nextOpen: boolean) => {
    setOpen(nextOpen);
    if (!nextOpen) {
      setSearch('');
      setPage(1);
    }
  };

  const numericValue = Number(value);
  const selectedLabel =
    selectedUser?.id === numericValue
      ? getUserLabel(selectedUser)
      : value
      ? `#${value}`
      : t('Select a user');

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          disabled={disabled}
          className="h-10 w-full justify-between font-normal"
        >
          <span className={cn(!value && 'text-muted-foreground')}>
            {selectedLabel}
          </span>
          <IconChevronDown size={16} className="ml-2 shrink-0 opacity-60" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        align="start"
        className="w-[min(24rem,calc(100vw-2rem))] p-2"
      >
        <Input
          autoFocus
          name="quota-user-search"
          autoComplete="off"
          value={search}
          placeholder={t('Search users by name, account or ID')!}
          onChange={(event) => {
            setSearch(event.target.value);
            setPage(1);
          }}
        />
        <div className="mt-2 max-h-64 overflow-y-auto">
          {loadError ? (
            <div className="px-2 py-3 text-sm text-destructive">
              {t('Failed to load users')}
            </div>
          ) : loading ? (
            <div className="px-2 py-3 text-sm text-muted-foreground">
              {t('Loading...')}
            </div>
          ) : users.length === 0 ? (
            <div className="px-2 py-3 text-sm text-muted-foreground">
              {t('No users found')}
            </div>
          ) : (
            users.map((user) => {
              const unavailable = user.hasQuota || existingUserIds.has(user.id);
              return (
                <button
                  key={user.id}
                  type="button"
                  disabled={unavailable}
                  className="flex w-full items-center justify-between rounded-md px-2 py-2 text-left text-sm hover:bg-accent disabled:cursor-not-allowed disabled:opacity-50"
                  onClick={() => {
                    if (unavailable) return;
                    setSelectedUser(user);
                    onChange(String(user.id));
                    handleOpenChange(false);
                  }}
                >
                  <span className="min-w-0">
                    <span className="block truncate">
                      {user.displayName || user.userName}
                    </span>
                    <span className="block text-xs text-muted-foreground">
                      {t('Account')}: {user.userName} · {t('ID')}: {user.id}
                    </span>
                  </span>
                  {unavailable && (
                    <span className="ml-2 shrink-0 text-xs text-muted-foreground">
                      {t('Already has quota')}
                    </span>
                  )}
                </button>
              );
            })
          )}
        </div>
        {totalCount > USER_PAGE_SIZE && (
          <PaginationContainer
            page={page}
            pageSize={USER_PAGE_SIZE}
            currentCount={users.length}
            totalCount={totalCount}
            showPageNumbers={false}
            showTotalCount={false}
            onPagingChange={(nextPage) => setPage(nextPage)}
          />
        )}
      </PopoverContent>
    </Popover>
  );
}
