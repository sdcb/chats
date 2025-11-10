import React, { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';
import { UserModelPermissionUserDto } from '@/types/adminApis';
import { PageResult, Paging } from '@/types/page';
import { getUsersForPermission } from '@/apis/adminApis';
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
  const [users, setUsers] = useState<PageResult<UserModelPermissionUserDto[]>>({
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

  /**
   * 用户模型数量的状态管理策略：
   * 
   * 1. 初始加载：从后端API获取准确的userModelCount
   * 2. 实时更新：UserModelTree组件在添加/删除模型后通过回调更新数量
   * 3. 优点：
   *    - 列表加载时就能看到模型数量（无需展开）
   *    - 分页/搜索性能好
   *    - UI响应快
   * 4. 注意：
   *    - 数量由前端维护，需要确保UserModelTree正确调用回调
   *    - 如果有其他地方修改用户模型，需要刷新列表或单独更新计数
   */

  // 增量更新模型计数（用于单个添加/删除操作）
  const updateUserModelCount = useCallback((userId: string, delta: number) => {
    setUsers((prev) => ({
      ...prev,
      rows: prev.rows.map((user) =>
        user.id.toString() === userId 
          ? { ...user, userModelCount: Math.max(0, user.userModelCount + delta) }
          : user
      ),
    }));
  }, []);

  // 直接设置准确的模型计数（用于批量操作或展开时的精确更新）
  const setUserModelCount = useCallback((userId: string, count: number) => {
    setUsers((prev) => ({
      ...prev,
      rows: prev.rows.map((user) =>
        user.id.toString() === userId ? { ...user, userModelCount: count } : user,
      ),
    }));
  }, []);

  // UserModelTree的回调接口：每次模型列表变化后调用，传入新的总数
  const handleUserModelCountChange = useCallback((userId: string, modelCount: number) => {
    setUserModelCount(userId, modelCount);
  }, [setUserModelCount]);

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
      const data = await getUsersForPermission({ query: searchQuery, ...pagination });
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
