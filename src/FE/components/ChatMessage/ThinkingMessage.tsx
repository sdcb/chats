import { useCallback, useEffect, useMemo, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { preprocessLaTeX } from '@/utils/chats';
import {
  aggregateStepGenerateInfo,
  fetchGenerateInfoCached,
  GenerateInfoCacheKey,
  getCachedGenerateInfo,
  subscribeGenerateInfoCache,
  requestGenerateInfo,
} from '@/utils/generateInfoCache';

import { ChatStatus } from '@/types/chat';
import { IStepGenerateInfo, ResponseMessageTempId } from '@/types/chatMessage';

import { CodeBlock } from '@/components/Markdown/CodeBlock';
import { MemoizedReactMarkdown } from '@/components/Markdown/MemoizedReactMarkdown';

import { IconChevronDown, IconChevronRight, IconThink } from '../Icons';

import rehypeKatex from 'rehype-katex';
import remarkGfm from 'remark-gfm';
import remarkMath from 'remark-math';

interface Props {
  readonly?: boolean;
  content: string;
  finished?: boolean; // 是否已结束推理
  reasoningDuration?: number;
  messageId: string;
  chatId?: string;
  chatShareId?: string;
  chatStatus: ChatStatus;
}

const ThinkingMessage = (props: Props) => {
  const { content, finished, reasoningDuration, messageId, chatId, chatShareId, chatStatus } = props;
  const { t } = useTranslation();

  const [isOpen, setIsOpen] = useState(false);
  const [displayDurationMs, setDisplayDurationMs] = useState<number | null>(
    typeof reasoningDuration === 'number' && reasoningDuration > 0
      ? reasoningDuration
      : null,
  );
  const [loading, setLoading] = useState(false);
  const [isManuallyToggled, setIsManuallyToggled] = useState(false);

  const cacheKey = useMemo<GenerateInfoCacheKey>(
    () => ({ turnId: messageId, chatId, chatShareId }),
    [messageId, chatId, chatShareId],
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
    return true;
  }, [chatStatus, finished, messageId]);

  // 自动开合逻辑（不覆盖用户手动动作）
  useEffect(() => {
    if (isManuallyToggled) return;
    const isFinished = finished ?? true;
    setIsOpen(!isFinished);
  }, [finished, isManuallyToggled]);

  useEffect(() => {
    if (typeof reasoningDuration === 'number' && reasoningDuration > 0) {
      setDisplayDurationMs(reasoningDuration);
    }
  }, [reasoningDuration]);

  const resolveDurationFromSteps = useCallback((stepInfos?: IStepGenerateInfo[]) => {
    if (!stepInfos || stepInfos.length === 0) {
      return;
    }
    const aggregated = aggregateStepGenerateInfo(stepInfos);
    if (!aggregated) {
      return;
    }
    const duration = aggregated.reasoningDuration || aggregated.duration || 0;
    if (duration > 0) {
      setDisplayDurationMs(duration);
    }
  }, []);

  const ensureDurationLoaded = useCallback(async () => {
    if (!canLoadDuration) {
      return;
    }
    if (loading) return;
    if ((displayDurationMs ?? 0) > 0) return;
    const cached = getCachedGenerateInfo(cacheKey);
    if (cached && cached.length > 0) {
      resolveDurationFromSteps(cached);
      return;
    }

    setLoading(true);
    try {
      const infos = await fetchGenerateInfoCached(cacheKey, () => requestGenerateInfo(cacheKey));
      resolveDurationFromSteps(infos);
    } catch (error) {
      console.error('Failed to load reasoning duration:', error);
    } finally {
      setLoading(false);
    }
  }, [
    canLoadDuration,
    cacheKey,
    chatId,
    chatShareId,
    displayDurationMs,
    loading,
    messageId,
    requestGenerateInfo,
    resolveDurationFromSteps,
  ]);
  
  useEffect(() => {
    const cached = getCachedGenerateInfo(cacheKey);
    if (cached && cached.length > 0) {
      resolveDurationFromSteps(cached);
    }

    const unsubscribe = subscribeGenerateInfoCache(cacheKey, (data) => {
      resolveDurationFromSteps(data);
    });

    return () => {
      unsubscribe();
    };
  }, [cacheKey, resolveDurationFromSteps]);

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
    if ((displayDurationMs ?? 0) > 0) {
      const seconds = Math.floor((displayDurationMs ?? 0) / 1000);
      return t('Deeply thought (took {{time}} seconds)', { time: seconds });
    }
    return t('Deeply thought');
  }, [finished, displayDurationMs, t]);

  useEffect(() => {
    if (!isOpen || !canLoadDuration) {
      return;
    }
    void ensureDurationLoaded();
  }, [canLoadDuration, ensureDurationLoaded, isOpen]);

  return (
    <div className="my-4">
      <div
        className="inline-flex items-center px-3 py-1 bg-muted dark:bg-gray-700 text-xs gap-1 rounded-sm"
        onClick={toggleOpen}
      >
        {(finished ?? true) ? (
          <div className="flex items-center h-6 gap-1">
            <IconThink size={16} />
            <span>{headerLabel}</span>
            {isOpen && loading && <span className="text-muted-foreground">{t('Loading...')}</span>}
          </div>
        ) : (
          headerLabel
        )}
        {isOpen ? (
          <IconChevronDown size={18} stroke="#6b7280" />
        ) : (
          <IconChevronRight size={18} stroke="#6b7280" />
        )}
      </div>
      <div 
        className="overflow-hidden transition-all duration-300 ease-in-out"
        style={{
          maxHeight: isOpen ? '2000px' : '0',
          opacity: isOpen ? 1 : 0,
        }}
      >
        <div className="px-2 text-gray-400 text-sm mt-2">
          <MemoizedReactMarkdown
            remarkPlugins={[remarkMath, remarkGfm]}
            rehypePlugins={[rehypeKatex as any]}
            components={{
              code({ node, className, inline, children, ...props }) {
                if (children.length) {
                  if (children[0] == '▍') {
                    return (
                      <span className="animate-pulse cursor-default mt-1">
                        ▍
                      </span>
                    );
                  }
                }

                const match = /language-(\w+)/.exec(className || '');

                return !inline ? (
                  <CodeBlock
                    key={Math.random()}
                    language={(match && match[1]) || ''}
                    value={String(children).replace(/\n$/, '')}
                    {...props}
                  />
                ) : (
                  <code className={className} {...props}>
                    {children}
                  </code>
                );
              },
              p({ children }) {
                return <p className="md-p">{children}</p>;
              },
              table({ children }) {
                return (
                  <table className="border-collapse border border-black px-3 py-1 dark:border-white">
                    {children}
                  </table>
                );
              },
              th({ children }) {
                return (
                  <th className="break-words border border-black bg-gray-500 px-3 py-1 text-white dark:border-white">
                    {children}
                  </th>
                );
              },
              td({ children }) {
                return (
                  <td className="break-words border border-black px-3 py-1 dark:border-white">
                    {children}
                  </td>
                );
              },
            }}
          >
            {`${preprocessLaTeX(content!)}${finished === false ? '▍' : ''}`}
          </MemoizedReactMarkdown>
        </div>
      </div>
    </div>
  );
};

export default ThinkingMessage;
