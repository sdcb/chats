import { useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconTrash } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';

interface Props {
  onDelete: (() => void) | (() => Promise<void>);
  onCancel?: () => void;
}

export default function DeletePopover(props: Props) {
  const { t } = useTranslation();
  const { onDelete, onCancel } = props;
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
      
      setIsOpen(false);
    } catch (error) {
      console.error('Delete operation failed:', error);
      // 删除失败时不关闭弹窗，让用户看到错误状态
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <Popover open={isOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="link"
          onClick={() => {
            setIsOpen(true);
          }}
          disabled={isDeleting}
        >
          <IconTrash size={18}/>
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-48 pointer-events-auto">
        <div className="pb-2">{t('Are you sure you want to delete it?')}</div>
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
