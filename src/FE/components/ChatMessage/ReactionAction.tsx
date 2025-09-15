import { forwardRef } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { ReactionMessageType } from '@/types/chatMessage';

import { IconThumbUp, IconThumbUpFilled, IconThumbDown, IconThumbDownFilled } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';

import { cn } from '@/lib/utils';

interface Props {
  hidden?: boolean;
  disabled?: boolean;
  value?: boolean | null; // true: good, false: bad, null: no reaction
  isHoverVisible?: boolean;
  onReactionMessage: (type: ReactionMessageType) => void;
}

export const ReactionAction = forwardRef<HTMLDivElement, Props>(
  (props, ref) => {
    const { t } = useTranslation();
    const {
      hidden,
      disabled,
      value,
      isHoverVisible,
      onReactionMessage,
    } = props;

    const Render = () => {
      return (
        <div 
          ref={ref}
          className={cn(
            isHoverVisible ? 'invisible' : 'visible',
            'flex group-hover:visible focus-within:visible',
          )}
        >
          {/* 点赞按钮 */}
          <Tips
            trigger={
              <Button
                disabled={disabled}
                variant="ghost"
                className={cn(
                  'p-1 m-0 h-7 w-7 rounded-r-none transition-all duration-200 overflow-hidden',
                  value === false ? 'w-0 p-0 opacity-0 scale-0' : 'w-7 opacity-100 scale-100',
                )}
                onClick={(e) => {
                  onReactionMessage(ReactionMessageType.Good);
                  e.stopPropagation();
                }}
              >
                {value === true ? (
                  <IconThumbUpFilled className="w-5 h-5" />
                ) : (
                  <IconThumbUp className="w-5 h-5" />
                )}
              </Button>
            }
            side="bottom"
            content={t('Like')!}
          />

          {/* 点踩按钮 */}
          <Tips
            trigger={
              <Button
                disabled={disabled}
                variant="ghost"
                className={cn(
                  'p-1 m-0 h-7 w-7 rounded-l-none transition-all duration-200 overflow-hidden',
                  value === true ? 'w-0 p-0 opacity-0 scale-0' : 'w-7 opacity-100 scale-100',
                )}
                onClick={(e) => {
                  onReactionMessage(ReactionMessageType.Bad);
                  e.stopPropagation();
                }}
              >
                {value === false ? (
                  <IconThumbDownFilled className="w-5 h-5" />
                ) : (
                  <IconThumbDown className="w-5 h-5" />
                )}
              </Button>
            }
            side="bottom"
            content={t('Dislike')!}
          />
        </div>
      );
    };

    return <>{!hidden && Render()}</>;
  },
);

ReactionAction.displayName = 'ReactionAction';

export default ReactionAction;