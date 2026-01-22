import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { ChatResult } from '@/types/clientApis';

import CopyButton from '@/components/Button/CopyButton';
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
  const [loading, setLoading] = useState(true);
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
    getUserChatShare(chat.id).then((data) => {
      if (data.length > 0) setShareUrl(baseUrl + data[0].shareId);
      setLoading(false);
    });
  }, [baseUrl, chat.id]);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className={shareUrl ? "max-w-[600px]" : "max-w-[320px]"}>
        <DialogHeader>
          <DialogTitle>{t('Share Message')}</DialogTitle>
        </DialogHeader>

        {shareUrl ? (
          <>
            <div className="flex items-center gap-2 p-3 bg-gray-50 dark:bg-gray-800 rounded-md border border-gray-200 dark:border-gray-700">
              <CopyButton value={shareUrl} />
              <div className="flex-1 text-sm text-gray-800 dark:text-gray-200 break-all">
                {shareUrl}
              </div>
            </div>
            <div className="flex justify-start gap-2 mt-2">
              <Button
                variant="destructive"
                onClick={() => {
                  handleCloseShared();
                }}
                disabled={loading}
              >
                {t('Close Shared')}
              </Button>
            </div>
          </>
        ) : (
          <div className="flex justify-end">
            <Button
              disabled={loading}
              onClick={() => {
                handleSharedMessage();
              }}
            >
              {t('Share and Copy Link')}
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
};
export default SharedMessageModal;
