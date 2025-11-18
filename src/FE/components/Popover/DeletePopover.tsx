import { useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconTrash } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
// (暂时移除复杂 Tooltip 组合，避免与 Popover ref 冲突)
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';

interface Props {
  onDelete: (() => void) | (() => Promise<void>);
  onCancel?: () => void;
  tooltip?: string; // 提示文本，可选
  className?: string; // 按钮自定义样式
  iconSize?: number; // 图标尺寸，默认 18
}

export default function DeletePopover(props: Props) {
  const { t } = useTranslation();
  const { onDelete, onCancel, tooltip, className, iconSize = 18 } = props;
  const [isOpen, setIsOpen] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  const handleCancel = () => {
    if (isDeleting) return; // 防止在删除过程中取消
    setIsOpen(false);
    onCancel && onCancel();
  };

  const handleDelete = async () => {
    if (isDeleting) return; // 防止重复点击
    
    setIsDeleting(true);
    try {
      const result = onDelete && onDelete();
      
      // 检查是否返回 Promise
      if (result instanceof Promise) {
        await result;
      }
    } catch (error) {
      console.error('Delete operation failed:', error);
      // 删除失败时不关闭弹窗，让用户看到错误状态
    } finally {
      setIsOpen(false);
      setIsDeleting(false);
    }
  };

  const triggerButton = (
    <Button
      variant="ghost"
      size="icon"
      className={cn("text-destructive", className || "h-9 w-9")}
      disabled={isDeleting}
      onClick={() => !isDeleting && setIsOpen(true)}
      title={tooltip}
      aria-label={tooltip || t('Delete') || 'Delete'}
    >
      <IconTrash size={iconSize} />
    </Button>
  );

  return (
    <Popover open={isOpen} onOpenChange={(open) => !isDeleting && setIsOpen(open)}>
      <PopoverTrigger asChild>
        {triggerButton}
      </PopoverTrigger>
      <PopoverContent side="bottom" align="center" className="w-56 pointer-events-auto">
        <div className="pb-2 text-sm leading-relaxed">
          {t('Are you sure you want to delete it?')}
        </div>
        <div className="flex justify-end gap-2">
          <Button
            size="sm"
            onClick={handleCancel}
            variant="outline"
            disabled={isDeleting}
          >
            {t('Cancel')}
          </Button>
          <Button
            size="sm"
            onClick={handleDelete}
            disabled={isDeleting}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          >
            {isDeleting ? (
              <div className="flex items-center space-x-2">
                <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                <span>{t('Deleting...')}</span>
              </div>
            ) : (
              t('Confirm')
            )}
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}
