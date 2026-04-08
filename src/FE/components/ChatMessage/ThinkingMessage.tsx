import { useCallback, useEffect, useMemo, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { preprocessLaTeX } from '@/utils/chats';
import {
  getCachedStepGenerateInfo,
  requestStepGenerateInfo,
  subscribeStepGenerateInfoCache,
  StepGenerateInfoCacheKey,
} from '@/utils/generateInfoCache';

import { ChatStatus } from '@/types/chat';
import { IStepGenerateInfo, ResponseMessageTempId } from '@/types/chatMessage';

import { loadComponentOnce } from '@/components/common/loadComponentOnce';
import LightMarkdown from '@/components/Markdown/LightMarkdown';
import {
  MarkdownLoadingFallback,
  appendStreamingCursor,
  hasMathMarkdown,
} from '@/components/Markdown/markdownShared';

import { IconChevronRight, IconThink } from '../Icons';

const RichMarkdown = loadComponentOnce<{
  className?: string;
  content: string;
}>({
  cacheKey: 'Markdown/RichMarkdown',
  loader: () => import('@/components/Markdown/RichMarkdown').then((mod) => mod.default),
  renderFallback: () => <MarkdownLoadingFallback />,
});

interface Props {
  readonly?: boolean;
  content: string;
  finished?: boolean; // 是否已结束推理
  messageId: string;
  stepId?: string;
  chatId?: string;
  chatShareId?: string;
  chatStatus: ChatStatus;
}

const ThinkingMessage = (props: Props) => {
  const { content, finished, messageId, stepId, chatId, chatShareId, chatStatus } = props;
  const { t } = useTranslation();

  const [isOpen, setIsOpen] = useState(false);
  const [displayDurationMs, setDisplayDurationMs] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [isManuallyToggled, setIsManuallyToggled] = useState(false);

  const cacheKey = useMemo<StepGenerateInfoCacheKey | null>(
    () => {
      if (!stepId) return null;
      return { stepId, chatId, chatShareId };
    },
    [stepId, chatId, chatShareId],
  );

  const canLoadDuration = useMemo(() => {
    if (chatStatus === ChatStatus.Chatting) {
      return false;
    }
    if (finished === false) {
      return false;
    }
    if (!messageId || messageId.startsWith(ResponseMessageTempId)) {
      return false;
    }
    if (!stepId) {
      return false;
    }
    return true;
  }, [chatStatus, finished, messageId, stepId]);

  // 自动开合逻辑（不覆盖用户手动动作）
  useEffect(() => {
    if (isManuallyToggled) return;
    const isFinished = finished ?? true;
    setIsOpen(!isFinished);
  }, [finished, isManuallyToggled]);

  const resolveDurationFromStep = useCallback((stepInfo?: IStepGenerateInfo | null) => {
    if (!stepInfo) {
      return;
    }
    const duration = stepInfo.reasoningDuration || stepInfo.duration || 0;
    if (duration > 0) {
      setDisplayDurationMs(duration);
    }
  }, []);

  const ensureDurationLoaded = useCallback(async () => {
    if (!canLoadDuration || !cacheKey) {
      return;
    }
    if (loading) return;
    if ((displayDurationMs ?? 0) > 0) return;
    const cached = getCachedStepGenerateInfo(cacheKey);
    if (cached !== undefined) {
      resolveDurationFromStep(cached);
      return;
    }

    setLoading(true);
    try {
      const info = await requestStepGenerateInfo(cacheKey);
      resolveDurationFromStep(info);
    } catch (error) {
      console.error('Failed to load reasoning duration:', error);
    } finally {
      setLoading(false);
    }
  }, [canLoadDuration, cacheKey, displayDurationMs, loading, resolveDurationFromStep]);
  
  useEffect(() => {
    if (!cacheKey) return;
    
    const cached = getCachedStepGenerateInfo(cacheKey);
    if (cached !== undefined) {
      resolveDurationFromStep(cached);
    }

    const unsubscribe = subscribeStepGenerateInfoCache(cacheKey, (data) => {
      resolveDurationFromStep(data);
    });

    return () => {
      unsubscribe();
    };
  }, [cacheKey, resolveDurationFromStep]);

  const toggleOpen = useCallback(() => {
    const nextState = !isOpen;
    setIsOpen(nextState);
    setIsManuallyToggled(true);
    if (!nextState) {
      return;
    }
    if (!canLoadDuration) {
      return;
    }
    void ensureDurationLoaded();
  }, [canLoadDuration, ensureDurationLoaded, isOpen]);

  const headerLabel = useMemo(() => {
    const isFinished = finished ?? true;
    if (!isFinished) {
      return t('Thinking...');
    }
    const durationMs = displayDurationMs ?? 0;
    if (durationMs > 0) {
      if (durationMs < 1000) {
        // 小于1秒，显示毫秒
        return t('Thought for {{time}} ms', { time: Math.round(durationMs) });
      } else if (durationMs < 10000) {
        // 小于10秒，显示秒并保留1位小数
        return t('Thought for {{time}} s', { time: (durationMs / 1000).toFixed(1) });
      } else {
        // 大于等于10秒，显示秒不保留小数
        return t('Thought for {{time}} s', { time: Math.round(durationMs / 1000) });
      }
    }
    return t('Thought');
  }, [finished, displayDurationMs, t]);

  useEffect(() => {
    if (!isOpen || !canLoadDuration) {
      return;
    }
    void ensureDurationLoaded();
  }, [canLoadDuration, ensureDurationLoaded, isOpen]);

  return (
    <div className="my-2">
      <div
        className="inline-flex items-center h-8 px-2.5 py-1 text-xs gap-1 rounded-sm leading-none"
        onClick={toggleOpen}
      >
        {(finished ?? true) ? (
          <div className="flex items-center gap-1 leading-none">
            <IconThink size={18} />
            <span className=''>{headerLabel}</span>
            {isOpen && loading && <span className="text-muted-foreground">{t('Loading...')}</span>}
          </div>
        ) : (
          <span className="leading-none">{headerLabel}</span>
        )}
        <div 
          className="transition-transform duration-300 ease-in-out"
          style={{ transform: isOpen ? 'rotate(90deg)' : 'rotate(0deg)' }}
        >
          <IconChevronRight size={18} stroke="#6b7280" />
        </div>
      </div>
      <div
        className={`grid overflow-hidden transition-all duration-300 ease-in-out ${
          isOpen ? 'grid-rows-[1fr] opacity-100' : 'grid-rows-[0fr] opacity-0'
        }`}
      >
        <div className="overflow-hidden">
          <div className="px-2 text-gray-400 text-sm mt-2">
            {(() => {
              const renderedMarkdown = appendStreamingCursor(
                preprocessLaTeX(content!),
                finished === false,
              );

              return hasMathMarkdown(content ?? '') ? (
                <RichMarkdown content={renderedMarkdown} />
              ) : (
                <LightMarkdown content={renderedMarkdown} />
              );
            })()}
          </div>
        </div>
      </div>
    </div>
  );
};

export default ThinkingMessage;
