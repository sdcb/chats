import useTranslation from '@/hooks/useTranslation';

import { IconRefresh } from '@/components/Icons';

import Tips from '../Tips/Tips';
import { Button } from '../ui/button';

import { cn } from '@/lib/utils';

interface Props {
  hidden?: boolean;
  disabled?: boolean;
  isHoverVisible?: boolean;
  modelName?: string;
  onRegenerate: () => any;
}

export const RegenerateAction = (props: Props) => {
  const { t } = useTranslation();
  const { hidden, disabled, isHoverVisible, onRegenerate } = props;

  const Render = () => {
    return (
      <Tips
        trigger={
          <Button
            disabled={disabled}
            variant="ghost"
            className={cn(
              isHoverVisible ? 'invisible' : 'visible',
              'p-1 m-0 h-7 w-7 group-hover:visible focus:visible',
            )}
            onClick={(e) => {
              onRegenerate();
              e.stopPropagation();
            }}
          >
            <IconRefresh />
          </Button>
        }
        side="bottom"
        content={t('Regenerate')!}
      />
    );
  };

  return <>{!hidden && Render()}</>;
};

export default RegenerateAction;
