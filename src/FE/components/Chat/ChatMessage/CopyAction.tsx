import { useState } from 'react';

import { useTranslation } from 'next-i18next';

import { IconCheck, IconCopy } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';

import { cn } from '@/lib/utils';

interface Props {
  triggerClassName?: string;
  text?: string;
  hidden?: boolean;
}
const CopyAction = (props: Props) => {
  const { text, triggerClassName, hidden = false } = props;
  const { t } = useTranslation('chat');
  const [messagedCopied, setMessageCopied] = useState(false);

  const copyOnClick = (content?: string) => {
    if (!navigator.clipboard) return;

    navigator.clipboard.writeText(content || '').then(() => {
      setMessageCopied(true);
      setTimeout(() => {
        setMessageCopied(false);
      }, 2000);
    });
  };

  const Render = () => {
    return (
      <>
        {messagedCopied ? (
          <Button variant="ghost" className="p-1 m-0 h-auto">
            <IconCheck
              stroke="#7d7d7d"
              className="text-green-500 dark:text-green-400"
            />
          </Button>
        ) : (
          <Tips
            className="h-[28px]"
            trigger={
              <Button
                variant="ghost"
                className={cn('p-1 m-0 h-auto', triggerClassName)}
                onClick={() => copyOnClick(text)}
              >
                <IconCopy stroke="#7d7d7d" />
              </Button>
            }
            content={t('Copy')!}
          />
        )}
      </>
    );
  };

  return <>{!hidden && Render()}</>;
};

export default CopyAction;