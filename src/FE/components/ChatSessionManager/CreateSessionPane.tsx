import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { IconLoader } from '@/components/Icons';

import {
  CreateDockerSessionRequest,
  MemoryLimitResponse,
  NetworkModesResponse,
  ResourceLimitResponse,
} from '@/types/dockerSessions';
import ImageComboBox from './ImageComboBox';
import { getApiErrorMessage } from '@/utils/apiError';
import useTranslation from '@/hooks/useTranslation';

type Props = {
  defaultImage: string;
  images: string[];
  cpuLimits: ResourceLimitResponse | null;
  memoryLimits: MemoryLimitResponse | null;
  networkModes: NetworkModesResponse | null;
  onCancel: () => void;
  onCreate: (req: CreateDockerSessionRequest) => Promise<void>;
};

export default function CreateSessionPane({
  defaultImage,
  images,
  cpuLimits,
  memoryLimits,
  networkModes,
  onCancel,
  onCreate,
}: Props) {
  const { t } = useTranslation();
  const [label, setLabel] = useState('');
  const [image, setImage] = useState('');
  const [cpuCores, setCpuCores] = useState<string>('');
  const [memoryBytes, setMemoryBytes] = useState<string>('');
  const [networkMode, setNetworkMode] = useState<string>('');
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    setImage(defaultImage ?? '');
  }, [defaultImage]);

  useEffect(() => {
    setNetworkMode(networkModes?.defaultNetworkMode ?? '');
  }, [networkModes?.defaultNetworkMode]);

  const allowedNetworkModes = useMemo(
    () => networkModes?.allowedNetworkModes ?? [],
    [networkModes?.allowedNetworkModes],
  );

  const hintCpu = cpuLimits
    ? t('Default {{default}}; max {{max}}', {
        default: cpuLimits.defaultValue,
        max: cpuLimits.maxValue || 'unlimited',
      })
    : '';
  const hintMem = memoryLimits
    ? t('Default {{default}} bytes; max {{max}}', {
        default: memoryLimits.defaultBytes,
        max: memoryLimits.maxBytes || 'unlimited',
      })
    : '';

  return (
    <div className="border rounded-md p-4">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label>{t('Session label (optional)')}</Label>
          <Input
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            placeholder={t('Leave empty to use container id prefix (12 chars)')}
          />
        </div>

        <div className="space-y-2">
          <Label>{t('Image name')}</Label>
          <ImageComboBox
            value={image}
            onChange={setImage}
            placeholder={defaultImage}
            options={images}
          />
        </div>

        <div className="space-y-2">
          <Label>{t('CPU limit (cores)')}</Label>
          <Input
            value={cpuCores}
            onChange={(e) => setCpuCores(e.target.value)}
            placeholder={cpuLimits ? String(cpuLimits.defaultValue) : ''}
            inputMode="decimal"
          />
          {hintCpu && <div className="text-xs text-muted-foreground">{hintCpu}</div>}
        </div>

        <div className="space-y-2">
          <Label>{t('Memory (bytes)')}</Label>
          <Input
            value={memoryBytes}
            onChange={(e) => setMemoryBytes(e.target.value)}
            placeholder={memoryLimits ? String(memoryLimits.defaultBytes) : ''}
            inputMode="numeric"
          />
          {hintMem && <div className="text-xs text-muted-foreground">{hintMem}</div>}
        </div>

        <div className="space-y-2">
          <Label>{t('Network mode')}</Label>
          <Select value={networkMode} onValueChange={setNetworkMode}>
            <SelectTrigger>
              <SelectValue placeholder={t('Select network mode')} />
            </SelectTrigger>
            <SelectContent>
              {allowedNetworkModes.map((m) => (
                <SelectItem key={m} value={m}>
                  {m}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {networkModes && (
            <div className="text-xs text-muted-foreground">
              {t('MaxAllowedNetworkMode')}: {networkModes.maxAllowedNetworkMode}
            </div>
          )}
        </div>
      </div>

      <div className="flex justify-end gap-2 pt-4">
        <Button variant="secondary" onClick={onCancel} disabled={creating}>
          {t('Cancel')}
        </Button>
        <Button
          onClick={async () => {
            setCreating(true);
            try {
              const req: CreateDockerSessionRequest = {
                label: label.trim() || null,
                image: image.trim() || null,
                cpuCores: cpuCores.trim() ? Number(cpuCores) : null,
                memoryBytes: memoryBytes.trim() ? Number(memoryBytes) : null,
                networkMode: networkMode || null,
              };
              await onCreate(req);
            } catch (e: any) {
              toast.error(getApiErrorMessage(e, t('Create failed')));
            } finally {
              setCreating(false);
            }
          }}
          disabled={creating}
        >
          {creating ? (
            <IconLoader className="animate-spin" size={16} />
          ) : (
            t('Create')
          )}
        </Button>
      </div>
    </div>
  );
}
