import { useCallback, useEffect, useMemo, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { ContainerResource } from '@/types/containers';

import { IconDocker, IconLoader } from '@/components/Icons';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { Switch } from '@/components/ui/switch';

import {
  grantContainerToChat,
  listContainers,
  revokeContainerFromChat,
} from '@/apis/containersApi';
import { cn } from '@/lib/utils';

interface ChatContainerAccessSelectorProps {
  chatId: string;
  disabled?: boolean;
}

const ChatContainerAccessSelector: React.FC<
  ChatContainerAccessSelectorProps
> = ({ chatId, disabled = false }) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [containers, setContainers] = useState<ContainerResource[]>([]);
  const [loading, setLoading] = useState(false);
  const [updatingId, setUpdatingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const all = await listContainers();
      setContainers(all.filter((c) => c.isPermanent && !c.isDeleted));
    } catch (error) {
      console.error('Failed to load containers:', error);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, chatId]);

  useEffect(() => {
    if (open) void load();
  }, [open, load]);

  const grantedCount = useMemo(
    () => containers.filter((c) => c.grantedChatIds.includes(chatId)).length,
    [containers, chatId],
  );

  const toggle = async (container: ContainerResource) => {
    if (disabled || updatingId) return;
    const granted = container.grantedChatIds.includes(chatId);
    setUpdatingId(container.encryptedId);
    try {
      if (granted)
        await revokeContainerFromChat(container.encryptedId, chatId);
      else await grantContainerToChat(container.encryptedId, chatId);
      setContainers((prev) =>
        prev.map((c) =>
          c.encryptedId === container.encryptedId
            ? {
                ...c,
                grantedChatIds: granted
                  ? c.grantedChatIds.filter((id) => id !== chatId)
                  : [...c.grantedChatIds, chatId],
              }
            : c,
        ),
      );
    } catch (error) {
      console.error('Failed to toggle container access:', error);
    } finally {
      setUpdatingId(null);
    }
  };

  if (!loading && containers.length === 0) return null;

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          disabled={disabled}
          title={t('Permanent containers available to this chat')}
          className={cn(
            'h-9 px-2 sm:px-3 rounded-md flex shrink-0 items-center justify-center gap-1 transition-colors whitespace-nowrap',
            'text-xs sm:text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed',
            grantedCount > 0
              ? 'bg-primary text-primary-foreground hover:bg-primary/90'
              : 'bg-transparent border border-input hover:bg-accent hover:text-accent-foreground',
          )}
        >
          <IconDocker size={16} />
          {grantedCount > 0 && <span>{grantedCount}</span>}
        </button>
      </PopoverTrigger>
      <PopoverContent side="top" align="start" className="w-72 p-2">
        <div className="px-1 pb-2 text-sm font-medium">
          {t('Permanent containers')}
        </div>
        {loading && containers.length === 0 ? (
          <div className="flex justify-center py-3">
            <IconLoader size={16} className="animate-spin" />
          </div>
        ) : (
          <div className="max-h-64 space-y-1 overflow-y-auto">
            {containers.map((container) => {
              const granted = container.grantedChatIds.includes(chatId);
              return (
                <div
                  key={container.encryptedId}
                  className="flex items-center justify-between gap-2 rounded-md px-1 py-1.5 hover:bg-accent"
                >
                  <div className="min-w-0">
                    <div className="truncate text-sm">{container.name}</div>
                    <div className="truncate text-xs text-muted-foreground">
                      {container.image}
                      {container.isStopped ? ` · ${t('Stopped')}` : ''}
                    </div>
                  </div>
                  <Switch
                    checked={granted}
                    disabled={disabled || updatingId !== null}
                    onCheckedChange={() => void toggle(container)}
                  />
                </div>
              );
            })}
          </div>
        )}
      </PopoverContent>
    </Popover>
  );
};

export default ChatContainerAccessSelector;
