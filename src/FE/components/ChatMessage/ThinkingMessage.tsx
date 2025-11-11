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

import { ChatSpanStatus } from '@/types/chat';
import { IStepGenerateInfo } from '@/types/chatMessage';

import { CodeBlock } from '@/components/Markdown/CodeBlock';
import { MemoizedReactMarkdown } from '@/components/Markdown/MemoizedReactMarkdown';

import { IconChevronDown, IconChevronRight, IconThink } from '../Icons';

import rehypeKatex from 'rehype-katex';
import remarkGfm from 'remark-gfm';
import remarkMath from 'remark-math';

interface Props {
  readonly?: boolean;
  content: string;
  chatStatus: ChatSpanStatus;
  reasoningDuration?: number;
  messageId: string;
  chatId?: string;
  chatShareId?: string;
}

const ThinkingMessage = (props: Props) => {
  const { content, chatStatus, reasoningDuration, messageId, chatId, chatShareId } = props;
  const { t } = useTranslation();

  const [isOpen, setIsOpen] = useState(false);
  const [displayDurationMs, setDisplayDurationMs] = useState<number | null>(
    typeof reasoningDuration === 'number' && reasoningDuration > 0
      ? reasoningDuration
      : null,
  );
  const [loading, setLoading] = useState(false);

  const cacheKey = useMemo<GenerateInfoCacheKey>(
    () => ({ turnId: messageId, chatId, chatShareId }),
    [messageId, chatId, chatShareId],
  );

  useEffect(() => {
    if (chatStatus === ChatSpanStatus.Reasoning) {
      setIsOpen(true);
    } else if (chatStatus === ChatSpanStatus.Chatting) {
      setIsOpen(false);
    }
  }, [chatStatus]);

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
    if (!nextState) {
      return;
    }
    void ensureDurationLoaded();
  }, [ensureDurationLoaded, isOpen]);

  const headerLabel = useMemo(() => {
    if (chatStatus === ChatSpanStatus.Reasoning) {
      return t('Thinking...');
    }
    if ((displayDurationMs ?? 0) > 0) {
      const seconds = Math.floor((displayDurationMs ?? 0) / 1000);
      return t('Deeply thought (took {{time}} seconds)', { time: seconds });
    }
    return t('Deeply thought');
  }, [chatStatus, displayDurationMs, t]);

  return (
    <div className="my-4">
      <div
        className="inline-flex items-center px-3 py-1 bg-muted dark:bg-gray-700 text-xs gap-1 rounded-sm"
        onClick={toggleOpen}
      >
        {chatStatus === ChatSpanStatus.Reasoning ? (
          headerLabel
        ) : (
          <div className="flex items-center h-6 gap-1">
            <IconThink size={16} />
            <span>{headerLabel}</span>
            {isOpen && loading && <span className="text-muted-foreground">{t('Loading...')}</span>}
          </div>
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
            {`${preprocessLaTeX(content!)}${
              chatStatus === ChatSpanStatus.Reasoning ? '▍' : ''
            }`}
          </MemoizedReactMarkdown>
        </div>
      </div>
    </div>
  );
};

export default ThinkingMessage;
