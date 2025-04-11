import { useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconCheck, IconCopy } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';

import { cn } from '@/lib/utils';

interface Props {
  triggerClassName?: string;
  text?: string;
  content?: string;
  hidden?: boolean;
}
const CopyAction = (props: Props) => {
  const { text, triggerClassName, content, hidden = false } = props;
  const { t } = useTranslation();
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
          <Button variant="ghost" className="p-1 m-0 h-auto w-auto">
            <IconCheck className="text-green-500 dark:text-green-400" />
          </Button>
        ) : (
          <Tips
            trigger={
              <Button
                variant="ghost"
                className={cn('p-1 m-0 h-7 w-7', triggerClassName)}
                onClick={(e) => {
                  copyOnClick(text);
                  e.stopPropagation();
                }}
              >
                <IconCopy />
                {content}
              </Button>
            }
            side="bottom"
            content={t('Copy')!}
          />
        )}
      </>
    );
  };

  return <>{!hidden && Render()}</>;
};

export default CopyAction;
