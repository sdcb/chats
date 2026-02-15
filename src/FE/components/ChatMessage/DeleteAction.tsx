import { useEffect, useRef, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconLoader, IconTrash } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';

import { cn } from '@/lib/utils';

interface Props {
  disabled?: boolean;
  isHoverVisible?: boolean;
  hidden?: boolean;
  onDelete: () => Promise<void>;
}

const DeleteAction = (props: Props) => {
  const { onDelete, disabled, isHoverVisible, hidden } = props;
  const { t } = useTranslation();
  const [isDeleting, setIsDeleting] = useState(false);
  const isUnmountedRef = useRef(false);

  useEffect(() => {
    return () => {
      isUnmountedRef.current = true;
    };
  }, []);

  const Render = () => {
    return (
      <Tips
        className="h-[28px]"
        trigger={
          <Button
            variant="ghost"
            disabled={disabled || isDeleting}
            className={cn(
              isHoverVisible ? 'invisible' : 'visible',
              'p-1 m-0 h-auto group-hover:visible focus:visible',
            )}
            onClick={async (e) => {
              e.stopPropagation();
              if (disabled || isDeleting) return;

              setIsDeleting(true);
              try {
                await onDelete();
              } catch (error) {
                console.error('Delete operation failed:', error);
              } finally {
                if (!isUnmountedRef.current) setIsDeleting(false);
              }
            }}
          >
            {isDeleting ? <IconLoader className="animate-spin" /> : <IconTrash />}
          </Button>
        }
        content={t('Delete message')!}
      />
    );
  };

  return <>{!hidden && Render()}</>;
};

export default DeleteAction;
