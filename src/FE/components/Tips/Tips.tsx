import { ReactElement, useState } from 'react';

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
  delayDuration,
  side = 'top',
}: {
  className?: string;
  trigger: ReactElement | string;
  content?: ReactElement | string;
  delayDuration?: number;
  side?: 'top' | 'right' | 'bottom' | 'left';
}) => {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <TooltipProvider delayDuration={delayDuration}>
      <Tooltip open={isOpen}>
        <TooltipTrigger
          asChild={true}
          className={className}
          onMouseEnter={() => setIsOpen(true)}
          onMouseLeave={() => setIsOpen(false)}
          onClick={() => setIsOpen(true)}
        >
          {trigger}
        </TooltipTrigger>
        {content && <TooltipContent side={side}>{content}</TooltipContent>}
      </Tooltip>
    </TooltipProvider>
  );
};

export default Tips;
