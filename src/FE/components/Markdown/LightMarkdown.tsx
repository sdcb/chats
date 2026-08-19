import { FC } from 'react';

import remarkBreaks from 'remark-breaks';
import remarkGfm from 'remark-gfm';

import { MemoizedReactMarkdown } from './MemoizedReactMarkdown';
import { markdownComponents } from './markdownShared';
import { rehypeStreamingCursor } from './rehypeStreamingCursor';

interface LightMarkdownProps {
  className?: string;
  content: string;
  showCursor?: boolean;
}

const LightMarkdown: FC<LightMarkdownProps> = ({ className, content, showCursor }) => {
  return (
    <MemoizedReactMarkdown
      key={showCursor ? 'streaming' : 'complete'}
      className={className}
      remarkPlugins={[remarkGfm, remarkBreaks]}
      rehypePlugins={[[rehypeStreamingCursor, { enabled: showCursor }]]}
      components={markdownComponents}
    >
      {content}
    </MemoizedReactMarkdown>
  );
};

export default LightMarkdown;
