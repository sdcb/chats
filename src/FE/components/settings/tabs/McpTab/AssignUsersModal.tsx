import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import {
  UnassignedUserDto,
  AssignedUserDetailsDto,
  AssignUsersToMcpRequest,
  AssignedUserInfo,
} from '@/types/clientApis';

import {
  IconPlus,
  IconSearch,
  IconX,
} from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

import {
  getUnassignedUsers,
  getAssignedUserDetails,
  assignUsersToMcp,
} from '@/apis/clientApis';
import { isEmptyOrJsonObject } from '@/utils/json';

interface AssignUsersModalProps {
  isOpen: boolean;
  onClose: () => void;
  mcpId: number | null;
  onSuccess: () => void;
  isAdmin: boolean;
}

interface AssignedUser extends AssignedUserDetailsDto {
  originalCustomHeaders?: string; // 用于跟踪原始值
  isNew?: boolean; // 标记是否为新分配的用户
}

const AssignUsersModal = ({ isOpen, onClose, mcpId, onSuccess, isAdmin }: AssignUsersModalProps) => {
  const { t } = useTranslation();
  const [searchTerm, setSearchTerm] = useState('');
  const [unassignedUsers, setUnassignedUsers] = useState<UnassignedUserDto[]>([]);
  const [assignedUsers, setAssignedUsers] = useState<AssignedUser[]>([]);
  const [originalAssignedUserIds, setOriginalAssignedUserIds] = useState<Set<number>>(new Set());
  const [loading, setLoading] = useState(false);
  const [searchLoading, setSearchLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (isOpen && mcpId) {
      loadData();
      // 初始搜索
      searchUsers('');
    }
  }, [isOpen, mcpId]);

  // 搜索用户的防抖效果
  useEffect(() => {
    const timer = setTimeout(() => {
      if (mcpId) {
        searchUsers(searchTerm);
      }
    }, 300);

    return () => clearTimeout(timer);
  }, [searchTerm, mcpId]);

  const loadData = async () => {
    if (!mcpId) return;

    setLoading(true);
    try {
      // 只加载已分配用户详情
      const assigned = await getAssignedUserDetails(mcpId);

      // 为已分配用户添加原始customHeaders用于变更跟踪
      const assignedWithOriginal = assigned.map(user => ({
        ...user,
        originalCustomHeaders: user.customHeaders,
        isNew: false
      }));
      setAssignedUsers(assignedWithOriginal);

      // 记录原始已分配用户ID
      const originalIds = new Set(assigned.map(user => user.id));
      setOriginalAssignedUserIds(originalIds);
    } catch (error) {
      console.error('Failed to load user data:', error);
      toast.error(t('Failed to load user data'));
    } finally {
      setLoading(false);
    }
  };

  const searchUsers = async (search: string) => {
    if (!mcpId) return;

    setSearchLoading(true);
    try {
      if (isAdmin) {
        const unassigned = await getUnassignedUsers(mcpId, search, 10);
        setUnassignedUsers(unassigned);
      }
    } catch (error) {
      console.error('Failed to search users:', error);
      toast.error(t('Failed to load user data'));
      setUnassignedUsers([]);
    } finally {
      setSearchLoading(false);
    }
  };

  const handleAssignUser = (user: UnassignedUserDto) => {
    // 从未分配列表移除
    setUnassignedUsers(prev => prev.filter(u => u.id !== user.id));

    // 添加到已分配列表的第一位
    const newAssignedUser: AssignedUser = {
      id: user.id,
      userName: user.userName,
      customHeaders: '',
      originalCustomHeaders: undefined, // 标记为新添加
      isNew: true
    };
    setAssignedUsers(prev => [newAssignedUser, ...prev]);
  };

  const handleUnassignUser = (user: AssignedUser) => {
    // 从已分配列表移除
    setAssignedUsers(prev => prev.filter(u => u.id !== user.id));

    // 如果是原本就存在的用户，重新搜索用户列表以包含这个用户
    if (!user.isNew) {
      searchUsers(searchTerm);
    }
  };

  const handleCustomHeadersChange = (userId: number, customHeaders: string) => {
    setAssignedUsers(prev =>
      prev.map(user =>
        user.id === userId
          ? { ...user, customHeaders }
          : user
      )
    );
  };

  const getChanges = (): AssignUsersToMcpRequest => {
    const toAssignedUsers: AssignedUserInfo[] = [];
    const toUpdateUsers: AssignedUserInfo[] = [];
    const toDeleteUserIds: number[] = [];

    // 获取当前已分配用户的ID集合
    const currentAssignedIds = new Set(assignedUsers.map(user => user.id));

    // 检查所有当前已分配的用户
    assignedUsers.forEach(user => {
      if (user.isNew) {
        // 新分配的用户
        toAssignedUsers.push({
          id: user.id,
          customHeaders: user.customHeaders || undefined
        });
      } else if (user.customHeaders !== user.originalCustomHeaders) {
        // 修改过customHeaders的用户
        toUpdateUsers.push({
          id: user.id,
          customHeaders: user.customHeaders || undefined
        });
      }
    });

    // 找出被删除的用户（原本分配但现在不在当前分配列表中的）
    originalAssignedUserIds.forEach(originalId => {
      if (!currentAssignedIds.has(originalId)) {
        toDeleteUserIds.push(originalId);
      }
    });

    return {
      toAssignedUsers,
      toUpdateUsers,
      toDeleteUserIds
    };
  };

  const handleSave = async () => {
    if (!mcpId) return;

    const changes = getChanges();

    // 校验每个用户的 Custom Headers：必须为空白或合法 JSON 对象
    for (const u of assignedUsers) {
      if (u.customHeaders && !isEmptyOrJsonObject(u.customHeaders)) {
        toast.error(t('Headers must be empty or a valid JSON object'));
        return;
      }
    }

    // 如果没有任何变更，直接关闭
    if (changes.toAssignedUsers.length === 0 &&
      changes.toUpdateUsers.length === 0 &&
      changes.toDeleteUserIds.length === 0) {
      toast.success(t('No changes to save'));
      onClose();
      return;
    }

    setSaving(true);
    try {
      await assignUsersToMcp(mcpId, changes);
      toast.success(t('User assignments saved successfully'));
      onSuccess();
      onClose();
    } catch (error) {
      console.error('Failed to save user assignments:', error);
      toast.error(t('Failed to save user assignments'));
    } finally {
      setSaving(false);
    }
  };

  const handleClose = () => {
    // 重置状态
    setSearchTerm('');
    setUnassignedUsers([]);
    setAssignedUsers([]);
    setOriginalAssignedUserIds(new Set());
    onClose();
  };

  if (!isOpen || !mcpId) return null;

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="max-w-6xl w-full h-[80vh] flex flex-col">
        <DialogHeader>
          <DialogTitle>{t('Assign Users to MCP Server')}</DialogTitle>
        </DialogHeader>

        <div className="flex-1 flex gap-4 min-h-0">
          {/* 左侧 - 未分配用户搜索 (仅管理员可见) */}
          {isAdmin && (
            <div className="w-1/3 flex flex-col">
              <div className="mb-4">
                <h3 className="text-sm font-medium mb-2">{t('Available Users')}</h3>
                <div className="flex items-center space-x-2">
                  <IconSearch size={16} />
                  <Input
                    placeholder={t('Search users...')}
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    className="flex-1"
                  />
                </div>
              </div>

              <div className="flex-1 overflow-y-auto border rounded-md p-2">
                {searchLoading ? (
                  <div className="text-center py-4 text-muted-foreground">
                    {t('Loading...')}
                  </div>
                ) : unassignedUsers.length === 0 ? (
                  <div className="text-center py-4 text-muted-foreground">
                    {searchTerm ? t('No users found') : t('No available users')}
                  </div>
                ) : (
                  <div className="space-y-2">
                    {unassignedUsers.map((user) => (
                      <div key={user.id} className="flex items-center justify-between p-2 border rounded hover:bg-muted/50">
                        <span className="text-sm">{user.userName}</span>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleAssignUser(user)}
                          title={t('Assign')}
                        >
                          <IconPlus size={14} />
                        </Button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* 右侧 - 已分配用户 */}
          <div className="flex-1 flex flex-col">
            <div className="mb-4">
              <h3 className="text-sm font-medium">{t('Assigned Users')} ({assignedUsers.length})</h3>
            </div>

            <div className="flex-1 overflow-y-auto border rounded-md p-2">
              {assignedUsers.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  {t('No users assigned yet')}
                </div>
              ) : (
                <div className="space-y-2">
                  {assignedUsers.map((user) => (
                    <Card key={user.id} className="p-2">
                      <div className="flex items-start justify-between mb-1">
                        <div className="flex-1">
                          <div className="flex items-center justify-between">
                            <span className="font-medium text-sm">{user.userName}</span>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => handleUnassignUser(user)}
                              title={t('Remove')}
                            >
                              <IconX size={14} />
                            </Button>
                          </div>
                          <div className="mt-1">
                            <Textarea
                              value={user.customHeaders || ''}
                              onChange={(e) => handleCustomHeadersChange(user.id, e.target.value)}
                              placeholder={t('Optional custom headers (JSON format)')}
                              className={`text-xs min-h-[60px] resize-none ${user.customHeaders && !isEmptyOrJsonObject(user.customHeaders) ? 'border-red-500 focus:border-red-500' : ''}`}
                              rows={3}
                            />
                            {user.customHeaders && !isEmptyOrJsonObject(user.customHeaders) && (
                              <p className="text-xs text-red-500 mt-1">{t('Headers must be empty or a valid JSON object')}</p>
                            )}
                          </div>
                        </div>
                      </div>
                    </Card>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose}>
            {t('Cancel')}
          </Button>
          <Button onClick={handleSave} disabled={saving}>
            {saving ? t('Saving...') : t('Save')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default AssignUsersModal;
