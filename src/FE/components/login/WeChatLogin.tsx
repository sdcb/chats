import { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { Button } from '@/components/ui/button';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

import WeChatLoginModal from './WeChatLoginModal';

const WeChatLogin = (props: {
  configs?: { appId: string };
  loading?: boolean;
}) => {
  const { t } = useTranslation();
  const { loading, configs } = props;
  const [weChatModal, setWeChatModal] = useState(false);

  return (
    <>
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              onClick={() => {
                setWeChatModal(true);
              }}
              className="p-0 w-8 h-8"
              disabled={loading}
              variant="link"
            >
              <img
                src="/logos/wechat.svg"
                alt="WeChat"
                width={0}
                height={0}
                className="h-8 w-8 dark:bg-transparent"
              />
            </Button>
          </TooltipTrigger>
          <TooltipContent>{t('WeChat Login')}</TooltipContent>
        </Tooltip>
      </TooltipProvider>
      {weChatModal && (
        <WeChatLoginModal
          appId={configs!.appId}
          isOpen={weChatModal}
          onClose={() => {
            setWeChatModal(false);
          }}
        />
      )}
    </>
  );
};
export default WeChatLogin;
