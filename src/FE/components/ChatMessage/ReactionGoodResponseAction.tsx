import useTranslation from '@/hooks/useTranslation';

import { ReactionMessageType } from '@/types/chatMessage';

import { IconThumbUp } from '../Icons';
import Tips from '../Tips/Tips';
import { Button } from '../ui/button';

import { cn } from '@/lib/utils';

interface Props {
  hidden?: boolean;
  disabled?: boolean;
  value?: boolean | null;
  onReactionMessage: (type: ReactionMessageType) => void;
}

export const ReactionGoodResponseAction = (props: Props) => {
  const { t } = useTranslation();
  const { hidden, disabled, value, onReactionMessage } = props;

  const Render = () => {
    return (
      <Tips
        trigger={
          <Button
            disabled={disabled}
            variant="ghost"
            className={cn('p-1 m-0 h-7 w-7', value === true && 'bg-muted')}
            onClick={(e) => {
              onReactionMessage(ReactionMessageType.Good);
              e.stopPropagation();
            }}
          >
            <IconThumbUp />
          </Button>
        }
        side="bottom"
        content={t('Like')!}
      />
    );
  };

  return <>{!hidden && Render()}</>;
};

export default ReactionGoodResponseAction;
