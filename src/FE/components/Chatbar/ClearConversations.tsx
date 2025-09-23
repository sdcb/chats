import { FC, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconCheck, IconTrash, IconX } from '@/components/Icons/index';

import SidebarButton from '../Sidebar/SidebarButton';

interface Props {
  onClearConversations: () => void;
}

const ClearConversations: FC<Props> = ({ onClearConversations }) => {
  const [isConfirming, setIsConfirming] = useState<boolean>(false);

  const { t } = useTranslation();

  const handleClearConversations = () => {
    onClearConversations();
    setIsConfirming(false);
  };

  return isConfirming ? (
    <div className="flex w-full cursor-pointer items-center rounded-lg py-3 px-3 hover:bg-gray-500/10">
      <IconTrash size={18} />

      <div className="ml-3 flex-1 text-left leading-3 text-black dark:text-white">
        {t('Are you sure?')}
      </div>

      <div className="flex w-[40px]">
        <IconCheck
          className="ml-auto mr-1 min-w-[20px] text-black dark:text-white hover:opacity-50"
          size={18}
          onClick={(e) => {
            e.stopPropagation();
            handleClearConversations();
          }}
        />

        <IconX
          className="ml-auto min-w-[20px] text-black dark:text-white hover:opacity-50"
          size={18}
          onClick={(e) => {
            e.stopPropagation();
            setIsConfirming(false);
          }}
        />
      </div>
    </div>
  ) : (
    <SidebarButton
      text={t('Clear conversations')}
      icon={<IconTrash />}
      onClick={() => setIsConfirming(true)}
    />
  );
};

export default ClearConversations;
