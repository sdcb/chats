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
import { cn } from '@/lib/utils';
import SessionEnvVarEditor from './SessionEnvVarEditor';

type Props = {
  chatId: string;
  session: DockerSessionDto;
};

function formatBytes(bytes: number | null): string {
  if (bytes === null) return '-';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

// 卡片颜色配置
const cardColors = [
  'bg-blue-500/10 border-blue-500/30 text-blue-600 dark:text-blue-400',
  'bg-green-500/10 border-green-500/30 text-green-600 dark:text-green-400',
  'bg-purple-500/10 border-purple-500/30 text-purple-600 dark:text-purple-400',
  'bg-orange-500/10 border-orange-500/30 text-orange-600 dark:text-orange-400',
  'bg-cyan-500/10 border-cyan-500/30 text-cyan-600 dark:text-cyan-400',
  'bg-pink-500/10 border-pink-500/30 text-pink-600 dark:text-pink-400',
];

export default function SessionInfoCard({ chatId, session }: Props) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  const infoItems = useMemo(() => {
    const items = [
      {
        label: t('Image'),
        value: session.image,
        icon: '🐳',
      },
      {
        label: t('CPU'),
        value: session.cpuCores !== null ? `${session.cpuCores} ${t('cores')}` : t('Unlimited'),
        icon: '⚡',
      },
      {
        label: t('Memory'),
        value: formatBytes(session.memoryBytes),
        icon: '💾',
      },
      {
        label: t('PID Limit'),
        value: session.maxProcesses !== null ? String(session.maxProcesses) : t('Unlimited'),
        icon: '🔢',
      },
      {
        label: t('Network'),
        value: session.networkMode,
        icon: '🌐',
      },
    ];

    // 仅当网络模式为 bridge 时显示 IP 地址
    if (session.networkMode === 'bridge' && session.ipAddress) {
      items.push({
        label: t('IP Address'),
        value: session.ipAddress,
        icon: '📍',
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
    <div className="h-full flex flex-col">
      {/* 标题栏 */}
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-medium text-muted-foreground">
          {t('Session Information')}
        </h3>
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                className="flex items-center rounded p-1.5 text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
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

      {/* 卡片网格 */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {infoItems.map((item, index) => (
          <div
            key={index}
            className={cn(
              'rounded-lg border p-3 transition-all hover:shadow-md',
              cardColors[index % cardColors.length],
            )}
          >
            <div className="flex items-center gap-2 mb-1">
              <span className="text-base">{item.icon}</span>
              <span className="text-xs font-medium opacity-80">{item.label}</span>
            </div>
            <div className="text-sm font-semibold truncate" title={item.value}>
              {item.value}
            </div>
          </div>
        ))}
      </div>

      {/* 环境变量管理 */}
      <div className="mt-6">
        <SessionEnvVarEditor
          chatId={chatId}
          encryptedSessionId={session.encryptedSessionId}
        />
      </div>
    </div>
  );
}
