import React from 'react';
import { Switch } from './switch';
import { cn } from '@/lib/utils';

interface LabelSwitchProps {
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
  label: string;
  disabled?: boolean;
  className?: string;
  labelClassName?: string;
  switchClassName?: string;
}

export function LabelSwitch({
  checked,
  onCheckedChange,
  label,
  disabled = false,
  className,
  labelClassName,
  switchClassName,
}: LabelSwitchProps) {
  const handleLabelClick = () => {
    if (!disabled) {
      onCheckedChange(!checked);
    }
  };

  return (
    <div
      className={cn(
        'flex items-center gap-2 select-none',
        disabled && 'opacity-50',
        className
      )}
    >
      <Switch
        checked={checked}
        onCheckedChange={onCheckedChange}
        disabled={disabled}
        className={switchClassName}
      />
      <span
        className={cn(
          'text-sm cursor-pointer',
          disabled && 'text-muted-foreground cursor-not-allowed',
          labelClassName
        )}
        onClick={handleLabelClick}
      >
        {label}
      </span>
    </div>
  );
}