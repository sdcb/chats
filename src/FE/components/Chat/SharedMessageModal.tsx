import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { ChatResult } from '@/types/clientApis';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

import {
  deleteUserChatShare,
  getUserChatShare,
  postUserChatShare,
} from '@/apis/clientApis';

interface IProps {
  chat: ChatResult;
  isOpen: boolean;
  onClose: () => void;
  onShareChange: (isShared: boolean) => void;
}

const SharedMessageModal = (props: IProps) => {
  const { t } = useTranslation();
  const { chat, isOpen, onClose, onShareChange } = props;
  const [loading, setLoading] = useState(false);
  const [shareUrl, setShareUrl] = useState<string>('');
  const baseUrl = `${location.origin}/share/`;

  const handleSharedMessage = () => {
    const date = new Date().addYear(2).toISOString();
    setLoading(true);
    postUserChatShare(chat.id, date)
      .then((data) => {
        onShareChange(true);
        const url = baseUrl + data.shareId;
        setShareUrl(baseUrl + data.shareId);
        handleCopySharedUrl(url);
      })
      .finally(() => {
        setLoading(false);
      });
  };

  const handleCloseShared = () => {
    setLoading(true);
    deleteUserChatShare(chat.id)
      .then(() => {
        onShareChange(false);
        setShareUrl('');
        toast.success(t('Save successful'));
      })
      .finally(() => {
        setLoading(false);
      });
  };

  const handleCopySharedUrl = (url: string) => {
    if (!navigator.clipboard) return;
    navigator.clipboard.writeText(url).then(() => {
      toast.success(t('Copy Successful'));
    });
  };

  useEffect(() => {
    setLoading(true);
    getUserChatShare(chat.id).then((data) => {
      if (data.length > 0) setShareUrl(baseUrl + data[0].shareId);
      setLoading(false);
    });
  }, []);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-[320px]">
        <DialogHeader>
          <DialogTitle>{t('Share Message')}</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-2">
          {shareUrl ? (
            <>
              <Button
                variant="link"
                onClick={() => {
                  handleCloseShared();
                }}
                disabled={loading}
              >
                {t('Close Shared')}
              </Button>
              <Button
                onClick={() => {
                  handleCopySharedUrl(shareUrl);
                }}
                disabled={loading}
              >
                {t('Copy Link')}
              </Button>
            </>
          ) : (
            <Button
              disabled={loading}
              onClick={() => {
                handleSharedMessage();
              }}
            >
              {t('Share and Copy Link')}
            </Button>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
};
export default SharedMessageModal;
