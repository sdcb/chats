import useTranslation from '@/hooks/useTranslation';

import { IconCheck, IconX } from '@/components/Icons';
import { Button } from '@/components/ui/button';

const ChatActionConfirm = ({
  confirming = false,
  selectedCount = 0,
  onCancel,
  onConfirm,
}: {
  confirming: boolean;
  selectedCount: number;
  onCancel: () => void;
  onConfirm: () => void;
}) => {
  const { t } = useTranslation();

  return (
    <div className="flex justify-between items-center h-8 px-2">
      <div className="text-gray-500">
        <span className="text-foreground"> {selectedCount} </span>
        {t('Selected')}
      </div>
      <div>
        <Button
          disabled={confirming}
          variant="ghost"
          className="gap-x-2 h-8 px-2"
          onClick={onCancel}
        >
          <IconX size={16} />
        </Button>
        <Button
          disabled={confirming}
          variant="ghost"
          className="gap-x-2 h-8 px-2"
          onClick={onConfirm}
        >
          <IconCheck size={16} />
        </Button>
      </div>
    </div>
  );
};

export default ChatActionConfirm;
