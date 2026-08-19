const countRun = (value: string, index: number, character: string) => {
  let end = index;
  while (value[end] === character) {
    end++;
  }
  return end - index;
};

const isEscaped = (value: string, index: number) => {
  let slashCount = 0;
  for (let i = index - 1; i >= 0 && value[i] === '\\'; i--) {
    slashCount++;
  }
  return slashCount % 2 === 1;
};

const findInlineCodeEnd = (
  value: string,
  start: number,
  delimiterLength: number,
) => {
  for (let i = start; i < value.length; i++) {
    if (value[i] !== '`') continue;

    const runLength = countRun(value, i, '`');
    if (runLength === delimiterLength) {
      return i + runLength;
    }
    i += runLength - 1;
  }
  return -1;
};

const getFence = (value: string, index: number) => {
  if (index > 0 && value[index - 1] !== '\n') return null;

  let markerIndex = index;
  while (markerIndex < value.length && value[markerIndex] === ' ') {
    markerIndex++;
  }
  if (markerIndex - index > 3) return null;

  const marker = value[markerIndex];
  if (marker !== '`' && marker !== '~') return null;

  const length = countRun(value, markerIndex, marker);
  return length >= 3 ? { marker, length } : null;
};

const findFenceEnd = (
  value: string,
  start: number,
  marker: string,
  delimiterLength: number,
) => {
  let lineStart = value.indexOf('\n', start);
  if (lineStart === -1) return value.length;
  lineStart++;

  while (lineStart < value.length) {
    let markerIndex = lineStart;
    while (markerIndex < value.length && value[markerIndex] === ' ') {
      markerIndex++;
    }

    const markerLength = countRun(value, markerIndex, marker);
    const lineEnd = value.indexOf('\n', markerIndex);
    const contentEnd = lineEnd === -1 ? value.length : lineEnd;
    const trailingContent = value.slice(markerIndex + markerLength, contentEnd);

    // CommonMark closing fences may contain only indentation, the fence marker,
    // and trailing whitespace. Text after the marker means this is code content,
    // not the end of the fenced block.
    if (
      markerIndex - lineStart <= 3 &&
      value[markerIndex] === marker &&
      markerLength >= delimiterLength &&
      /^[\t \r]*$/.test(trailingContent)
    ) {
      return lineEnd === -1 ? value.length : lineEnd + 1;
    }

    const nextLine = value.indexOf('\n', lineStart);
    if (nextLine === -1) return value.length;
    lineStart = nextLine + 1;
  }

  return value.length;
};

const findMathEnd = (
  value: string,
  start: number,
  closingDelimiter: '\\)' | '\\]',
  multiline: boolean,
) => {
  for (let i = start; i < value.length - 1; i++) {
    if (!multiline && value[i] === '\n') return -1;
    if (value[i] === '`') return -1;

    if (value.startsWith(closingDelimiter, i) && !isEscaped(value, i)) {
      return i;
    }
  }
  return -1;
};

/**
 * Normalizes unambiguous LaTeX delimiters before Markdown parsing.
 * Code spans/blocks and incomplete delimiters are deliberately left untouched.
 */
export const normalizeMathDelimiters = (value: string) => {
  let result = '';
  let index = 0;

  while (index < value.length) {
    const fence = getFence(value, index);
    if (fence) {
      const end = findFenceEnd(value, index, fence.marker, fence.length);
      result += value.slice(index, end);
      index = end;
      continue;
    }

    if (value[index] === '`') {
      const delimiterLength = countRun(value, index, '`');
      const end = findInlineCodeEnd(
        value,
        index + delimiterLength,
        delimiterLength,
      );

      if (end === -1) {
        result += value.slice(index);
        break;
      }

      result += value.slice(index, end);
      index = end;
      continue;
    }

    if (value.startsWith('\\(', index) && !isEscaped(value, index)) {
      const end = findMathEnd(value, index + 2, '\\)', false);
      if (end !== -1) {
        result += `$$${value.slice(index + 2, end)}$$`;
        index = end + 2;
        continue;
      }
    }

    if (value.startsWith('\\[', index) && !isEscaped(value, index)) {
      const end = findMathEnd(value, index + 2, '\\]', true);
      if (end !== -1) {
        result += `$$${value.slice(index + 2, end)}$$`;
        index = end + 2;
        continue;
      }
    }

    result += value[index];
    index++;
  }

  return result;
};
