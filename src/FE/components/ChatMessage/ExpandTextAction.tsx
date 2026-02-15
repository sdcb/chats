import useTranslation from '@/hooks/useTranslation';

import { IconArrowDown, IconArrowUp } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';

import { cn } from '@/lib/utils';

interface Props {
  expanded: boolean;
  hidden?: boolean;
  disabled?: boolean;
  isHoverVisible?: boolean;
  onToggle: () => void;
}

const ExpandTextAction = (props: Props) => {
  const { expanded, hidden, disabled, isHoverVisible, onToggle } = props;
  const { t } = useTranslation();

  if (hidden) return null;

  const tooltip = expanded ? t('Collapse text') : t('Expand text');

  return (
    <Tips
      className="h-[28px]"
      trigger={
        <Button
          variant="ghost"
          disabled={disabled}
          className={cn(
            isHoverVisible ? 'invisible' : 'visible',
            'p-1 m-0 h-auto group-hover:visible focus:visible',
          )}
          onClick={(e) => {
            onToggle();
            e.stopPropagation();
          }}
        >
          {expanded ? <IconArrowUp /> : <IconArrowDown />}
        </Button>
      }
      content={tooltip!}
    />
  );
};

export default ExpandTextAction;

