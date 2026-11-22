import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { ContainerPortMapping } from '@/types/containers';

import { IconLoader, IconPlus, IconTrash } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';

import {
  addContainerPortMapping,
  listContainerPortMappings,
  removeContainerPortMapping,
} from '@/apis/containersApi';

type Props = {
  encryptedId: string | null;
  containerName?: string;
  onOpenChange: (open: boolean) => void;
  onChanged?: () => void;
};

export default function ContainerPortMappingsDialog({
  encryptedId,
  containerName,
  onOpenChange,
  onChanged,
}: Props) {
  const { t } = useTranslation();
  const [mappings, setMappings] = useState<ContainerPortMapping[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [port, setPort] = useState('');
  const [protocol, setProtocol] = useState<'tcp' | 'udp'>('tcp');

  const load = useCallback(async () => {
    if (!encryptedId) return;
    setLoading(true);
    try {
      setMappings(await listContainerPortMappings(encryptedId));
    } finally {
      setLoading(false);
    }
  }, [encryptedId]);

  useEffect(() => {
    if (encryptedId) {
      setPort('');
      setProtocol('tcp');
      void load().catch(() => undefined);
    } else {
      setMappings([]);
    }
  }, [encryptedId, load]);

  const add = async () => {
    const value = Number(port.trim());
    if (!encryptedId || !Number.isInteger(value) || value < 1 || value > 65535) {
      toast.error(t('Container port must be between 1 and 65535'));
      return;
    }
    setBusy(true);
    try {
      await addContainerPortMapping(encryptedId, {
        containerPort: value,
        protocol,
      });
      setPort('');
      toast.success(t('Port mapping added'));
      onChanged?.();
      await load();
    } catch {
      // errors are shown by the fetch client
    } finally {
      setBusy(false);
    }
  };

  const remove = async (mapping: ContainerPortMapping) => {
    if (!encryptedId) return;
    setBusy(true);
    try {
      await removeContainerPortMapping(encryptedId, mapping.id);
      toast.success(t('Port mapping removed'));
      onChanged?.();
      await load();
    } catch {
      // errors are shown by the fetch client
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={encryptedId != null} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>
            {t('Port mappings')}
            {containerName ? ` - ${containerName}` : ''}
          </DialogTitle>
          <DialogDescription>
            {t(
              'Adding or removing a port mapping recreates the container. Files outside mounted volumes will be lost.',
            )}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div className="flex items-center gap-2">
            <Input
              type="number"
              min={1}
              max={65535}
              placeholder={t('Container port')}
              value={port}
              disabled={busy}
              onChange={(e) => setPort(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') void add();
              }}
            />
            <select
              className="h-10 rounded-md border bg-background px-3 text-sm"
              value={protocol}
              disabled={busy}
              onChange={(e) => setProtocol(e.target.value as 'tcp' | 'udp')}
            >
              <option value="tcp">TCP</option>
              <option value="udp">UDP</option>
            </select>
            <Button onClick={() => void add()} disabled={busy || !port.trim()}>
              {busy ? (
                <IconLoader size={16} className="animate-spin" />
              ) : (
                <IconPlus size={16} />
              )}
              <span className="ml-1">{t('Add')}</span>
            </Button>
          </div>
          {loading ? (
            <div className="py-4 text-center text-sm text-muted-foreground">
              {t('Loading...')}
            </div>
          ) : mappings.length === 0 ? (
            <div className="py-4 text-center text-sm text-muted-foreground">
              {t('No port mappings')}
            </div>
          ) : (
            <div className="divide-y rounded-md border">
              {mappings.map((mapping) => (
                <div
                  key={mapping.id}
                  className="flex items-center justify-between gap-2 px-3 py-2 text-sm"
                >
                  <div className="min-w-0">
                    <div className="font-medium">
                      {mapping.containerPort}/{mapping.protocol} →{' '}
                      {mapping.hostPort}
                    </div>
                    <code className="break-all text-xs text-muted-foreground">
                      {mapping.upstreamAddress}
                    </code>
                  </div>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-8 w-8 shrink-0"
                    disabled={busy}
                    title={t('Delete')}
                    onClick={() => void remove(mapping)}
                  >
                    <IconTrash size={15} />
                  </Button>
                </div>
              ))}
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
