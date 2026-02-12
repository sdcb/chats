import useTranslation from '@/hooks/useTranslation';

import DateTimePopover from '@/components/Popover/DateTimePopover';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';

interface ApiKeyDialogProps {
  open: boolean;
  mode: 'create' | 'edit';
  title: string;
  name: string;
  onNameChange: (value: string) => void;
  expires: string;
  onExpiresChange: (value: string) => void;
  submitting: boolean;
  submitText: string;
  onSubmit: () => void;
  onCancel: () => void;
  onOpenChange: (open: boolean) => void;
  createdKey?: string | null;
  onCopyCreatedKey?: () => void;
  onCloseCreatedState?: () => void;
}

export default function ApiKeyDialog({
  open,
  mode,
  title,
  name,
  onNameChange,
  expires,
  onExpiresChange,
  submitting,
  submitText,
  onSubmit,
  onCancel,
  onOpenChange,
  createdKey,
  onCopyCreatedKey,
  onCloseCreatedState,
}: ApiKeyDialogProps) {
  const { t } = useTranslation();

  const renderForm = () => (
    <>
      <DialogHeader>
        <DialogTitle>{title}</DialogTitle>
      </DialogHeader>
      <div className="space-y-4">
        <div className="space-y-2">
          <div className="text-sm">{t('Comment')}</div>
          <Input
            value={name}
            onChange={(e) => onNameChange(e.target.value)}
            placeholder={t('Please enter a name')}
          />
        </div>
        <div className="space-y-2">
          <div className="text-sm">{t('Expires')}</div>
          <DateTimePopover
            className="w-full"
            value={expires}
            placeholder={t('Pick a date')}
            onSelect={(date: Date) => {
              onExpiresChange(date.toISOString());
            }}
          />
        </div>
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={onCancel} disabled={submitting}>
          {t('Cancel')}
        </Button>
        <Button onClick={onSubmit} disabled={submitting}>
          {submitText}
        </Button>
      </DialogFooter>
    </>
  );

  const renderCreateSuccess = () => (
    <>
      <DialogHeader>
        <DialogTitle>{t('Create')}</DialogTitle>
        <DialogDescription>
          {t('Please save this API key securely and keep it accessible. For security reasons, you will not be able to view it again in API keys management. If you lose this key, you will need to create a new one.')}
        </DialogDescription>
      </DialogHeader>
      <div className="rounded-md border p-3 font-mono text-sm break-all">
        {createdKey}
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={onCloseCreatedState}>
          {t('Close')}
        </Button>
        <Button onClick={onCopyCreatedKey}>{t('Copy')}</Button>
      </DialogFooter>
    </>
  );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[560px]">
        {mode === 'create' && createdKey ? renderCreateSuccess() : renderForm()}
      </DialogContent>
    </Dialog>
  );
}
