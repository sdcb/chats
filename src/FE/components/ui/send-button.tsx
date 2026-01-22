import { KeyboardEvent, useEffect } from 'react';
import useTranslation from '@/hooks/useTranslation';
import { useSendMode, SendMode } from '@/hooks/useSendMode';
import { useIsMobile } from '@/hooks/useMobile';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from '@/components/ui/dropdown-menu';
import { IconChevronDown } from '@/components/Icons';
import { cn } from '@/lib/utils';

interface SendButtonProps {
  onSend: () => void;
  disabled?: boolean;
  isSending?: boolean;
  className?: string;
  size?: 'default' | 'sm' | 'lg';
}

export const SendButton = ({
  onSend,
  disabled = false,
  isSending = false,
  className,
  size = 'default',
}: SendButtonProps) => {
  const { t } = useTranslation();
  const { sendMode, updateSendMode } = useSendMode();
  const isMobile = useIsMobile();

  // 处理 Alt+S 快捷键
  useEffect(() => {
    const handleKeyDown = (e: globalThis.KeyboardEvent) => {
      if (e.altKey && e.key.toLowerCase() === 's' && !disabled) {
        e.preventDefault();
        onSend();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onSend, disabled]);

  const sendText = t('Send');

  // 在移动端显示普通按钮
  if (isMobile) {
    return (
      <Button
        className={cn('h-auto py-1.5 bg-primary/90 hover:bg-primary/80 active:bg-primary/75', className)}
        onClick={onSend}
        disabled={disabled || isSending}
        size={size}
      >
        {isSending ? t('Sending...') : sendText}
      </Button>
    );
  }

  // 在桌面端显示组合按钮
  return (
    <div className="flex">
      <Button
        className={cn(
          'rounded-r-none border-r border-r-black/20 flex-1',
          className
        )}
        onClick={onSend}
        disabled={disabled || isSending}
        size={size}
      >
        {isSending ? t('Sending...') : sendText}
      </Button>
      
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            className="rounded-l-none px-2 border-l-0"
            variant="default"
            size={size}
            disabled={isSending}
          >
            <IconChevronDown size={14} className="text-white dark:text-black" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-56">
          <DropdownMenuItem
            onClick={() => updateSendMode('enter')}
            className="flex items-center gap-2"
          >
            <div className="w-4 h-4 flex items-center justify-center text-sm">⏎</div>
            <div className="flex-1">
              <div className="font-medium">{t('Press Enter to send')}</div>
              <div className="text-xs text-muted-foreground">
                {t('Shift+Enter for line break')}
              </div>
            </div>
            {sendMode === 'enter' && (
              <div className="w-2 h-2 bg-primary rounded-full" />
            )}
          </DropdownMenuItem>
          <DropdownMenuItem
            onClick={() => updateSendMode('ctrl-enter')}
            className="flex items-center gap-2"
          >
            <div className="w-4 h-4 flex items-center justify-center text-xs">^⏎</div>
            <div className="flex-1">
              <div className="font-medium">{t('Press Ctrl+Enter to send')}</div>
              <div className="text-xs text-muted-foreground">
                {t('Enter for line break')}
              </div>
            </div>
            {sendMode === 'ctrl-enter' && (
              <div className="w-2 h-2 bg-primary rounded-full" />
            )}
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem disabled className="text-xs text-muted-foreground">
            {t('Alt+S to send anytime')}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
};

// 用于处理文本框键盘事件的 hook
export const useSendKeyHandler = (
  onSend: () => void,
  isTyping: boolean = false,
  disabled: boolean = false
) => {
  const { sendMode } = useSendMode();

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (disabled || isTyping) return;

    // Alt+S 发送
    if (e.altKey && e.key.toLowerCase() === 's') {
      e.preventDefault();
      onSend();
      return;
    }

    // 根据模式处理 Enter 键
    if (e.key === 'Enter') {
      if (sendMode === 'enter' && !e.shiftKey && !e.ctrlKey) {
        e.preventDefault();
        onSend();
      } else if (sendMode === 'ctrl-enter' && e.ctrlKey && !e.shiftKey) {
        e.preventDefault();
        onSend();
      }
    }
  };

  return { handleKeyDown, sendMode };
};