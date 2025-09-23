import useTranslation from '@/hooks/useTranslation';

import {
  IconArchive,
  IconDots,
  IconFolderPlus,
  IconTrash,
} from '@/components/Icons';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

const ChatActions = ({
  onAddGroup,
  onBatchArchive,
  onBatchDelete,
}: {
  onAddGroup: () => void;
  onBatchArchive: () => void;
  onBatchDelete: () => void;
}) => {
  const { t } = useTranslation();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="focus:outline-none p-[6px]">
        <IconDots className="w-6" size={16} />
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-42 border-none">
        <DropdownMenuItem
          className="flex justify-start gap-3"
          onClick={onAddGroup}
        >
          <IconFolderPlus size={18} />
          {t('New Group')}
        </DropdownMenuItem>
        {/* <DropdownMenuItem
          className="flex justify-start gap-3"
          onClick={onBatchArchive}
        >
          <IconArchive size={18} />
          {t('Batch Archive')}
        </DropdownMenuItem> */}
        <DropdownMenuItem
          className="flex justify-start gap-3"
          onClick={onBatchDelete}
        >
          <IconTrash size={18} />
          {t('Batch Delete')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
};

export default ChatActions;
