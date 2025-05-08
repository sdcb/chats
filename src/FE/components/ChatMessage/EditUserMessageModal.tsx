import useTransition from '@/hooks/useTranslation';

import { MessageContentType, ResponseContent } from '@/types/chat';

import { Button } from '../ui/button';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import ImageEditor from '../ImageEditor';

interface Props {
  content: ResponseContent[];
  isOpen: boolean;
  onClose: () => void;
}

const EditUserMessageModal = (props: Props) => {
  const { content, isOpen, onClose } = props;
  const { t } = useTransition();
  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="w-[800px] h-[700px]">
        <DialogHeader>
          <DialogTitle></DialogTitle>
        </DialogHeader>

        {
          <ImageEditor
            imageUrl={
              (
                content.filter((x) => x.$type === MessageContentType.fileId)[0]
                  .c as any
              ).url
            }
          ></ImageEditor>
        }

        <DialogFooter className="pt-4">
          <Button
            variant="link"
            className="rounded-md px-4 py-1 text-sm font-medium"
            onClick={() => {
              // handleEditMessage(true);
            }}
          >
            {t('Save')}
          </Button>
          <Button
            variant="default"
            className="rounded-md px-4 py-1 text-sm font-medium"
            onClick={() => {
              // handleEditMessage();
            }}
          >
            {t('Send')}
          </Button>
          <Button
            variant="outline"
            className="rounded-md border border-neutral-300 px-4 py-1 text-sm font-medium text-neutral-700 hover:bg-neutral-100 dark:border-neutral-700 dark:text-neutral-300 dark:hover:bg-neutral-800"
            onClick={() => {
              onClose();
            }}
          >
            {t('Cancel')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default EditUserMessageModal;
