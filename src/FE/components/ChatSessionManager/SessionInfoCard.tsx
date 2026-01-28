import { useCallback, useMemo, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';
import { DockerSessionDto } from '@/types/dockerSessions';
import { IconCheck, IconClipboard } from '@/components/Icons';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

type Props = {
  session: DockerSessionDto;
};

function formatBytes(bytes: number | null): string {
  if (bytes === null) return '-';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

export default function SessionInfoCard({ session }: Props) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  const infoItems = useMemo(() => {
    const items = [
      {
        label: t('Image'),
        value: session.image,
      },
      {
        label: t('CPU'),
        value: session.cpuCores !== null ? `${session.cpuCores} ${t('cores')}` : t('Unlimited'),
      },
      {
        label: t('Memory'),
        value: formatBytes(session.memoryBytes),
      },
      {
        label: t('PID Limit'),
        value: session.maxProcesses !== null ? String(session.maxProcesses) : t('Unlimited'),
      },
      {
        label: t('Network'),
        value: session.networkMode,
      },
    ];

    // 仅当网络模式为 bridge 时显示 IP 地址
    if (session.networkMode === 'bridge' && session.ipAddress) {
      items.push({
        label: t('IP Address'),
        value: session.ipAddress,
      });
    }

    return items;
  }, [session, t]);

  const copyText = useMemo(() => {
    return infoItems.map((item) => `${item.label}: ${item.value}`).join('\n');
  }, [infoItems]);

  const handleCopy = useCallback(() => {
    navigator.clipboard.writeText(copyText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [copyText]);

  return (
    <div className="relative rounded-md border bg-muted/30 px-3 py-2 group">
      <div className="flex flex-wrap gap-x-5 gap-y-1.5 text-xs pr-8">
        {infoItems.map((item, index) => (
          <div key={index} className="flex items-center gap-1 text-muted-foreground">
            <span>{item.label}:</span>
            <span className="text-foreground font-medium">{item.value}</span>
          </div>
        ))}
      </div>
      <div className="absolute top-1/2 -translate-y-1/2 right-2 opacity-0 group-hover:opacity-100 transition-opacity">
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                className="flex items-center rounded p-1 text-muted-foreground hover:text-foreground hover:bg-muted"
                onClick={handleCopy}
              >
                {copied ? (
                  <IconCheck stroke="currentColor" size={16} />
                ) : (
                  <IconClipboard stroke="currentColor" size={16} />
                )}
              </button>
            </TooltipTrigger>
            <TooltipContent>
              {copied ? t('Copied') : t('Click Copy')}
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      </div>
    </div>
  );
}
