import { FC, useMemo } from 'react';

import { loadComponentOnce } from '@/components/common/loadComponentOnce';

import LightMarkdown from './LightMarkdown';
import { MarkdownLoadingFallback, hasMathMarkdown } from './markdownShared';
import { normalizeMathDelimiters } from './normalizeMathDelimiters';

interface MarkdownRendererProps {
  className?: string;
  content: string;
  showCursor?: boolean;
}

const RichMarkdown = loadComponentOnce<MarkdownRendererProps>({
  cacheKey: 'Markdown/RichMarkdown',
  loader: () => import('./RichMarkdown').then((mod) => mod.default),
  renderFallback: () => <MarkdownLoadingFallback />,
});

const MarkdownRenderer: FC<MarkdownRendererProps> = ({
  className,
  content,
  showCursor,
}) => {
  const normalizedContent = useMemo(
    () => normalizeMathDelimiters(content),
    [content],
  );

  const MarkdownComponent = hasMathMarkdown(normalizedContent)
    ? RichMarkdown
    : LightMarkdown;

  return (
    <MarkdownComponent
      className={className}
      content={normalizedContent}
      showCursor={showCursor}
    />
  );
};

export default MarkdownRenderer;
