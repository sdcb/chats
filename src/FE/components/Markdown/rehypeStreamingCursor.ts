import type { Element, Root, RootContent } from 'hast';
import type { Plugin } from 'unified';

interface Options {
  enabled?: boolean;
}

const CURSOR_CONTAINER_TAGS = new Set([
  'p',
  'h1',
  'h2',
  'h3',
  'h4',
  'h5',
  'h6',
  'li',
  'blockquote',
  'td',
  'th',
  'div',
]);

const MATH_CLASS_NAMES = new Set([
  'katex',
  'katex-display',
  'math',
  'math-block',
  'math-display',
  'math-inline',
]);

const isWhitespaceText = (node: RootContent) =>
  node.type === 'text' && node.value.trim().length === 0;

const isMathElement = (node: Element) => {
  const className = node.properties?.className;
  return (
    Array.isArray(className) &&
    className.some((value) => MATH_CLASS_NAMES.has(String(value)))
  );
};

const findLastCursorContainer = (node: RootContent): Element | null => {
  if (node.type !== 'element') return null;
  if (node.tagName === 'pre' || node.tagName === 'code' || isMathElement(node)) {
    return null;
  }

  for (let i = node.children.length - 1; i >= 0; i--) {
    const child = node.children[i];
    if (isWhitespaceText(child)) continue;

    const nestedContainer = findLastCursorContainer(child);
    if (nestedContainer) return nestedContainer;
    break;
  }

  return CURSOR_CONTAINER_TAGS.has(node.tagName) ? node : null;
};

const createCursor = (): Element => ({
  type: 'element',
  tagName: 'span',
  properties: {
    className: ['animate-pulse', 'cursor-default', 'streaming-cursor'],
  },
  children: [{ type: 'text', value: '▍' }],
});

const insertCursor = (container: Root | Element) => {
  let insertionIndex = container.children.length;

  // A trailing Markdown line break would push a sibling cursor to the next line.
  // Insert before trailing whitespace and <br> nodes so it remains on the last
  // visible line without altering the original Markdown source.
  while (insertionIndex > 0) {
    const child = container.children[insertionIndex - 1];
    if (
      isWhitespaceText(child) ||
      (child.type === 'element' && child.tagName === 'br')
    ) {
      insertionIndex--;
      continue;
    }
    break;
  }

  container.children.splice(insertionIndex, 0, createCursor());
};

export const rehypeStreamingCursor: Plugin<[Options?], Root> = function (
  options = {},
) {
  return function (tree) {
    if (!options.enabled) return;

    let lastContent: RootContent | undefined;
    for (let i = tree.children.length - 1; i >= 0; i--) {
      if (!isWhitespaceText(tree.children[i])) {
        lastContent = tree.children[i];
        break;
      }
    }

    const container = lastContent
      ? findLastCursorContainer(lastContent)
      : null;
    insertCursor(container ?? tree);
  };
};

export default rehypeStreamingCursor;
