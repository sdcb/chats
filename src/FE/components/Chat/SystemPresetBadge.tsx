import { ShieldCheck } from 'lucide-react';

import useTranslation from '@/hooks/useTranslation';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

interface SystemPresetBadgeProps {
  isSystem: boolean;
  className?: string;
}

const SystemPresetBadge = ({ isSystem, className }: SystemPresetBadgeProps) => {
  const { t } = useTranslation();

  if (!isSystem) return null;

  return (
    <TooltipProvider delayDuration={100}>
      <Tooltip>
        <TooltipTrigger asChild>
          <span
            className={className}
            onPointerDown={(event) => event.stopPropagation()}
            onClick={(event) => event.stopPropagation()}
          >
            <ShieldCheck size={16} className="text-primary shrink-0" />
          </span>
        </TooltipTrigger>
        <TooltipContent>{t('System preset')}</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
};

export default SystemPresetBadge;
