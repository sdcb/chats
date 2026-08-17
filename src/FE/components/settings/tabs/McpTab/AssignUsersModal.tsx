import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { isEmptyOrJsonObject } from '@/utils/json';

import {
  AssignUsersToMcpRequest,
  AssignedUserDetailsDto,
  AssignedUserInfo,
  UnassignedUserDto,
} from '@/types/clientApis';

import { IconPlus, IconSearch, IconX } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';

import {
  assignUsersToMcp,
  getAssignedUserDetails,
  getUnassignedUsers,
} from '@/apis/clientApis';

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
  const [unassignedUsers, setUnassignedUsers] = useState<UnassignedUserDto[]>(
    [],
  );
  const [assignedUsers, setAssignedUsers] = useState<AssignedUser[]>([]);
  const [locallyUnassignedUsers, setLocallyUnassignedUsers] = useState<
    AssignedUser[]
  >([]);
  const [originalAssignedUserIds, setOriginalAssignedUserIds] = useState<
    Set<number>
  >(new Set());
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

      const assignedWithOriginal = assigned.map((user) => ({
        ...user,
        showShortcut: user.showShortcut ?? false,
        originalCustomHeaders: user.customHeaders,
        originalShowShortcut: user.showShortcut ?? false,
        isNew: false,
      }));
      setAssignedUsers(assignedWithOriginal);
      setLocallyUnassignedUsers([]);

      const originalIds = new Set(assigned.map((user) => user.id));
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
    setUnassignedUsers((prev) => prev.filter((u) => u.id !== user.id));
    const locallyUnassignedUser = locallyUnassignedUsers.find(
      (u) => u.id === user.id,
    );
    setLocallyUnassignedUsers((prev) => prev.filter((u) => u.id !== user.id));

    // Restore a locally removed user with their unsaved settings. Only users
    // returned by the API are initialized as a new assignment.
    const assignedUser: AssignedUser = locallyUnassignedUser ?? {
      id: user.id,
      userName: user.userName,
      customHeaders: '',
      showShortcut: defaultShowShortcut,
      originalCustomHeaders: undefined,
      originalShowShortcut: undefined,
      isNew: true,
    };
    setAssignedUsers((prev) => [assignedUser, ...prev]);
  };

  const handleUnassignUser = (user: AssignedUser) => {
    setAssignedUsers((prev) => prev.filter((u) => u.id !== user.id));
    setLocallyUnassignedUsers((prev) => [
      user,
      ...prev.filter((u) => u.id !== user.id),
    ]);
  };

  const handleCustomHeadersChange = (userId: number, customHeaders: string) => {
    setAssignedUsers((prev) =>
      prev.map((user) =>
        user.id === userId ? { ...user, customHeaders } : user,
      ),
    );
  };

  const handleShowShortcutChange = (userId: number, showShortcut: boolean) => {
    setAssignedUsers((prev) =>
      prev.map((user) =>
        user.id === userId ? { ...user, showShortcut } : user,
      ),
    );
  };

  const getChanges = (): AssignUsersToMcpRequest => {
    const toAssignedUsers: AssignedUserInfo[] = [];
    const toUpdateUsers: AssignedUserInfo[] = [];
    const toDeleteUserIds: number[] = [];

    const currentAssignedIds = new Set(assignedUsers.map((user) => user.id));

    assignedUsers.forEach((user) => {
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

    originalAssignedUserIds.forEach((originalId) => {
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
    setLocallyUnassignedUsers([]);
    setOriginalAssignedUserIds(new Set());
    onClose();
  };

  if (!isOpen || !mcpId) return null;

  const normalizedSearchTerm = searchTerm.trim().toLowerCase();
  const matchingLocallyUnassignedUsers = locallyUnassignedUsers.filter((user) =>
    user.userName.toLowerCase().includes(normalizedSearchTerm),
  );
  const locallyUnassignedUserIds = new Set(
    matchingLocallyUnassignedUsers.map((user) => user.id),
  );
  const availableUsers: UnassignedUserDto[] = [
    ...matchingLocallyUnassignedUsers.map((user) => ({
      id: user.id,
      userName: user.userName,
    })),
    ...unassignedUsers.filter((user) => !locallyUnassignedUserIds.has(user.id)),
  ];

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
                <h3 className="text-sm font-medium mb-2">
                  {t('Available Users')}
                </h3>
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
                {searchLoading && availableUsers.length === 0 ? (
                  <div className="text-center py-4 text-muted-foreground">
                    {t('Loading...')}
                  </div>
                ) : availableUsers.length === 0 ? (
                  <div className="text-center py-4 text-muted-foreground">
                    {searchTerm ? t('No users found') : t('No available users')}
                  </div>
                ) : (
                  <div className="space-y-2">
                    {availableUsers.map((user) => (
                      <div
                        key={user.id}
                        className="flex items-center justify-between p-2 border rounded hover:bg-muted/50"
                      >
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
              <h3 className="text-sm font-medium">
                {t('Assigned Users')} ({assignedUsers.length})
              </h3>
            </div>

            <div className="flex-1 min-h-0 overflow-y-auto border rounded-md">
              {loading ? (
                <div className="text-center py-8 text-muted-foreground">
                  {t('Loading...')}
                </div>
              ) : assignedUsers.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  {t('No users assigned yet')}
                </div>
              ) : (
                <Table
                  className="table-fixed"
                  containerClassName="overflow-visible"
                >
                  <TableHeader className="sticky top-0 z-10 bg-background">
                    <TableRow>
                      <TableHead className="w-[28%]">{t('User')}</TableHead>
                      <TableHead>{t('Request Headers')}</TableHead>
                      <TableHead className="w-24 px-2">
                        {t('Shortcut')}
                      </TableHead>
                      <TableHead className="w-16 text-center">
                        {t('Actions')}
                      </TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {assignedUsers.map((user) => {
                      const hasInvalidHeaders =
                        !!user.customHeaders &&
                        !isEmptyOrJsonObject(user.customHeaders);

                      return (
                        <TableRow key={user.id}>
                          <TableCell
                            className="truncate whitespace-nowrap py-2 font-medium"
                            title={user.userName}
                          >
                            {user.userName}
                          </TableCell>
                          <TableCell className="px-2 py-2">
                            <Textarea
                              value={user.customHeaders || ''}
                              onChange={(e) =>
                                handleCustomHeadersChange(
                                  user.id,
                                  e.target.value,
                                )
                              }
                              placeholder={t(
                                'Optional custom headers (JSON format)',
                              )}
                              title={
                                hasInvalidHeaders
                                  ? t(
                                      'Headers must be empty or a valid JSON object',
                                    )
                                  : undefined
                              }
                              aria-invalid={hasInvalidHeaders}
                              className={`h-9 min-h-9 resize-none overflow-y-auto py-2 text-xs leading-5 ${
                                hasInvalidHeaders
                                  ? 'border-red-500 focus:border-red-500'
                                  : ''
                              }`}
                              rows={1}
                            />
                          </TableCell>
                          <TableCell className="px-2 py-2">
                            <Switch
                              checked={!!user.showShortcut}
                              onCheckedChange={(checked) =>
                                handleShowShortcutChange(user.id, checked)
                              }
                              aria-label={t('Show Shortcut')}
                              title={t(
                                'Show this MCP as a shortcut button in chat input',
                              )}
                            />
                          </TableCell>
                          <TableCell className="py-2 text-center">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => handleUnassignUser(user)}
                              title={t('Remove')}
                              aria-label={t('Remove')}
                            >
                              <IconX size={14} />
                            </Button>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
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
