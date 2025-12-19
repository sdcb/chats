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

import { CodeBlock } from '@/components/Markdown/CodeBlock';
import { MemoizedReactMarkdown } from '@/components/Markdown/MemoizedReactMarkdown';

import { IconChevronRight, IconThink } from '../Icons';

import rehypeKatex from 'rehype-katex';
import { rehypeKatexDataMath } from '@/components/Markdown/rehypeKatexWithCopy';
import remarkGfm from 'remark-gfm';
import remarkMath from 'remark-math';
import type { Components as MarkdownComponents } from 'react-markdown';
import type {
  CodeProps,
  ReactMarkdownProps,
  TableDataCellProps,
  TableHeaderCellProps,
} from 'react-markdown/lib/ast-to-react';

const thinkingMarkdownComponents = {
  code({ node, className, inline, children, ...props }: CodeProps) {
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
  p({ children }: ReactMarkdownProps) {
    return <p className="md-p">{children}</p>;
  },
  table({ children }: ReactMarkdownProps) {
    return (
      <table className="border-collapse border border-black px-3 py-1 dark:border-white">
        {children}
      </table>
    );
  },
  th({ children }: TableHeaderCellProps) {
    return (
      <th className="break-words border border-black bg-gray-500 px-3 py-1 text-white dark:border-white">
        {children}
      </th>
    );
  },
  td({ children }: TableDataCellProps) {
    return (
      <td className="break-words border border-black px-3 py-1 dark:border-white">
        {children}
      </td>
    );
  },
} as unknown as MarkdownComponents;

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
        <div 
          className="transition-transform duration-300 ease-in-out"
          style={{ transform: isOpen ? 'rotate(90deg)' : 'rotate(0deg)' }}
        >
          <IconChevronRight size={18} stroke="#6b7280" />
        </div>
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
            rehypePlugins={[rehypeKatex as any, rehypeKatexDataMath]}
            components={thinkingMarkdownComponents}
          >
            {`${preprocessLaTeX(content!)}${finished === false ? '▍' : ''}`}
          </MemoizedReactMarkdown>
        </div>
      </div>
    </div>
  );
};

export default ThinkingMessage;
