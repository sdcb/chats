import { ReactNode } from 'react';

import { CodeBlock } from './CodeBlock';

import type { Components as MarkdownComponents } from 'react-markdown';
import type {
  CodeProps,
  ReactMarkdownProps,
  TableDataCellProps,
  TableHeaderCellProps,
} from 'react-markdown/lib/ast-to-react';

const INLINE_MATH_REGEX = /(^|[^\\])\$[^$\n]+\$/;
const BLOCK_MATH_REGEX = /(^|[^\\])\$\$[\s\S]+?\$\$/;
const KATEX_ESCAPE_REGEX = /\\\(|\\\)|\\\[|\\\]/;

export const hasMathMarkdown = (value: string) =>
  KATEX_ESCAPE_REGEX.test(value) ||
  BLOCK_MATH_REGEX.test(value) ||
  INLINE_MATH_REGEX.test(value);

export const appendStreamingCursor = (
  value: string,
  showCursor: boolean,
) => `${value}${showCursor ? '▍' : ''}`;

export const markdownComponents = {
  code({ className, inline, children, ...props }: CodeProps) {
    if (children.length && children[0] == '▍') {
      return <span className="mt-1 animate-pulse cursor-default">▍</span>;
    }

    const match = /language-(\w+)/.exec(className || '');

    return !inline ? (
      <CodeBlock
        key={Math.random()}
        language={(match && match[1]) || ''}
        value={String(children).replace(/\n$/, '')}
        {...props}
      />
    ) : (
      <code className={className} {...props}>
        {children}
      </code>
    );
  },
  p({ children }: ReactMarkdownProps) {
    return <p className="md-p">{children}</p>;
  },
  table({ children }: ReactMarkdownProps) {
    return (
      <table className="border-collapse border border-black px-3 py-1 dark:border-white">
        {children}
      </table>
    );
  },
  th({ children }: TableHeaderCellProps) {
    return (
      <th className="break-words border border-black bg-gray-500 px-3 py-1 text-white dark:border-white">
        {children}
      </th>
    );
  },
  td({ children }: TableDataCellProps) {
    return (
      <td className="break-words border border-black px-3 py-1 dark:border-white">
        {children}
      </td>
    );
  },
} as unknown as MarkdownComponents;

export const MarkdownLoadingFallback = ({
  children,
}: {
  children?: ReactNode;
}) => (
  <div className="space-y-2">
    <div className="h-4 w-4/5 animate-pulse rounded bg-muted" />
    <div className="h-4 w-full animate-pulse rounded bg-muted" />
    <div className="h-4 w-3/5 animate-pulse rounded bg-muted" />
    {children}
  </div>
);
