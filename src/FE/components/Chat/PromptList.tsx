import { MutableRefObject, useEffect } from 'react';

import { PromptSlim } from '@/types/prompt';

interface Props {
  prompts: PromptSlim[];
  activePromptIndex: number;
  onSelect: (index: number) => void;
  onMouseOver: (index: number) => void;
  promptListRef: MutableRefObject<HTMLUListElement | null>;
}

const PromptList = ({
  prompts,
  activePromptIndex,
  promptListRef,
  onSelect,
  onMouseOver,
}: Props) => {
  useEffect(() => {
    const list = promptListRef.current;
    const activeItem = list?.children.item(
      activePromptIndex,
    ) as HTMLElement | null;
    if (!list || !activeItem) return;

    const itemTop = activeItem.offsetTop;
    const itemBottom = itemTop + activeItem.offsetHeight;
    const visibleTop = list.scrollTop;
    const visibleBottom = visibleTop + list.clientHeight;

    if (itemTop < visibleTop) {
      list.scrollTop = itemTop;
    } else if (itemBottom > visibleBottom) {
      list.scrollTop = itemBottom - list.clientHeight;
    }
  }, [activePromptIndex, promptListRef, prompts.length]);

  return (
    <ul
      ref={promptListRef}
      className="relative z-30 max-h-52 w-full overflow-y-auto bg-background rounded-md border border-black/10 shadow-[0_0_10px_rgba(0,0,0,0.10)] dark:border-neutral-500 dark:text-white dark:shadow-[0_0_15px_rgba(0,0,0,0.10)]"
    >
      {prompts.map((prompt, index) => (
        <li
          key={prompt.id}
          className={`${
            index === activePromptIndex
              ? 'bg-gray-200 dark:bg-black dark:text-black'
              : ''
          } cursor-pointer px-3 py-2 text-sm text-black dark:text-white`}
          onMouseDown={(e) => {
            e.preventDefault();
            e.stopPropagation();
            onSelect(index);
          }}
          onMouseEnter={() => onMouseOver(index)}
        >
          {prompt.name}
        </li>
      ))}
    </ul>
  );
};

export default PromptList;
