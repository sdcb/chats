import useTranslation from '@/hooks/useTranslation';

import { IconRefresh } from '@/components/Icons';

import { Separator } from '../ui/separator';

interface Props {
  hidden?: boolean;
  modelName?: string;
  onRegenerate: (event: React.MouseEvent<Element, MouseEvent>) => any;
}

export const RegenerateModel = (props: Props) => {
  const { t } = useTranslation();
  const { modelName, hidden, onRegenerate } = props;

  const Render = () => {
    return (
      <>
        <Separator className="my-1" />

        <div
          className="flex my-1 ml-[.8px] py-2 gap-[6px] items-center text-sm rounded-md hover:bg-accent"
          onClick={onRegenerate}
        >
          <div>
            <IconRefresh
              size={20}
              stroke="hsl(var(--muted-foreground))"
              className="text-muted-foreground ml-[6px] "
            />
          </div>

          <div className="text-nowrap overflow-hidden text-ellipsis whitespace-nowrap pr-2">
            {t('Regenerate')}({modelName})
          </div>
        </div>
      </>
    );
  };

  return <>{!hidden && Render()}</>;
};

export default RegenerateModel;
