import { useCallback, useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { ContainerResource } from '@/types/containers';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

import {
  createVolume,
  deleteVolume,
  listContainers,
  listVolumes,
  mountVolume,
  unmountVolume,
} from '@/apis/containersApi';

type Volume = {
  id: number;
  name: string;
  declaredBytes: number | null;
  containerVolumeMounts: Array<{
    id: number;
    containerResourceId: number;
    containerPath: string;
    isReadOnly: boolean;
  }>;
};

export default function VolumesPage() {
  const { t } = useTranslation();
  const [volumes, setVolumes] = useState<Volume[]>([]);
  const [containers, setContainers] = useState<ContainerResource[]>([]);
  const [name, setName] = useState('');
  const [declaredBytes, setDeclaredBytes] = useState('');
  const refresh = useCallback(async () => {
    const [v, c] = await Promise.all([listVolumes(), listContainers()]);
    setVolumes(v);
    setContainers(c);
  }, []);
  useEffect(() => {
    refresh().catch(() => null);
  }, [refresh]);
  const create = async () => {
    if (!name.trim()) return;
    await createVolume({
      runtimeNodeId: 0,
      name: name.trim(),
      declaredBytes: declaredBytes ? Number(declaredBytes) : null,
    });
    setName('');
    setDeclaredBytes('');
    await refresh();
  };
  return (
    <main className="mx-auto max-w-5xl p-6 space-y-6">
      <h1 className="text-2xl font-semibold">{t('Volumes')}</h1>
      <section className="rounded-lg border p-4 space-y-3">
        <h2 className="font-medium">{t('Create standalone volume')}</h2>
        <div className="grid gap-2 sm:grid-cols-3">
          <Label>
            {t('Name')}
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </Label>
          <Label>
            {t('Declared bytes')}
            <Input
              type="number"
              value={declaredBytes}
              onChange={(e) => setDeclaredBytes(e.target.value)}
            />
          </Label>
          <Button
            className="self-end"
            onClick={() => create().catch(() => null)}
          >
            {t('Create')}
          </Button>
        </div>
      </section>
      <div className="space-y-3">
        {volumes.map((volume) => (
          <section key={volume.id} className="rounded-lg border p-4">
            <div className="flex justify-between">
              <div>
                <h2 className="font-medium">{volume.name}</h2>
                <p className="text-sm text-muted-foreground">
                  {volume.declaredBytes ?? t('Unlimited')} ·{' '}
                  {volume.containerVolumeMounts.length} {t('mounts')}
                </p>
              </div>
              <Button
                variant="destructive"
                size="sm"
                onClick={() => deleteVolume(volume.id).then(refresh)}
              >
                {t('Delete')}
              </Button>
            </div>
            <div className="mt-3 space-y-2">
              {volume.containerVolumeMounts.map((mount) => (
                <div
                  key={mount.id}
                  className="flex justify-between text-sm border-t pt-2"
                >
                  <span>
                    {mount.containerPath} ·{' '}
                    {mount.isReadOnly ? t('Read only') : t('Read/write')}
                  </span>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() =>
                      unmountVolume(volume.id, mount.id).then(refresh)
                    }
                  >
                    {t('Unmount')}
                  </Button>
                </div>
              ))}
            </div>
            <div className="mt-3 flex gap-2">
              <select
                className="h-9 rounded border px-2 text-sm"
                id={`container-${volume.id}`}
              >
                {containers
                  .filter((container) => !container.isDeleted)
                  .map((container) => (
                    <option
                      key={container.encryptedId}
                      value={container.encryptedId}
                    >
                      {container.name}
                    </option>
                  ))}
              </select>
              <Input
                id={`path-${volume.id}`}
                placeholder="/data"
                className="h-9"
              />
              <Button
                size="sm"
                onClick={() => {
                  const c = document.getElementById(
                    `container-${volume.id}`,
                  ) as HTMLSelectElement;
                  const p = document.getElementById(
                    `path-${volume.id}`,
                  ) as HTMLInputElement;
                  return mountVolume(volume.id, {
                    encryptedContainerResourceId: c.value,
                    containerPath: p.value || '/data',
                    isReadOnly: false,
                  }).then(refresh);
                }}
              >
                {t('Mount')}
              </Button>
            </div>
          </section>
        ))}
      </div>
    </main>
  );
}
