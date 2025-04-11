import useTranslation from '@/hooks/useTranslation';

import { IconEditCheck } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';

import { cn } from '@/lib/utils';

interface Props {
  isHoverVisible?: boolean;
  hovered?: boolean;
}

const EditStatusAction = (props: Props) => {
  const { isHoverVisible, hovered } = props;
  const { t } = useTranslation();

  return (
    <Tips
      className="h-[28px]"
      trigger={
        <Button
          variant="ghost"
          className={cn(
            isHoverVisible ? 'invisible' : 'visible',
            hovered && 'bg-muted',
            'p-1 m-0 h-auto group-hover:visible focus:visible',
          )}
          onClick={(e) => {
            e.stopPropagation();
          }}
        >
          <IconEditCheck />
        </Button>
      }
      content={t('Message edited')!}
    />
  );
};

export default EditStatusAction;
