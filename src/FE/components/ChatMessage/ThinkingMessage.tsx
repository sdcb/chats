import { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { preprocessLaTeX } from '@/utils/chats';

import { ChatSpanStatus } from '@/types/chat';

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
}

const ThinkingMessage = (props: Props) => {
  const { content, chatStatus, reasoningDuration } = props;
  const { t } = useTranslation();

  const [isOpen, setIsOpen] = useState(false);

  useEffect(() => {
    if (chatStatus === ChatSpanStatus.Reasoning) {
      setIsOpen(true);
    } else if (chatStatus === ChatSpanStatus.Chatting) {
      setIsOpen(false);
    }
  }, [chatStatus]);

  return (
    <div className="my-4">
      <div
        className="inline-flex items-center px-3 py-1 bg-muted dark:bg-gray-700 text-xs gap-1 rounded-sm"
        onClick={() => {
          setIsOpen(!isOpen);
        }}
      >
        {chatStatus === ChatSpanStatus.Reasoning ? (
          t('Thinking...')
        ) : (
          <div className="flex items-center h-6">
            <IconThink size={16} />
            {t('Deeply thought (took {{time}} seconds)', {
              time: Math.floor((reasoningDuration || 0) / 1000),
            })}
          </div>
        )}
        {isOpen ? (
          <IconChevronDown size={18} stroke="#6b7280" />
        ) : (
          <IconChevronRight size={18} stroke="#6b7280" />
        )}
      </div>
      {isOpen && (
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
      )}
    </div>
  );
};

export default ThinkingMessage;
