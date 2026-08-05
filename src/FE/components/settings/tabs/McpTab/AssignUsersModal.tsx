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
import { LabelSwitch } from '@/components/ui/label-switch';
import {
  Dialog,
  DialogContent,
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
  /** Assigner's current ShowShortcut; new users default to this value. */
  defaultShowShortcut?: boolean;
}

interface AssignedUser extends AssignedUserDetailsDto {
  originalCustomHeaders?: string;
  originalShowShortcut?: boolean;
  isNew?: boolean;
}

const AssignUsersModal = ({
  isOpen,
  onClose,
  mcpId,
  onSuccess,
  isAdmin,
  defaultShowShortcut = false,
}: AssignUsersModalProps) => {
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
      searchUsers('');
    }
  }, [isOpen, mcpId]);

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
      const assigned = await getAssignedUserDetails(mcpId);

      const assignedWithOriginal = assigned.map(user => ({
        ...user,
        showShortcut: user.showShortcut ?? false,
        originalCustomHeaders: user.customHeaders,
        originalShowShortcut: user.showShortcut ?? false,
        isNew: false,
      }));
      setAssignedUsers(assignedWithOriginal);

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
    setUnassignedUsers(prev => prev.filter(u => u.id !== user.id));

    // New users default to the assigner's current ShowShortcut.
    const newAssignedUser: AssignedUser = {
      id: user.id,
      userName: user.userName,
      customHeaders: '',
      showShortcut: defaultShowShortcut,
      originalCustomHeaders: undefined,
      originalShowShortcut: undefined,
      isNew: true,
    };
    setAssignedUsers(prev => [newAssignedUser, ...prev]);
  };

  const handleUnassignUser = (user: AssignedUser) => {
    setAssignedUsers(prev => prev.filter(u => u.id !== user.id));

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

  const handleShowShortcutChange = (userId: number, showShortcut: boolean) => {
    setAssignedUsers(prev =>
      prev.map(user =>
        user.id === userId
          ? { ...user, showShortcut }
          : user
      )
    );
  };

  const getChanges = (): AssignUsersToMcpRequest => {
    const toAssignedUsers: AssignedUserInfo[] = [];
    const toUpdateUsers: AssignedUserInfo[] = [];
    const toDeleteUserIds: number[] = [];

    const currentAssignedIds = new Set(assignedUsers.map(user => user.id));

    assignedUsers.forEach(user => {
      if (user.isNew) {
        toAssignedUsers.push({
          id: user.id,
          customHeaders: user.customHeaders || undefined,
          showShortcut: user.showShortcut,
        });
      } else if (
        user.customHeaders !== user.originalCustomHeaders ||
        user.showShortcut !== user.originalShowShortcut
      ) {
        toUpdateUsers.push({
          id: user.id,
          customHeaders: user.customHeaders || undefined,
          showShortcut: user.showShortcut,
        });
      }
    });

    originalAssignedUserIds.forEach(originalId => {
      if (!currentAssignedIds.has(originalId)) {
        toDeleteUserIds.push(originalId);
      }
    });

    return {
      toAssignedUsers,
      toUpdateUsers,
      toDeleteUserIds,
    };
  };

  const handleSave = async () => {
    if (!mcpId) return;

    const changes = getChanges();

    for (const u of assignedUsers) {
      if (u.customHeaders && !isEmptyOrJsonObject(u.customHeaders)) {
        toast.error(t('Headers must be empty or a valid JSON object'));
        return;
      }
    }

    if (
      changes.toAssignedUsers.length === 0 &&
      changes.toUpdateUsers.length === 0 &&
      changes.toDeleteUserIds.length === 0
    ) {
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

          <div className="flex-1 flex flex-col">
            <div className="mb-4">
              <h3 className="text-sm font-medium">{t('Assigned Users')} ({assignedUsers.length})</h3>
            </div>

            <div className="flex-1 overflow-y-auto border rounded-md p-2">
              {loading ? (
                <div className="text-center py-8 text-muted-foreground">
                  {t('Loading...')}
                </div>
              ) : assignedUsers.length === 0 ? (
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
                          <div className="mt-1 space-y-2">
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
                            <LabelSwitch
                              checked={!!user.showShortcut}
                              onCheckedChange={(checked) => handleShowShortcutChange(user.id, checked)}
                              label={t('Show Shortcut')}
                              tooltip={t('Show this MCP as a shortcut button in chat input')}
                            />
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