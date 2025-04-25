import useTranslation from '@/hooks/useTranslation';

import { formatRelativeTime } from '@/utils/date';

import { IChatMessage, MessageDisplayType } from '@/types/chatMessage';

import ChatIcon from '../ChatIcon/ChatIcon';
import { Button } from '../ui/button';

import { cn } from '@/lib/utils';

const ChatMessageHeader = ({
  onChangeDisplayType,
  message,
}: {
  onChangeDisplayType?: (messageId: string) => void;
  message: IChatMessage;
}) => {
  const { t } = useTranslation();

  return (
    <div className="flex justify-between items-center h-8">
      <div className="flex gap-1 items-center">
        <ChatIcon
          providerId={message.modelProviderId}
          className="w-4 h-4 hidden sm:block"
        />
        {message.modelName}
        <span className="text-xs text-muted-foreground hidden sm:block invisible group-hover/item:visible">
          {message.createdAt && formatRelativeTime(message.createdAt)}
        </span>
      </div>
      <div className="flex gap-1 invisible group-hover/item:visible">
        <Button
          variant="ghost"
          size="sm"
          className={cn(
            'p-2 h-6',
            (message.displayType === MessageDisplayType.Preview ||
              message.displayType === undefined) &&
              'bg-muted text-foreground',
          )}
          onClick={() => onChangeDisplayType && onChangeDisplayType(message.id)}
        >
          {t('preview')}
        </Button>
        <Button
          variant="ghost"
          size="sm"
          className={cn(
            'p-2 h-6',
            message.displayType === MessageDisplayType.Code &&
              'bg-muted text-foreground',
          )}
          onClick={() => onChangeDisplayType && onChangeDisplayType(message.id)}
        >
          {t('code')}
        </Button>
      </div>
    </div>
  );
};

export default ChatMessageHeader;
