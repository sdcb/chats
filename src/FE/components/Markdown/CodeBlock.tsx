import { FC, memo } from 'react';

import dynamic from 'next/dynamic';

interface Props {
  language: string;
  value: string;
}

const CodeBlockLoading = () => (
  <div className="rounded-md border bg-muted p-3">
    <div className="h-4 w-24 animate-pulse rounded bg-background/60" />
    <div className="mt-3 h-4 w-full animate-pulse rounded bg-background/60" />
    <div className="mt-2 h-4 w-4/5 animate-pulse rounded bg-background/60" />
  </div>
);

const LazyCodeBlockCore = dynamic<Props>(
  () => import('./CodeBlockCore').then((mod) => mod.CodeBlockCore),
  {
    loading: () => <CodeBlockLoading />,
  },
);

const LazyMermaidBlock = dynamic<{ value: string }>(
  () => import('./MermaidBlock').then((mod) => mod.MermaidBlock),
  {
    loading: () => <CodeBlockLoading />,
  },
);

export const CodeBlock: FC<Props> = memo(({ language, value }) => {
  if (language === 'mermaid') {
    return <LazyMermaidBlock value={value} />;
  }
  return <LazyCodeBlockCore language={language} value={value} />;
});
CodeBlock.displayName = 'CodeBlock';
