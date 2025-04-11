import useTranslation from '@/hooks/useTranslation';

import { IconRefresh } from '@/components/Icons';

import Tips from '../Tips/Tips';
import { Button } from '../ui/button';

interface Props {
  hidden?: boolean;
  disabled?: boolean;
  modelName?: string;
  onRegenerate: () => any;
}

export const RegenerateAction = (props: Props) => {
  const { t } = useTranslation();
  const { hidden, disabled, onRegenerate } = props;

  const Render = () => {
    return (
      <Tips
        trigger={
          <Button
            disabled={disabled}
            variant="ghost"
            className="p-1 m-0 h-7 w-7"
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
