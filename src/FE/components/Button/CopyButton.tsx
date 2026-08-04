import { useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { IconCheck, IconClipboard } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { copyTextToClipboard } from '@/utils/clipboard';

interface Props {
  value: string;
  onCopy?: () => void;
}

export default function CopyButton(props: Props) {
  const { t } = useTranslation();
  const { value, onCopy } = props;
  const [isCopied, setIsCopied] = useState<Boolean>(false);

  const handleCopy = async () => {
    if (!(await copyTextToClipboard(value))) return;

    setIsCopied(true);
    onCopy && onCopy();
    setTimeout(() => {
      setIsCopied(false);
    }, 1000);
    toast.success(t('Copy Successful'));
  };

  return (
    <Button
      variant="ghost"
      className="items-center rounded bg-none p-1 h-auto w-auto"
      onClick={handleCopy}
      title={t('Copy')}
    >
      {isCopied ? <IconCheck size={18} /> : <IconClipboard size={18} />}
    </Button>
  );
}
