import React from 'react';
import { Button } from '@/components/ui/button';
import Tips from '@/components/Tips/Tips';
import { cn } from '@/lib/utils';

/**
 * 通用图标动作按钮，集成：
 * - ghost + size=icon 样式 (统一 h-9 w-9)
 * - Tooltip (Tips) 基于 title/label
 * - aria-label 自动回退
 * - 可选 stopPropagation
 */
export interface IconActionButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  label: string;                 // Tooltip & aria-label 文本
  icon: React.ReactNode;         // 渲染的图标
  disabledTooltip?: string;      // 禁用状态下 tooltip 替换文本
  stopPropagation?: boolean;     // 是否阻止冒泡（列表行内按钮常用）
  noTooltip?: boolean;           // 允许在特殊情况下关闭 tooltip
}

const IconActionButton: React.FC<IconActionButtonProps> = ({
  label,
  icon,
  className,
  disabled,
  disabledTooltip,
  stopPropagation = true,
  noTooltip = false,
  onClick,
  ...rest
}) => {
  const content = (
    <Button
      variant="ghost"
      size="icon"
      aria-label={label}
      title={label}
      disabled={disabled}
      className={cn('h-9 w-9', className)}
      onClick={(e) => {
        if (stopPropagation) e.stopPropagation();
        onClick?.(e);
      }}
      {...rest}
    >
      {icon}
    </Button>
  );

  if (noTooltip) return content;

  const tipText = disabled && disabledTooltip ? disabledTooltip : label;
  return (
    <Tips trigger={content} content={tipText} />
  );
};

export default IconActionButton;
