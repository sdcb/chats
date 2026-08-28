import { ReactElement, useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { copyTextToClipboard } from '@/utils/clipboard';
import {
  formatAbsoluteTime,
  formatRelativeWithinHour,
} from '@/utils/relativeTime';

import { ContainerSessionDto } from '@/types/containers';

import {
  IconArchive,
  IconBolt,
  IconCheck,
  IconClipboard,
  IconDocker,
  IconFolder,
  IconIdBadge,
  IconLoader,
  IconNotes,
  IconPin,
  IconPlus,
  IconRefresh,
  IconSettings,
  IconWorld,
} from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';

import { touchContainer } from '@/apis/containersApi';

type Props = {
  chatId: string;
  session: ContainerSessionDto;
  onRefreshTimes: () => Promise<void>;
};

function formatBytes(bytes: number | null): string {
  if (bytes === null) return '-';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024)
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

export default function SessionInfoCard({
  chatId,
  session,
  onRefreshTimes,
}: Props) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const [touching, setTouching] = useState(false);
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = window.setInterval(() => {
      setNow(new Date());
    }, 30 * 1000);

    return () => window.clearInterval(timer);
  }, []);

  const basicItems = useMemo(() => {
    const items: Array<{
      label: string;
      value: string;
      toCopyValue: string;
      title?: string;
      icon: ReactElement;
      group: 'basic' | 'time' | 'resource';
    }> = [
      {
        label: t('Label'),
        value: session.name,
        toCopyValue: session.name,
        icon: <IconNotes size={16} />,
        group: 'basic',
      },
      {
        label: t('Image'),
        value: session.image,
        toCopyValue: session.image,
        icon: <IconDocker size={16} />,
        group: 'basic',
      },
      {
        label: t('Turn Binding'),
        value:
          !session.ownerTurnId !== null
            ? t('Unbound')
            : session.ownerTurnId === null
            ? t('Bound (no position)')
            : t('Bound to position{{spanId}}', {
                spanId: session.ownerTurnId,
              }),
        toCopyValue:
          !session.ownerTurnId !== null
            ? t('Unbound')
            : session.ownerTurnId === null
            ? t('Bound (no position)')
            : String(session.ownerTurnId),
        icon: <IconSettings size={16} />,
        group: 'basic',
      },
      {
        label: t('Created Time'),
        value: formatRelativeWithinHour(session.createdAt, now, t),
        toCopyValue: formatAbsoluteTime(session.createdAt),
        title: formatAbsoluteTime(session.createdAt),
        icon: <IconPlus size={16} />,
        group: 'time',
      },
      {
        label: t('Last Active Time'),
        value: formatRelativeWithinHour(session.lastActiveAt, now, t),
        toCopyValue: formatAbsoluteTime(session.lastActiveAt),
        title: formatAbsoluteTime(session.lastActiveAt),
        icon: <IconRefresh size={16} />,
        group: 'time',
      },
      {
        label: t('Delete Time'),
        value: session.cleanupAt
          ? formatRelativeWithinHour(session.cleanupAt, now, t)
          : t('Permanent'),
        toCopyValue: session.cleanupAt
          ? formatAbsoluteTime(session.cleanupAt)
          : t('Permanent'),
        title: session.cleanupAt
          ? formatAbsoluteTime(session.cleanupAt)
          : t('Permanent'),
        icon: <IconArchive size={16} />,
        group: 'time',
      },
      {
        label: t('CPU'),
        value:
          session.cpuCores !== null
            ? `${session.cpuCores} ${t('cores')}`
            : t('Unlimited'),
        toCopyValue:
          session.cpuCores !== null
            ? `${session.cpuCores} ${t('cores')}`
            : t('Unlimited'),
        icon: <IconBolt size={16} />,
        group: 'resource',
      },
      {
        label: t('Status'),
        value: session.isStopped ? t('Stopped') : t('Running'),
        toCopyValue: session.isStopped ? t('Stopped') : t('Running'),
        icon: <IconSettings size={16} />,
        group: 'basic',
      },
      {
        label: t('Memory'),
        value: formatBytes(session.memoryBytes),
        toCopyValue: formatBytes(session.memoryBytes),
        icon: <IconFolder size={16} />,
        group: 'resource',
      },
      {
        label: t('PID Limit'),
        value:
          session.maxProcesses !== null
            ? String(session.maxProcesses)
            : t('Unlimited'),
        toCopyValue:
          session.maxProcesses !== null
            ? String(session.maxProcesses)
            : t('Unlimited'),
        icon: <IconIdBadge size={16} />,
        group: 'resource',
      },
      {
        label: t('Network'),
        value: session.backendNetworkName ?? 'bridge',
        toCopyValue: session.backendNetworkName ?? 'bridge',
        icon: <IconWorld size={16} />,
        group: 'resource',
      },
    ];

    if ((session.backendNetworkName ?? 'bridge') === 'bridge' && session.ip) {
      items.push({
        label: t('IP Address'),
        value: session.ip,
        toCopyValue: session.ip,
        icon: <IconPin size={16} />,
        group: 'resource',
      });
    }

    return items;
  }, [now, session, t]);

  const copyText = useMemo(() => {
    return basicItems
      .map((item) => `${item.label}: ${item.toCopyValue}`)
      .join('\n');
  }, [basicItems]);

  const groupedItems = useMemo(() => {
    return [
      {
        key: 'basic',
        title: t('Basic Info'),
        items: basicItems.filter((item) => item.group === 'basic'),
      },
      {
        key: 'time',
        title: t('Time'),
        items: basicItems.filter((item) => item.group === 'time'),
      },
      {
        key: 'resource',
        title: t('Resources'),
        items: basicItems.filter((item) => item.group === 'resource'),
      },
    ].filter((section) => section.items.length > 0);
  }, [basicItems, t]);

  const handleCopy = useCallback(async () => {
    if (!(await copyTextToClipboard(copyText))) return;

    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [copyText]);

  const handleTouchSession = useCallback(async () => {
    if (touching) return;
    setTouching(true);
    try {
      await touchContainer(chatId, session.encryptedId);
      await onRefreshTimes();
    } catch (e: any) {
      toast.error(e?.message || t('Failed to refresh session time'));
    } finally {
      setTouching(false);
    }
  }, [chatId, onRefreshTimes, session.encryptedId, t, touching]);

  return (
    <div className="h-full flex flex-col">
      {/* 标题栏 */}
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-medium text-muted-foreground">
          {t('Session Information')}
        </h3>
        <div className="flex items-center gap-1">
          <Tips
            content={t('Refresh session time')}
            trigger={
              <Button
                size="sm"
                variant="default"
                onClick={handleTouchSession}
                disabled={touching}
                className="h-8 w-8 p-0"
              >
                {touching ? (
                  <IconLoader size={14} stroke="currentColor" />
                ) : (
                  <IconRefresh size={14} stroke="currentColor" />
                )}
              </Button>
            }
          />
          <Tips
            content={copied ? t('Copied') : t('Click Copy')}
            trigger={
              <button
                className="flex items-center rounded p-1 text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
                onClick={handleCopy}
              >
                {copied ? (
                  <IconCheck stroke="currentColor" size={18} />
                ) : (
                  <IconClipboard stroke="currentColor" size={18} />
                )}
              </button>
            }
          />
        </div>
      </div>

      <div className="space-y-3 overflow-auto pr-1">
        {groupedItems.map((section) => (
          <section
            key={section.key}
            className="rounded-lg border border-border/70"
          >
            <h4 className="px-3 py-2 text-xs font-medium text-muted-foreground bg-muted/40 border-b border-border/70">
              {section.title}
            </h4>
            <dl className="divide-y divide-border/70">
              {section.items.map((item) => (
                <div
                  key={`item-${item.label}`}
                  className="flex items-center gap-3 px-3 py-2.5"
                >
                  <div className="shrink-0 text-muted-foreground">
                    {item.icon}
                  </div>
                  <dt className="shrink-0 text-xs text-muted-foreground w-24 sm:w-28">
                    {item.label}
                  </dt>
                  <dd
                    className="min-w-0 flex-1 text-sm font-medium truncate"
                    title={item.title ?? item.value}
                  >
                    {item.value}
                  </dd>
                </div>
              ))}
            </dl>
          </section>
        ))}
      </div>
    </div>
  );
}
