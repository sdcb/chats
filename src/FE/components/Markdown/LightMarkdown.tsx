import { FC } from 'react';

import remarkBreaks from 'remark-breaks';
import remarkGfm from 'remark-gfm';

import { MemoizedReactMarkdown } from './MemoizedReactMarkdown';
import { markdownComponents } from './markdownShared';

interface LightMarkdownProps {
  className?: string;
  content: string;
}

const LightMarkdown: FC<LightMarkdownProps> = ({ className, content }) => {
  return (
    <MemoizedReactMarkdown
      className={className}
      remarkPlugins={[remarkGfm, remarkBreaks]}
      components={markdownComponents}
    >
      {content}
    </MemoizedReactMarkdown>
  );
};

export default LightMarkdown;
