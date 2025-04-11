import useTranslation from '@/hooks/useTranslation';

import { ReactionMessageType } from '@/types/chatMessage';

import { IconThumbDown } from '../Icons';
import Tips from '../Tips/Tips';
import { Button } from '../ui/button';

import { cn } from '@/lib/utils';

interface Props {
  hidden?: boolean;
  disabled?: boolean;
  value?: boolean | null;
  onReactionMessage: (type: ReactionMessageType) => void;
}

export const ReactionBadResponseAction = (props: Props) => {
  const { t } = useTranslation();
  const { hidden, disabled, value, onReactionMessage } = props;

  const Render = () => {
    return (
      <Tips
        trigger={
          <Button
            disabled={disabled}
            variant="ghost"
            className={cn(
              'p-1 m-0 h-7 w-7 hidden sm:block',
              value === false && 'bg-muted',
            )}
            onClick={(e) => {
              onReactionMessage(ReactionMessageType.Bad);
              e.stopPropagation();
            }}
          >
            <IconThumbDown />
          </Button>
        }
        side="bottom"
        content={t('Dislike')!}
      />
    );
  };

  return <>{!hidden && Render()}</>;
};

export default ReactionBadResponseAction;
