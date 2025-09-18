import React from 'react';
import { cn } from '@/lib/utils';

interface CollapsiblePanelProps {
  open: boolean;
  children: React.ReactNode;
  className?: string;
}

export default function CollapsiblePanel({
  open,
  children,
  className,
}: CollapsiblePanelProps) {
  return (
    <div
      className={cn(
        'grid transition-[grid-template-rows] duration-300 ease-in-out',
        open ? 'grid-rows-[1fr]' : 'grid-rows-[0fr]',
        className
      )}
    >
      <div className="min-h-0 overflow-hidden">{children}</div>
    </div>
  );
}