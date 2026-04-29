import { FC, memo, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconCheck, IconChevronDown, IconClipboard } from '@/components/Icons';
import { loadComponentOnce } from '@/components/common/loadComponentOnce';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

interface Props {
  language: string;
  value: string;
}

const CodeBlockFallback: FC<Props> = ({ language, value }) => {
  const { t } = useTranslation();
  const [isCopied, setIsCopied] = useState(false);
  const [isExpanded, setIsExpanded] = useState(true);

  const copyToClipboard = (e: React.MouseEvent) => {
    e.stopPropagation();

    if (!navigator.clipboard || !navigator.clipboard.writeText) {
      return;
    }

    navigator.clipboard.writeText(value).then(() => {
      setIsCopied(true);
      setTimeout(() => setIsCopied(false), 2000);
    });
  };

  const toggleExpanded = () => setIsExpanded((value) => !value);

  const handleHeaderKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      toggleExpanded();
    }
  };

  return (
    <div className="codeblock relative font-sans text-base">
      <div className="overflow-hidden rounded-md border border-slate-300/80 bg-slate-50 shadow-sm dark:border-[#1b1d22] dark:bg-[#07090d]">
        <div
          className={`flex h-8 cursor-pointer select-none items-center justify-between gap-2 bg-slate-200/95 px-2.5 text-xs text-slate-700 dark:bg-[#010101] dark:text-slate-300 ${
            isExpanded ? 'border-b border-slate-300/80 dark:border-[#181a1f]' : ''
          }`}
          role="button"
          tabIndex={0}
          aria-expanded={isExpanded}
          onClick={toggleExpanded}
          onKeyDown={handleHeaderKeyDown}
          title={isExpanded ? t('Collapse code') : t('Expand code')}
        >
          <div className="flex min-w-0 items-center gap-1.5">
            <IconChevronDown
              className={`shrink-0 transition-transform duration-200 ${
                isExpanded ? '' : '-rotate-90'
              }`}
              size={14}
              stroke="currentColor"
            />
            <span className="truncate font-mono">{language || 'text'}</span>
          </div>

          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  className="flex shrink-0 items-center rounded p-1 text-xs text-slate-600 transition-colors hover:bg-slate-900/10 hover:text-slate-950 dark:text-slate-400 dark:hover:bg-white/10 dark:hover:text-white"
                  onClick={copyToClipboard}
                  aria-label={isCopied ? t('Copied') : t('Click Copy')}
                >
                  {isCopied ? (
                    <IconCheck stroke="currentColor" size={16} />
                  ) : (
                    <IconClipboard stroke="currentColor" size={16} />
                  )}
                </button>
              </TooltipTrigger>
              <TooltipContent>
                {isCopied ? t('Copied') : t('Click Copy')}
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </div>

        <div
          className="grid transition-[grid-template-rows] duration-200 ease-in-out"
          style={{ gridTemplateRows: isExpanded ? '1fr' : '0fr' }}
        >
          <div className="min-h-0 overflow-hidden bg-slate-50 dark:bg-[#07090d]">
            <pre className="m-0 overflow-x-auto bg-transparent p-3 text-sm leading-6 text-slate-900 dark:text-slate-100">
              <code className="whitespace-pre font-mono">{value}</code>
            </pre>
          </div>
        </div>
      </div>
    </div>
  );
};

const LazyCodeBlockCore = loadComponentOnce<Props>({
  cacheKey: 'Markdown/CodeBlockCore',
  loader: () => import('./CodeBlockCore').then((mod) => mod.CodeBlockCore),
  renderFallback: ({ language, value }) => (
    <CodeBlockFallback language={language} value={value} />
  ),
});

const LazyMermaidBlock = loadComponentOnce<{ value: string }>({
  cacheKey: 'Markdown/MermaidBlock',
  loader: () => import('./MermaidBlock').then((mod) => mod.MermaidBlock),
  renderFallback: ({ value }) => (
    <CodeBlockFallback language="mermaid" value={value} />
  ),
});

export const CodeBlock: FC<Props> = memo(({ language, value }) => {
  if (language === 'mermaid') {
    return <LazyMermaidBlock value={value} />;
  }

  return <LazyCodeBlockCore language={language} value={value} />;
});
CodeBlock.displayName = 'CodeBlock';
