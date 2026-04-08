import { FC, memo } from 'react';

import { loadComponentOnce } from '@/components/common/loadComponentOnce';

interface Props {
  language: string;
  value: string;
}

const CodeBlockFallback: FC<Props> = ({ language, value }) => (
  <div className="codeblock relative font-sans text-base group">
    <div className="relative overflow-hidden border bg-muted">
      {language ? (
        <div className="absolute right-2 top-2 text-xs text-muted-foreground">
          {language}
        </div>
      ) : null}
      <pre className="m-0 overflow-x-auto p-3 text-sm leading-6">
        <code className="whitespace-pre font-mono">{value}</code>
      </pre>
    </div>
  </div>
);

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
