import { useEffect } from 'react';

import useExternal from '@/hooks/useExternal';

import Spinner from '@/components/Spinner/Spinner';
import { Dialog, DialogContent } from '@/components/ui/dialog';

const WeChatLoginModal = (props: {
  isOpen: boolean;
  appId: string;
  onClose: () => void;
}) => {
  const { isOpen, appId, onClose } = props;
  const status = useExternal(
    'https://res.wx.qq.com/connect/zh_CN/htmledition/js/wxLogin.js',
    {
      js: {
        async: true,
      },
    },
  );
  useEffect(() => {
    if (isOpen && status === 'ready') {
      const { origin } = location;
      new WxLogin({
        id: 'wxContainer',
        appid: appId,
        scope: 'snsapi_login',
        response_type: 'code',
        state: 'STATE',
        redirect_uri: encodeURIComponent(`${origin}/authorizing`),
        href: 'data:text/css;base64,LmltcG93ZXJCb3ggLnFyY29kZSB7d2lkdGg6IDIwMHB4O2hlaWdodDogMjAwcHg7fQ0KLmltcG93ZXJCb3ggLmluZm8ge2Rpc3BsYXk6IG5vbmU7fQ==',
      });
    }
  }, [status]);
  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="w-[340px] h-[320px]">
        {status !== 'ready' && (
          <div className="flex justify-center items-center h-[300px]">
            <Spinner className="mx-auto text-gray-500 dark:text-gray-50" />
          </div>
        )}
        <div
          id="wxContainer"
          className="flex justify-center items-center"
        ></div>
      </DialogContent>
    </Dialog>
  );
};

export default WeChatLoginModal;
