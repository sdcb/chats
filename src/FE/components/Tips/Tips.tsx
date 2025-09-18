import { ReactElement } from 'react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

const Tips = ({
  trigger,
  content,
  className,
  delayDuration = 0,
  side = 'top',
}: {
  className?: string;
  trigger: ReactElement | string;
  content?: ReactElement | string;
  delayDuration?: number;
  side?: 'top' | 'right' | 'bottom' | 'left';
}) => {
  return (
    <TooltipProvider delayDuration={delayDuration}>
      <Tooltip>
        <TooltipTrigger
          asChild={true}
          className={className}
        >
          {trigger}
        </TooltipTrigger>
        {content && <TooltipContent side={side}>{content}</TooltipContent>}
      </Tooltip>
    </TooltipProvider>
  );
};

export default Tips;
