import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import useTranslation from '@/hooks/useTranslation';
import { AdminModelDto, UserModelUserDto } from '@/types/adminApis';
import { getUsersByModelId, deleteUserModel } from '@/apis/adminApis';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { IconChevronDown, IconChevronRight, IconTrash, IconPlus } from '@/components/Icons';
import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

interface IProps {
  model: AdminModelDto;
  providerName: string;
  keyName: string;
  isExpanded: boolean;
  onToggle: () => void;
  onUpdate: () => void;
}

export default function ModelUserList({ model, providerName, keyName, isExpanded, onToggle, onUpdate }: IProps) {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [users, setUsers] = useState<UserModelUserDto[]>([]);

  useEffect(() => {
    if (isExpanded) {
      loadUsers();
    }
  }, [isExpanded]);

  const loadUsers = async () => {
    try {
      setLoading(true);
      const data = await getUsersByModelId(model.modelId);
      setUsers(data);
    } catch (error) {
      console.error('Failed to load users:', error);
      toast.error(t('Failed to load users'));
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (userModelId: number) => {
    try {
      await deleteUserModel(userModelId);
      toast.success(t('User removed successfully'));
      await loadUsers();
      onUpdate();
    } catch (error) {
      console.error('Failed to remove user:', error);
      toast.error(t('Failed to remove user'));
    }
  };

  const isOrphan = users.length === 0 && !loading && isExpanded;

  return (
    <div className="border-b last:border-0">
      <div
        className={cn(
          "flex items-center justify-between p-4 hover:bg-muted/50 cursor-pointer",
          isOrphan && "bg-yellow-50 dark:bg-yellow-950/20"
        )}
        onClick={onToggle}
      >
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            {isExpanded ? (
              <IconChevronDown size={16} />
            ) : (
              <IconChevronRight size={16} />
            )}
            <ModelProviderIcon providerId={model.modelProviderId} className="w-5 h-5" />
          </div>
          <div>
            <div className="font-medium">{model.name}</div>
            <div className="text-sm text-muted-foreground">
              {providerName} / {keyName}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {isOrphan && (
            <Badge variant="destructive" className="text-xs">
              ⚠️ {t('Orphan Model')}
            </Badge>
          )}
          {!isExpanded && users.length > 0 && (
            <Badge variant="secondary">
              {users.length} {t('users')}
            </Badge>
          )}
        </div>
      </div>

      {isExpanded && (
        <div className="pl-8 pr-4 pb-4">
          {loading ? (
            <div className="space-y-2">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : users.length === 0 ? (
            <div className="border rounded-lg p-6 text-center bg-yellow-50 dark:bg-yellow-950/20">
              <div className="text-lg mb-2">⚠️</div>
              <div className="text-sm font-medium mb-1">{t('No users assigned to this model')}</div>
              <div className="text-xs text-muted-foreground mb-3">
                {t('This model is not available to any users')}
              </div>
              <Button size="sm" variant="default">
                <IconPlus size={14} className="mr-1" />
                {t('Assign to Users')}
              </Button>
            </div>
          ) : (
            <div className="space-y-1">
              {users.map(user => (
                <div
                  key={user.id}
                  className="flex items-center justify-between p-3 rounded-lg bg-muted/30 hover:bg-muted/50"
                >
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center">
                      <span className="text-sm font-medium">{user.username[0].toUpperCase()}</span>
                    </div>
                    <div>
                      <div className="text-sm font-medium">{user.displayName}</div>
                      <div className="text-xs text-muted-foreground">@{user.username}</div>
                    </div>
                  </div>
                  <div className="flex items-center gap-4">
                    <div className="text-xs text-muted-foreground">
                      <span>
                        {user.counts === -1 ? '∞' : user.counts} {t('counts')},
                        {user.tokens === -1 ? ' ∞' : ` ${user.tokens}`} {t('tokens')}
                      </span>
                    </div>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => handleDelete(user.id)}
                    >
                      <IconTrash size={14} />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
