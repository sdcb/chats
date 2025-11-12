import React, { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { useRouter } from 'next/router';
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
  focusUsername?: string;
  queryParam?: string;
}

export default function ByUserTab({ focusUsername, queryParam }: IProps) {
  const { t } = useTranslation();
  const router = useRouter();
  const [users, setUsers] = useState<PageResult<UserModelPermissionUserDto[]>>({
    count: 0,
    rows: [],
  });
  const [pagination, setPagination] = useState<Paging>({
    page: 1,
    pageSize: 20,
  });
  // 使用 focusUsername 或 queryParam 作为初始查询值
  const [query, setQuery] = useState<string>(focusUsername || queryParam || '');
  const [loading, setLoading] = useState(true);
  const [expandedUsername, setExpandedUsername] = useState<string | null>(focusUsername || null);

  /**
   * 用户模型数量的状态管理策略：
   * 
   * 1. 初始加载：从后端 API 获取每个用户的模型总数（userModelCount）
   * 2. 实时更新：UserModelTree 组件在执行模型操作（添加/删除/批量操作）后，
   *    后端会返回最新的准确总数，通过 onUserModelCountChange 回调同步更新
   * 3. 数据来源：
   *    - 所有模型数量都来自后端，前端只负责显示和更新状态
   *    - 后端在每次操作后重新统计并返回准确的 userModelCount
   * 4. 优点：
   *    - 用户列表加载时就能看到模型数量（无需展开树）
   *    - 数据始终准确，不会出现前端计算误差
   *    - 支持分页/搜索，性能好
   * 5. 注意：
   *    - 如果通过其他途径修改了用户模型（如直接操作数据库），需要刷新列表才能看到最新数量
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
  }, [pagination, query]);

  // focusUsername 变化时更新 expandedUsername
  useEffect(() => {
    if (focusUsername) {
      setExpandedUsername(focusUsername);
    }
  }, [focusUsername]);

  // 更新 URL 中的 query 参数
  const updateQueryInUrl = useCallback((newQuery: string) => {
    if (!router.isReady) return;

    const nextQuery: Record<string, string> = {};
    
    Object.entries(router.query).forEach(([key, value]) => {
      if (key === 'query') return; // 跳过旧的 query，稍后会添加新的
      
      if (typeof value === 'string' && value) {
        nextQuery[key] = value;
      } else if (Array.isArray(value) && value.length > 0) {
        nextQuery[key] = value[0];
      }
    });

    // 如果有查询内容，添加到 URL
    if (newQuery) {
      nextQuery.query = newQuery;
    }

    router.push(
      {
        pathname: router.pathname,
        query: nextQuery,
      },
      undefined,
      { shallow: true }
    );
  }, [router]);

  const updateQueryWithDebounce = useDebounce((query: string) => {
    updateQueryInUrl(query);
  }, 1000);

  const loadUsers = async () => {
    try {
      setLoading(true);
      // 使用本地 query 状态作为查询条件
      const data = await getUsersForPermission({ query, ...pagination });
      setUsers(data);
    } catch (error) {
      console.error('Failed to load users:', error);
      toast.error(t('Failed to load users'));
    } finally {
      setLoading(false);
    }
  };

  const handleToggleUser = (username: string) => {
    if (expandedUsername === username) {
      setExpandedUsername(null);
    } else {
      setExpandedUsername(username);
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
                isExpanded={expandedUsername === user.username}
                onToggle={() => handleToggleUser(user.username)}
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
