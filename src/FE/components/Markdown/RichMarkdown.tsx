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
  showCursor?: boolean;
}

const RichMarkdown: FC<RichMarkdownProps> = ({ className, content, showCursor }) => {
  useEffect(() => {
    void ensureKatexStylesLoaded().catch((error) => {
      console.error('Failed to load KaTeX assets:', error);
    });
  }, []);

  const markdown = (
    <MemoizedReactMarkdown
      remarkPlugins={[
        [remarkMath, { singleDollarTextMath: false }],
        remarkGfm,
        remarkBreaks,
      ]}
      rehypePlugins={[rehypeKatex as any, rehypeKatexDataMath]}
      components={markdownComponents}
    >
      {content}
    </MemoizedReactMarkdown>
  );

  if (!className && !showCursor) {
    return markdown;
  }

  return (
    <div className={`${className ?? ''}${showCursor ? ' markdown-streaming' : ''}`}>
      {markdown}
      {showCursor && <span className="animate-pulse cursor-default">▍</span>}
    </div>
  );
};

export default RichMarkdown;
