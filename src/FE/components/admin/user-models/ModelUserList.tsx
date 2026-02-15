import React, { useCallback, useEffect, useState, useRef } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';
import { ModelUserPermissionDto, AdminModelDto } from '@/types/adminApis';
import { PageResult, Paging } from '@/types/page';
import { 
  getUsersByModel, 
  addUserModel, 
  deleteUserModel, 
  editUserModel,
  batchAddUserModelsByModel,
  batchDeleteUserModelsByModel
} from '@/apis/adminApis';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { TriStateCheckbox } from '@/components/ui/tristate-checkbox';
import { cn } from '@/lib/utils';
import { IconLoader, IconPencil } from '@/components/Icons';
import PaginationContainer from '@/components/Pagination/Pagination';
import EditUserModelDialog from './EditUserModelDialog';
import { TooltipProvider, Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip';

interface IProps {
  model: AdminModelDto;
  isExpanded: boolean;
  onToggle: () => void;
  onAssignedUserCountChange?: (modelId: number, assignedUserCount: number) => void;
  onCheckStateChange?: (state: 'checked' | 'unchecked' | 'indeterminate' | 'hidden') => void;
  onBatchToggleComplete?: () => void;
  batchPending?: boolean;
  triggerBatchToggle?: number;
}

export default function ModelUserList({ model, isExpanded, onToggle, onAssignedUserCountChange, onCheckStateChange, onBatchToggleComplete, batchPending: externalBatchPending, triggerBatchToggle }: IProps) {
  const { t } = useTranslation();
  const modelId = model.modelId;

  const [loading, setLoading] = useState(false);
  const [users, setUsers] = useState<PageResult<ModelUserPermissionDto[]>>({
    count: 0,
    rows: [],
  });
  const [pagination, setPagination] = useState<Paging>({
    page: 1,
    pageSize: 20,
  });
  const [pendingUsers, setPendingUsers] = useState<Set<number>>(new Set());
  const [internalBatchPending, setInternalBatchPending] = useState(false);
  const [editingUser, setEditingUser] = useState<ModelUserPermissionDto | null>(null);
  const [editDialogOpen, setEditDialogOpen] = useState(false);

  const batchPending = externalBatchPending ?? internalBatchPending;

  useEffect(() => {
    if (isExpanded) {
      loadUsers();
    }
  }, [isExpanded, pagination]);

  const loadUsers = async () => {
    try {
      setLoading(true);
      const data = await getUsersByModel(modelId, {
        page: pagination.page,
        pageSize: pagination.pageSize,
      });
      setUsers(data);
    } catch (error) {
      console.error('Failed to load users:', error);
      toast.error(t('Failed to load data'));
    } finally {
      setLoading(false);
    }
  };

  // 计算当前页已分配用户数
  const assignedUserCount = users.rows.filter(u => u.isAssigned).length;
  const currentPageUserCount = users.rows.length;

  // 添加单个用户
  const handleAddUser = async (user: ModelUserPermissionDto) => {
    if (pendingUsers.has(user.userId) || user.isAssigned) return;

    setPendingUsers(prev => new Set(prev).add(user.userId));

    try {
      const expiresAt = new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString();
      const result = await addUserModel({
        userId: user.userId,
        modelId: modelId,
        tokens: 0,
        counts: 0,
        expires: expiresAt,
      });

      // 更新本地用户状态
      if (result.model) {
        setUsers(prev => ({
          ...prev,
          rows: prev.rows.map(u =>
            u.userId === user.userId
              ? {
                  ...u,
                  isAssigned: true,
                  userModelId: result.model!.userModelId,
                  counts: result.model!.counts,
                  tokens: result.model!.tokens,
                  expires: result.model!.expires,
                }
              : u
          ),
        }));
      }

      onAssignedUserCountChange?.(modelId, result.userModelCount);
    } catch (error) {
      console.error('Failed to add user:', error);
      toast.error(t('Failed to add user'));
    } finally {
      setPendingUsers(prev => {
        const next = new Set(prev);
        next.delete(user.userId);
        return next;
      });
    }
  };

  // 删除单个用户
  const handleDeleteUser = async (user: ModelUserPermissionDto) => {
    if (pendingUsers.has(user.userId) || !user.isAssigned || !user.userModelId) return;

    setPendingUsers(prev => new Set(prev).add(user.userId));

    const userModelId = user.userModelId;

    try {
      const result = await deleteUserModel(userModelId);

      // 更新本地用户状态
      if (result.model) {
        setUsers(prev => ({
          ...prev,
          rows: prev.rows.map(u =>
            u.userId === user.userId
              ? {
                  ...u,
                  isAssigned: false,
                  userModelId: null,
                  counts: null,
                  tokens: null,
                  expires: null,
                }
              : u
          ),
        }));
      }

      onAssignedUserCountChange?.(modelId, result.userModelCount);
    } catch (error) {
      console.error('Failed to delete user:', error);
      toast.error(t('Failed to remove user'));
    } finally {
      setPendingUsers(prev => {
        const next = new Set(prev);
        next.delete(user.userId);
        return next;
      });
    }
  };

  // 使用 ref 来跟踪上次的 triggerBatchToggle 值
  const prevTriggerRef = useRef(0);

  // 批量操作：基于当前页的用户列表
  const handleBatchToggle = async () => {
    // 当前页全部已分配，则批量删除；否则批量添加
    const isFullyAssigned = assignedUserCount === currentPageUserCount;
    const userIds = users.rows.map(u => u.userId);

    if (userIds.length === 0) {
      onBatchToggleComplete?.();
      return;
    }

    setInternalBatchPending(true);

    try {
      if (isFullyAssigned) {
        await batchDeleteUserModelsByModel({ modelId, userIds });
      } else {
        await batchAddUserModelsByModel({ modelId, userIds });
      }

      // 重新加载用户列表以获取最新的分配状态
      await loadUsers();

      // 通知父组件更新用户数量（使用重新加载后的数据）
      const updatedAssignedCount = users.rows.filter(u => u.isAssigned).length;
      onAssignedUserCountChange?.(modelId, updatedAssignedCount);
    } catch (error) {
      console.error('Batch operation failed:', error);
      toast.error(isFullyAssigned ? t('Failed to remove users') : t('Failed to add users'));
    } finally {
      setInternalBatchPending(false);
      onBatchToggleComplete?.();
    }
  };

  // 监听外部触发的批量操作
  useEffect(() => {
    if (triggerBatchToggle && triggerBatchToggle > 0 && triggerBatchToggle !== prevTriggerRef.current) {
      prevTriggerRef.current = triggerBatchToggle;
      handleBatchToggle();
    }
  }, [triggerBatchToggle]);

  const getCheckState = (): 'checked' | 'unchecked' | 'indeterminate' | 'hidden' => {
    // 如果用户列表还未加载，返回hidden状态
    if (loading || currentPageUserCount === 0) return 'hidden';
    if (assignedUserCount === 0) return 'unchecked';
    if (assignedUserCount === currentPageUserCount) return 'checked';
    return 'indeterminate';
  };

  const checkState = getCheckState();

  // 通知父组件 checkState 的变化
  useEffect(() => {
    onCheckStateChange?.(checkState);
  }, [checkState, onCheckStateChange]);

  // 计算过期时间显示
  const getExpiresDisplay = (expires: string | null | undefined) => {
    if (!expires) return { 
      text: t('No expiration'), 
      shortText: '-', 
      color: 'text-muted-foreground', 
      isExpired: false 
    };
    
    const expireDate = new Date(expires);
    const now = new Date();
    const diffMs = expireDate.getTime() - now.getTime();
    const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays < 0) {
      return { 
        text: t('Expired'), 
        shortText: t('Expired'), 
        color: 'text-red-500', 
        isExpired: true 
      };
    } else if (diffDays === 0) {
      return { 
        text: t('Expires today'), 
        shortText: '0d', 
        color: 'text-orange-500', 
        isExpired: false 
      };
    } else if (diffDays === 1) {
      return { 
        text: t('1 day left'), 
        shortText: '1d', 
        color: 'text-orange-500', 
        isExpired: false 
      };
    } else {
      return { 
        text: `${diffDays} ${t('days left')}`, 
        shortText: `${diffDays}d`, 
        color: 'text-muted-foreground', 
        isExpired: false 
      };
    }
  };

  // 格式化过期时间用于tooltip
  const formatExpiresForTooltip = (expires: string | null | undefined) => {
    if (!expires) return '';
    return new Date(expires).toLocaleString();
  };

  // 编辑用户模型
  const handleEditUser = (user: ModelUserPermissionDto) => {
    setEditingUser(user);
    setEditDialogOpen(true);
  };

  // 保存编辑
  const handleSaveEdit = async (tokensDelta: number, countsDelta: number, expires: string) => {
    if (!editingUser || !editingUser.userModelId) return;

    try {
      const result = await editUserModel(editingUser.userModelId, {
        tokensDelta,
        countsDelta,
        expires,
      });

      // 更新本地用户状态
      if (result.model) {
        setUsers(prev => ({
          ...prev,
          rows: prev.rows.map(u =>
            u.userId === editingUser.userId
              ? {
                  ...u,
                  counts: result.model!.counts,
                  tokens: result.model!.tokens,
                  expires: result.model!.expires,
                }
              : u
          ),
        }));
      }

      onAssignedUserCountChange?.(modelId, result.userModelCount);
    } catch (error) {
      console.error('Failed to update user model:', error);
      toast.error(t('Failed to update user model'));
      throw error;
    }
  };

  return (
    <>
      <div 
        className={cn(
          "grid transition-all duration-300 ease-in-out",
          isExpanded ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
        )}
      >
        <div className="overflow-hidden">
          <div className="pl-6 pr-3 pt-2 pb-2">
            {loading ? (
              <div className="space-y-1">
                {Array.from({ length: 5 }).map((_, idx) => (
                  <div key={`skeleton-${idx}`} className="flex items-center justify-between p-2 rounded bg-muted/30">
                    <div className="flex items-center gap-2">
                      <Skeleton className="h-4 w-4 rounded-full" />
                      <Skeleton className="h-3 w-32" />
                    </div>
                  </div>
                ))}
              </div>
            ) : users.rows.length === 0 ? (
              <div className="text-sm text-muted-foreground text-center py-4">
                {t('No users found')}
              </div>
            ) : (
              <>
                <div className="space-y-1">
                  {users.rows.map(user => {
                    const isPending = pendingUsers.has(user.userId);
                    const isDisabled = isPending || batchPending;
                    const expiresDisplay = getExpiresDisplay(user.expires);

                    return (
                      <div
                        key={user.userId}
                        className={cn(
                          "flex items-center justify-between p-2 rounded text-xs transition-colors",
                          user.isAssigned && "bg-green-50 dark:bg-green-950/20",
                          !user.isAssigned && "bg-muted/30",
                          isDisabled && "opacity-50"
                        )}
                      >
                        <div className="flex items-center gap-2 flex-1 min-w-0">
                          {isPending ? (
                            <IconLoader size={14} className="animate-spin text-muted-foreground flex-shrink-0" />
                          ) : (
                            <TriStateCheckbox
                              state={user.isAssigned ? 'checked' : 'unchecked'}
                              size="md"
                              onClick={(e) => {
                                e.stopPropagation();
                                if (!isDisabled) {
                                  if (user.isAssigned) {
                                    handleDeleteUser(user);
                                  } else {
                                    handleAddUser(user);
                                  }
                                }
                              }}
                              disabled={isDisabled}
                              className="flex-shrink-0"
                            />
                          )}
                          <div className="flex items-center gap-2 flex-1 min-w-0">
                            <span className={cn(
                              "truncate",
                              user.isAssigned ? "font-medium" : "text-muted-foreground"
                            )}>
                              {user.username}
                            </span>
                            {!user.enabled && (
                              <Badge variant="outline" className="text-[10px] px-1 py-0">
                                {t('Disabled')}
                              </Badge>
                            )}
                          </div>
                        </div>
                        {user.isAssigned && (
                          <div className="flex items-center gap-2 sm:gap-3 flex-shrink-0">
                            <TooltipProvider>
                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <div className="flex items-center gap-1 text-[10px] sm:text-xs">
                                    <span className="hidden sm:inline text-muted-foreground">{t('Counts')}:</span>
                                    <span>{user.counts ?? 0}</span>
                                  </div>
                                </TooltipTrigger>
                                <TooltipContent>{t('Usage count')}: {user.counts ?? 0}</TooltipContent>
                              </Tooltip>
                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <div className="flex items-center gap-1 text-[10px] sm:text-xs">
                                    <span className="hidden sm:inline text-muted-foreground">{t('Tokens')}:</span>
                                    <span>{user.tokens ?? 0}</span>
                                  </div>
                                </TooltipTrigger>
                                <TooltipContent>{t('Token count')}: {user.tokens ?? 0}</TooltipContent>
                              </Tooltip>
                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <div className={cn("text-[10px] sm:text-xs", expiresDisplay.color)}>
                                    <span className="hidden sm:inline">{expiresDisplay.text}</span>
                                    <span className="sm:hidden">{expiresDisplay.shortText}</span>
                                  </div>
                                </TooltipTrigger>
                                <TooltipContent>
                                  {formatExpiresForTooltip(user.expires) || t('No expiration')}
                                </TooltipContent>
                              </Tooltip>
                            </TooltipProvider>
                            <button
                              type="button"
                              className="flex items-center justify-center p-1 hover:bg-muted rounded transition-colors"
                              onClick={(e) => {
                                e.stopPropagation();
                                if (!isDisabled) {
                                  handleEditUser(user);
                                }
                              }}
                              disabled={isDisabled}
                            >
                              <IconPencil size={14} className="text-muted-foreground hover:text-foreground" />
                            </button>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
                
                {users.count > pagination.pageSize && (
                  <div className="mt-3">
                    <PaginationContainer
                      page={pagination.page}
                      pageSize={pagination.pageSize}
                      currentCount={users.rows.length}
                      totalCount={users.count}
                      onPagingChange={(page: number, pageSize: number) => {
                        setPagination({ page, pageSize });
                      }}
                    />
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      </div>

      {editingUser && (
        <EditUserModelDialog
          open={editDialogOpen}
          onOpenChange={setEditDialogOpen}
          model={{
            modelId: modelId,
            name: model.name,
            isAssigned: editingUser.isAssigned,
            userModelId: editingUser.userModelId,
            counts: editingUser.counts,
            tokens: editingUser.tokens,
            expires: editingUser.expires,
            isDeleted: false,
          }}
          onSave={handleSaveEdit}
        />
      )}
    </>
  );
}
