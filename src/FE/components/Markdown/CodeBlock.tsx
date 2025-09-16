import { FC, memo } from 'react';
import { MermaidBlock } from './MermaidBlock';
import { CodeBlockCore } from './CodeBlockCore';

interface Props {
  language: string;
  value: string;
}

export const CodeBlock: FC<Props> = memo(({ language, value }) => {
  // 如果是mermaid语言，使用MermaidBlock组件
  if (language === 'mermaid') {
    return <MermaidBlock value={value} />;
  }
  return <CodeBlockCore language={language} value={value} />;
});
CodeBlock.displayName = 'CodeBlock';
