import { useContext } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconMessageChatbot } from '@/components/Icons';
import { Button } from '@/components/ui/button';

import HomeContext from '../../_contexts/home.context';

const NoChat = () => {
  const { t } = useTranslation();

  const { handleNewChat } = useContext(HomeContext);

  return (
    <div className="w-full flex items-center flex-wrap justify-center gap-10">
      <div className="grid gap-2 w-60">
        <div className="w-20 h-20 mx-auto">
          <IconMessageChatbot size={64} />
        </div>
        <div>
          <h2 className="text-center text-lg font-semibold leading-relaxed">
            {t("You don't have any chat yet")}
          </h2>
          <Button
            variant="link"
            onClick={() => {
              handleNewChat();
            }}
            className="text-center text-sm font-normal leading-snug w-full"
          >
            {t('Click here to create your first chat')}
          </Button>
        </div>
      </div>
    </div>
  );
};

export default NoChat;
