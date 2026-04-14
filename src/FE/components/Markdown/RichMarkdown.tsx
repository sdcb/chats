import { FC, useEffect } from 'react';

import rehypeKatex from 'rehype-katex';
import remarkBreaks from 'remark-breaks';
import remarkGfm from 'remark-gfm';
import remarkMath from 'remark-math';

import { MemoizedReactMarkdown } from './MemoizedReactMarkdown';
import { ensureKatexStylesLoaded } from './katexAssetLoader';
import { rehypeKatexDataMath } from './rehypeKatexWithCopy';
import { markdownComponents } from './markdownShared';

interface RichMarkdownProps {
  className?: string;
  content: string;
}

const RichMarkdown: FC<RichMarkdownProps> = ({ className, content }) => {
  useEffect(() => {
    void ensureKatexStylesLoaded().catch((error) => {
      console.error('Failed to load KaTeX assets:', error);
    });
  }, []);

  return (
    <MemoizedReactMarkdown
      className={className}
      remarkPlugins={[remarkMath, remarkGfm, remarkBreaks]}
      rehypePlugins={[rehypeKatex as any, rehypeKatexDataMath]}
      components={markdownComponents}
    >
      {content}
    </MemoizedReactMarkdown>
  );
};

export default RichMarkdown;
