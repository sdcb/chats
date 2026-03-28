import { ComponentType, FC, memo, useEffect, useState } from 'react';

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

const useLazyComponent = <TProps extends object>(
  loader: () => Promise<ComponentType<TProps>>,
) => {
  const [component, setComponent] = useState<ComponentType<TProps> | null>(null);

  useEffect(() => {
    let mounted = true;

    void loader().then((loadedComponent) => {
      if (mounted) {
        setComponent(() => loadedComponent);
      }
    });

    return () => {
      mounted = false;
    };
  }, [loader]);

  return component;
};

export const CodeBlock: FC<Props> = memo(({ language, value }) => {
  const LazyCodeBlockCore = useLazyComponent<Props>(() =>
    import('./CodeBlockCore').then((mod) => mod.CodeBlockCore),
  );
  const LazyMermaidBlock = useLazyComponent<{ value: string }>(() =>
    import('./MermaidBlock').then((mod) => mod.MermaidBlock),
  );

  if (language === 'mermaid') {
    return LazyMermaidBlock ? (
      <LazyMermaidBlock value={value} />
    ) : (
      <CodeBlockFallback language={language} value={value} />
    );
  }

  return LazyCodeBlockCore ? (
    <LazyCodeBlockCore language={language} value={value} />
  ) : (
    <CodeBlockFallback language={language} value={value} />
  );
});
CodeBlock.displayName = 'CodeBlock';
