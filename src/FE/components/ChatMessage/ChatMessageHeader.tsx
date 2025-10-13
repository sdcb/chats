import useTranslation from '@/hooks/useTranslation';

import { formatRelativeTime } from '@/utils/date';

import { IChatMessage, MessageDisplayType } from '@/types/chatMessage';

import ModelProviderIcon from '../common/ModelProviderIcon';
import { Button } from '../ui/button';

import { cn } from '@/lib/utils';

const ChatMessageHeader = ({
  onChangeDisplayType,
  message,
  readonly,
}: {
  onChangeDisplayType?: (messageId: string, type: MessageDisplayType) => void;
  message: IChatMessage;
  readonly?: boolean;
}) => {
  const { t } = useTranslation();

  return (
    <div className="flex justify-between items-center h-8 mb-1">
      <div className="flex gap-1 items-center">
  <ModelProviderIcon
          providerId={message.modelProviderId}
          className="w-4 h-4 hidden sm:block"
        />
        {message.modelName}
        <span className="text-xs text-muted-foreground hidden sm:block invisible group-hover/item:visible">
          {message.createdAt && formatRelativeTime(message.createdAt)}
        </span>
      </div>
      <div
        className={cn(
          'flex gap-1 invisible group-hover/item:visible',
          readonly && 'hidden',
        )}
      >
        <Button
          variant="ghost"
          size="sm"
          className={cn(
            'p-2 h-6',
            (message.displayType === 'Preview' || message.displayType === undefined) && 'bg-muted text-foreground',
          )}
          onClick={() => onChangeDisplayType && onChangeDisplayType(message.id, 'Preview')}
        >
          {t('preview')}
        </Button>
        <Button
          variant="ghost"
          size="sm"
          className={cn(
            'p-2 h-6',
            message.displayType === 'Raw' && 'bg-muted text-foreground',
          )}
          onClick={() => onChangeDisplayType && onChangeDisplayType(message.id, 'Raw')}
        >
          {t('raw')}
        </Button>
      </div>
    </div>
  );
};

export default ChatMessageHeader;
