import { FC, useEffect } from 'react';

import rehypeKatex from 'rehype-katex';
import remarkBreaks from 'remark-breaks';
import remarkGfm from 'remark-gfm';
import remarkMath from 'remark-math';

import { MemoizedReactMarkdown } from './MemoizedReactMarkdown';
import { ensureKatexStylesLoaded } from './katexAssetLoader';
import { rehypeKatexDataMath } from './rehypeKatexWithCopy';
import { markdownComponents } from './markdownShared';
import { rehypeStreamingCursor } from './rehypeStreamingCursor';

interface RichMarkdownProps {
  className?: string;
  content: string;
  showCursor?: boolean;
}

const RichMarkdown: FC<RichMarkdownProps> = ({ className, content, showCursor }) => {
  useEffect(() => {
    void ensureKatexStylesLoaded().catch((error) => {
      console.error('Failed to load KaTeX assets:', error);
    });
  }, []);

  return (
    <MemoizedReactMarkdown
      key={showCursor ? 'streaming' : 'complete'}
      className={className}
      remarkPlugins={[
        [remarkMath, { singleDollarTextMath: false }],
        remarkGfm,
        remarkBreaks,
      ]}
      rehypePlugins={[
        rehypeKatex as any,
        rehypeKatexDataMath,
        [rehypeStreamingCursor, { enabled: showCursor }],
      ]}
      components={markdownComponents}
    >
      {content}
    </MemoizedReactMarkdown>
  );
};

export default RichMarkdown;
