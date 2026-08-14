import { FC } from 'react';

import remarkBreaks from 'remark-breaks';
import remarkGfm from 'remark-gfm';

import { MemoizedReactMarkdown } from './MemoizedReactMarkdown';
import { markdownComponents } from './markdownShared';

interface LightMarkdownProps {
  className?: string;
  content: string;
  showCursor?: boolean;
}

const LightMarkdown: FC<LightMarkdownProps> = ({ className, content, showCursor }) => {
  const markdown = (
    <MemoizedReactMarkdown
      remarkPlugins={[remarkGfm, remarkBreaks]}
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

export default LightMarkdown;
