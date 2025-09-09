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
  const handleToggle = () => {
    if (!disabled) {
      onCheckedChange(!checked);
    }
  };

  return (
    <div
      className={cn(
        'flex items-center gap-2 cursor-pointer select-none',
        disabled && 'cursor-not-allowed opacity-50',
        className
      )}
      onClick={handleToggle}
    >
      <Switch
        checked={checked}
        onCheckedChange={onCheckedChange}
        disabled={disabled}
        className={switchClassName}
      />
      <span
        className={cn(
          'text-sm',
          disabled && 'text-muted-foreground',
          labelClassName
        )}
      >
        {label}
      </span>
    </div>
  );
}