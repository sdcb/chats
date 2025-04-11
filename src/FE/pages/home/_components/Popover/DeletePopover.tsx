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
  onDelete: () => void;
  onCancel?: () => void;
}
export default function DeletePopover(props: Props) {
  const { t } = useTranslation();
  const { onDelete, onCancel } = props;
  const [isOpen, setIsOpen] = useState(false);

  const handleCancel = () => {
    setIsOpen(false);
    onCancel && onCancel();
  };

  const handleDelete = () => {
    setIsOpen(false);
    onDelete && onDelete();
  };

  return (
    <Popover open={isOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="link"
          onClick={() => {
            setIsOpen(true);
          }}
        >
          <IconTrash size={18}/>
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-48 pointer-events-auto">
        <div className="pb-2">{t('Are you sure you want to delete it?')}</div>
        <div className="flex justify-end gap-2">
          <Button size="sm" onClick={handleCancel} variant="outline">
            {t('Cancel')}
          </Button>
          <Button size="sm" onClick={handleDelete}>
            {t('Confirm')}
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}
