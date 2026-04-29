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
      <div className="overflow-hidden border bg-muted">
        <div
          className={`flex h-7 cursor-pointer select-none items-center justify-between gap-2 bg-muted-foreground/10 px-2 text-xs text-muted-foreground ${
            isExpanded ? 'border-b' : ''
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
                  className="flex shrink-0 items-center rounded p-1 text-xs text-muted-foreground transition-colors hover:bg-muted-foreground/10 hover:text-foreground"
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
          <div className="min-h-0 overflow-hidden">
            <pre className="m-0 overflow-x-auto p-3 text-sm leading-6">
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
