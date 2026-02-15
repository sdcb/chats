import * as React from 'react';
import { Checkbox } from './checkbox';
import { cn } from '@/lib/utils';

export type TriStateCheckboxState = 'checked' | 'unchecked' | 'indeterminate';

interface TriStateCheckboxProps {
  state: TriStateCheckboxState;
  onClick?: (e: React.MouseEvent) => void;
  disabled?: boolean;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

/**
 * 三态复选框组件（基于 shadcn/ui Checkbox）
 * - unchecked: 空方框边框
 * - checked: 边框 + 勾选
 * - indeterminate: 边框 + 横杠（部分选中）
 */
const TriStateCheckbox = React.forwardRef<HTMLButtonElement, TriStateCheckboxProps>(
  ({ state, onClick, disabled = false, size = 'md', className }, ref) => {
    const sizeClasses = {
      sm: 'h-3.5 w-3.5',
      md: 'h-4 w-4',
      lg: 'h-5 w-5',
    };

    // 将 state 转换为 Checkbox 的 checked 属性
    const checkedValue = state === 'checked' ? true : state === 'indeterminate' ? 'indeterminate' : false;

    return (
      <Checkbox
        ref={ref}
        checked={checkedValue}
        disabled={disabled}
        onClick={onClick}
        className={cn(sizeClasses[size], className)}
      />
    );
  }
);

TriStateCheckbox.displayName = 'TriStateCheckbox';

export { TriStateCheckbox };
