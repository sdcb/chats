import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { isMobile } from '@/utils/common';
import useTranslation from '@/hooks/useTranslation';

import { ChatRole, MessageContentType } from '@/types/chat';
import { IChatMessage, getMessageContents } from '@/types/chatMessage';

import { IconArrowDown, IconArrowUp } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import Tips from '@/components/Tips/Tips';
import { cn } from '@/lib/utils';

const MIN_WIDTH = 12;
const MAX_WIDTH = 26;
const MAX_CONTENT_LENGTH = 320;
const MIN_MESSAGES_THRESHOLD = 3;

const getIndicatorWidth = (content: string): number => {
  if (!content) return MIN_WIDTH;
  const ratio = Math.min(content.length / MAX_CONTENT_LENGTH, 1);
  return MIN_WIDTH + (MAX_WIDTH - MIN_WIDTH) * ratio;
};

const getPreviewText = (content: string): string => {
  if (!content) return '';
  const normalized = content.replaceAll(/\s+/g, ' ').trim();
  if (!normalized) return '';
  return normalized.slice(0, 100) + (normalized.length > 100 ? '...' : '');
};

const getMessagePlainText = (message: IChatMessage): string => {
  const contents = getMessageContents(message);
  const parts: string[] = [];

  contents.forEach((content) => {
    switch (content.$type) {
      case MessageContentType.text:
      case MessageContentType.reasoning:
      case MessageContentType.error:
        parts.push(content.c);
        break;
      case MessageContentType.toolCall:
        parts.push(`${content.n} ${content.p}`.trim());
        break;
      case MessageContentType.toolResponse:
        parts.push(content.r);
        break;
      default:
        break;
    }
  });

  return parts.join(' ').trim();
};

const getFirstTextContent = (message: IChatMessage): string => {
  const contents = getMessageContents(message);
  const textContent = contents.find(
    (content) => content.$type === MessageContentType.text,
  );
  return textContent && 'c' in textContent ? textContent.c : '';
};

type MinimapIndicator = {
  id: string;
  role: ChatRole;
  preview: string;
  width: number;
};

interface ChatMiniMapProps {
  messages: IChatMessage[][];
  containerRef: React.RefObject<HTMLDivElement | null>;
}

const ChatMiniMap = memo(({ messages, containerRef }: ChatMiniMapProps) => {
  const { t } = useTranslation();
  const [activeIndex, setActiveIndex] = useState<number | null>(null);
  const [isHovered, setIsHovered] = useState(false);
  const offsetsRef = useRef<(number | null)[]>([]);

  const indicators = useMemo<MinimapIndicator[]>(() => {
    const list: MinimapIndicator[] = [];
    messages.forEach((group) => {
      group.forEach((message) => {
        if (message.role !== ChatRole.User && message.role !== ChatRole.Assistant) {
          return;
        }
        const content =
          message.role === ChatRole.Assistant
            ? getFirstTextContent(message)
            : getMessagePlainText(message);
        list.push({
          id: message.id,
          role: message.role,
          preview: getPreviewText(content),
          width: getIndicatorWidth(content),
        });
      });
    });
    return list;
  }, [messages]);

  const updateOffsets = useCallback(() => {
    const container = containerRef.current;
    if (!container) return;

    const containerTop = container.getBoundingClientRect().top;
    const scrollTop = container.scrollTop;
    offsetsRef.current = indicators.map((indicator) => {
      const element = container.querySelector(
        `[data-message-id="${indicator.id}"]`,
      ) as HTMLElement | null;
      if (!element) return null;
      const rect = element.getBoundingClientRect();
      return rect.top - containerTop + scrollTop;
    });
  }, [containerRef, indicators]);

  const updateActiveIndex = useCallback(() => {
    const container = containerRef.current;
    if (!container || indicators.length === 0) {
      setActiveIndex(null);
      return;
    }

    const offsets = offsetsRef.current;
    const target = container.scrollTop + 4;
    let nextIndex: number | null = null;

    for (let i = 0; i < offsets.length; i += 1) {
      const offset = offsets[i];
      if (offset === null) continue;

      if (offset <= target) {
        nextIndex = i;
      } else if (nextIndex === null) {
        nextIndex = i;
        break;
      }
    }

    if (nextIndex === null && offsets.length > 0) {
      nextIndex = offsets.length - 1;
    }

    setActiveIndex((prev) => (prev === nextIndex ? prev : nextIndex));
  }, [containerRef, indicators.length]);

  useEffect(() => {
    updateOffsets();
    updateActiveIndex();
  }, [indicators, updateOffsets, updateActiveIndex]);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const handleScroll = () => updateActiveIndex();
    container.addEventListener('scroll', handleScroll, { passive: true });

    const handleResize = () => updateOffsets();
    window.addEventListener('resize', handleResize);

    return () => {
      container.removeEventListener('scroll', handleScroll);
      window.removeEventListener('resize', handleResize);
    };
  }, [containerRef, updateActiveIndex, updateOffsets]);

  const handleJump = useCallback(
    (index: number) => {
      const indicator = indicators[index];
      if (!indicator) return;
      const element = containerRef.current?.querySelector(
        `[data-message-id="${indicator.id}"]`,
      ) as HTMLElement | null;
      element?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      setActiveIndex(index);
    },
    [containerRef, indicators],
  );

  const handleStep = useCallback(
    (direction: 'prev' | 'next') => {
      if (indicators.length === 0) return;

      let targetIndex: number;

      if (activeIndex !== null) {
        const delta = direction === 'prev' ? -1 : 1;
        targetIndex = Math.min(
          Math.max(activeIndex + delta, 0),
          indicators.length - 1,
        );
      } else {
        const container = containerRef.current;
        if (!container) return;
        const scrollTop = container.scrollTop;
        const offsets = offsetsRef.current;

        if (direction === 'prev') {
          let found = 0;
          for (let i = offsets.length - 1; i >= 0; i -= 1) {
            const offset = offsets[i];
            if (offset !== null && offset < scrollTop - 1) {
              found = i;
              break;
            }
          }
          targetIndex = found;
        } else {
          let found = offsets.length - 1;
          for (let i = 0; i < offsets.length; i += 1) {
            const offset = offsets[i];
            if (offset !== null && offset > scrollTop + 1) {
              found = i;
              break;
            }
          }
          targetIndex = found;
        }
      }

      handleJump(targetIndex);
    },
    [activeIndex, containerRef, handleJump, indicators.length],
  );

  if (indicators.length <= MIN_MESSAGES_THRESHOLD) return null;

  const showArrows = isMobile() || isHovered;

  return (
    <div className="pointer-events-none absolute right-2 top-1/2 z-10 -translate-y-1/2">
      <div
        className="pointer-events-auto flex flex-col items-end gap-1"
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
      >
        <Button
          size="xs"
          variant="ghost"
          className={cn(
            'h-6 w-6 p-0 transition-opacity',
            showArrows ? 'opacity-100' : 'opacity-0',
          )}
          onClick={() => handleStep('prev')}
        >
          <IconArrowUp size={14} />
        </Button>
        <div className="flex max-h-[50vh] flex-col items-end gap-0.5 overflow-y-auto pr-0">
          {indicators.map((indicator, index) => {
            const isActive = activeIndex === index;
            const roleLabel =
              indicator.role === ChatRole.User ? t('User') : t('Assistant');
            const tooltipContent = indicator.preview ? (
              <div className="max-w-[240px] space-y-1">
                <div className="text-xs text-muted-foreground">{roleLabel}</div>
                <div className="text-xs">{indicator.preview}</div>
              </div>
            ) : (
              roleLabel
            );

            return (
              <Tips
                key={indicator.id}
                side="left"
                content={tooltipContent}
                trigger={
                  <button
                    type="button"
                    aria-current={isActive ? 'true' : undefined}
                    aria-label={`Jump to message ${index + 1}`}
                    onClick={() => handleJump(index)}
                    className="flex h-2 items-center justify-end"
                    style={{ width: `${indicator.width}px` }}
                  >
                    <span
                      className={cn(
                        'h-[2px] w-full rounded-full bg-muted-foreground/40 transition-colors',
                        isActive && 'bg-primary',
                      )}
                    />
                  </button>
                }
              />
            );
          })}
        </div>
        <Button
          size="xs"
          variant="ghost"
          className={cn(
            'h-6 w-6 p-0 transition-opacity',
            showArrows ? 'opacity-100' : 'opacity-0',
          )}
          onClick={() => handleStep('next')}
        >
          <IconArrowDown size={14} />
        </Button>
      </div>
    </div>
  );
});

ChatMiniMap.displayName = 'ChatMiniMap';

export default ChatMiniMap;
