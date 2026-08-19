import { IconChevronLeft, IconChevronRight } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Loader2 } from 'lucide-react';
import { MouseEvent, useState } from 'react';

interface Props {
  hidden?: boolean;
  disabledPrev?: boolean;
  disabledNext?: boolean;
  currentSelectIndex: number;
  messageIds: string[];
  onChangeMessage?: (messageId: string) => void | Promise<void>;
}

const PaginationLoader = () => (
  <span className="inline-flex animate-spin" aria-hidden="true">
    <Loader2 size={20} />
  </span>
);

export const PaginationAction = (props: Props) => {
  const {
    disabledPrev,
    disabledNext,
    currentSelectIndex,
    messageIds,
    onChangeMessage,
    hidden,
  } = props;
  const [loadingDirection, setLoadingDirection] = useState<'prev' | 'next' | null>(null);

  const handleChange = async (
    direction: 'prev' | 'next',
    messageId: string,
    event: MouseEvent<HTMLButtonElement>,
  ) => {
    event.stopPropagation();
    if (!onChangeMessage || loadingDirection !== null) return;
    setLoadingDirection(direction);
    try {
      await onChangeMessage(messageId);
    } finally {
      setLoadingDirection(null);
    }
  };

  const Render = () => {
    return (
      <div className="flex text-sm items-center">
        <Button
          variant="ghost"
          className="p-1 m-0 h-7 w-7 disabled:opacity-50"
          disabled={disabledPrev || loadingDirection !== null}
          onClick={(e) => {
            const index = currentSelectIndex - 1;
            void handleChange('prev', messageIds[index], e);
          }}
        >
          {loadingDirection === 'prev' ? (
            <PaginationLoader />
          ) : (
            <IconChevronLeft size={20} />
          )}
        </Button>
        <span className="font-bold">
          {`${currentSelectIndex + 1}/${messageIds.length}`}
        </span>
        <Button
          variant="ghost"
          className="p-1 m-0 h-7 w-7"
          disabled={disabledNext || loadingDirection !== null}
          onClick={(e) => {
            const index = currentSelectIndex + 1;
            void handleChange('next', messageIds[index], e);
          }}
        >
          {loadingDirection === 'next' ? (
            <PaginationLoader />
          ) : (
            <IconChevronRight size={20} />
          )}
        </Button>
      </div>
    );
  };

  return <>{!hidden && Render()}</>;
};

export default PaginationAction;
