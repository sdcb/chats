import React, { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';
import { GetUsersResult } from '@/types/adminApis';
import { PageResult, Paging } from '@/types/page';
import { getUsers } from '@/apis/adminApis';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import PaginationContainer from '@/components/Pagination/Pagination';
import useDebounce from '@/hooks/useDebounce';
import UserModelTree from './UserModelTree';

interface IProps {
  focusUserId?: string;
}

export default function ByUserTab({ focusUserId }: IProps) {
  const { t } = useTranslation();
  const [users, setUsers] = useState<PageResult<GetUsersResult[]>>({
    count: 0,
    rows: [],
  });
  const [pagination, setPagination] = useState<Paging>({
    page: 1,
    pageSize: 20,
  });
  const [query, setQuery] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [expandedUserId, setExpandedUserId] = useState<string | null>(focusUserId || null);

  const handleUserModelCountChange = useCallback((userId: string, modelCount: number) => {
    setUsers((prev) => ({
      ...prev,
      rows: prev.rows.map((user) =>
        user.id === userId ? { ...user, userModelCount: modelCount } : user,
      ),
    }));
  }, []);

  useEffect(() => {
    loadUsers();
  }, [pagination]);

  useEffect(() => {
    if (focusUserId && users.rows.length > 0) {
      setExpandedUserId(focusUserId);
    }
  }, [focusUserId, users.rows]);

  const updateQueryWithDebounce = useDebounce((query: string) => {
    loadUsers(query);
  }, 1000);

  const loadUsers = async (searchQuery: string = query) => {
    try {
      setLoading(true);
      const data = await getUsers({ query: searchQuery, ...pagination });
      setUsers(data);
    } catch (error) {
      console.error('Failed to load users:', error);
      toast.error(t('Failed to load users'));
    } finally {
      setLoading(false);
    }
  };

  const handleToggleUser = (userId: string) => {
    if (expandedUserId === userId) {
      setExpandedUserId(null);
    } else {
      setExpandedUserId(userId);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex gap-4">
        <Input
          className="max-w-[300px]"
          placeholder={t('Search users...')!}
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            updateQueryWithDebounce(e.target.value);
          }}
        />
      </div>

      <Card>
        {loading ? (
          <div className="p-8 text-center text-muted-foreground">
            {t('Loading...')}
          </div>
        ) : users.rows.length === 0 ? (
          <div className="p-8 text-center text-muted-foreground">
            {t('No users found')}
          </div>
        ) : (
          <div className="divide-y">
            {users.rows.map((user) => (
              <UserModelTree
                key={user.id}
                user={user}
                isExpanded={expandedUserId === user.id.toString()}
                onToggle={() => handleToggleUser(user.id.toString())}
                onUserModelCountChange={handleUserModelCountChange}
              />
            ))}
          </div>
        )}

        {users.count > 0 && (
          <div className="border-t">
            <PaginationContainer
              page={pagination.page}
              pageSize={pagination.pageSize}
              currentCount={users.rows.length}
              totalCount={users.count}
              onPagingChange={(page, pageSize) => {
                setPagination({ page, pageSize });
              }}
            />
          </div>
        )}
      </Card>
    </div>
  );
}
