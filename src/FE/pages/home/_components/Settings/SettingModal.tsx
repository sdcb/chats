import React, { useEffect } from 'react';

import useTranslation from '@/hooks/useTranslation';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

import ApiKeyTab from './ApiKeyTabContent';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

const SettingModal = (props: Props) => {
  const { isOpen, onClose } = props;
  const { t } = useTranslation();

  useEffect(() => {}, []);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="w-full max-w-5xl gap-0 overflow-scroll max-h-full">
        <DialogHeader className="mb-[16px]">
          <DialogTitle>{t('API Key Management')}</DialogTitle>
        </DialogHeader>
        <ApiKeyTab />
      </DialogContent>
    </Dialog>
  );
};
export default SettingModal;
