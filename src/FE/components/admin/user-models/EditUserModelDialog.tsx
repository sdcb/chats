import React, { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { IconLoader } from '@/components/Icons';
import useTranslation from '@/hooks/useTranslation';
import { UserModelPermissionModelDto } from '@/types/adminApis';

interface EditUserModelDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  model: UserModelPermissionModelDto;
  onSave: (tokensDelta: number, countsDelta: number, expires: string) => Promise<void>;
}

export default function EditUserModelDialog({ open, onOpenChange, model, onSave }: EditUserModelDialogProps) {
  const { t } = useTranslation();
  const [tokensDelta, setTokensDelta] = useState<string>('0');
  const [countsDelta, setCountsDelta] = useState<string>('0');
  const [expiresDate, setExpiresDate] = useState<string>('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      setTokensDelta('0');
      setCountsDelta('0');
      if (model.expires) {
        const date = new Date(model.expires);
        setExpiresDate(date.toISOString().split('T')[0]);
      } else {
        const defaultDate = new Date();
        defaultDate.setFullYear(defaultDate.getFullYear() + 1);
        setExpiresDate(defaultDate.toISOString().split('T')[0]);
      }
    }
  }, [open, model]);

  const currentTokens = model.tokens ?? 0;
  const currentCounts = model.counts ?? 0;

  const parsedTokensDelta = parseInt(tokensDelta) || 0;
  const parsedCountsDelta = parseInt(countsDelta) || 0;

  const finalTokens = currentTokens + parsedTokensDelta;
  const finalCounts = currentCounts + parsedCountsDelta;

  const handleSave = async () => {
    if (!expiresDate) {
      return;
    }

    setSaving(true);
    try {
      const expiresISO = new Date(expiresDate).toISOString();
      await onSave(parsedTokensDelta, parsedCountsDelta, expiresISO);
      onOpenChange(false);
    } catch (error) {
      console.error('Failed to save:', error);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[calc(100vw-2rem)] max-w-[500px] sm:w-full">
        <DialogHeader>
          <DialogTitle>{t('Edit User Model')}: {model.name}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="tokens-delta">{t('Token Delta')}</Label>
            <Input
              id="tokens-delta"
              type="number"
              value={tokensDelta}
              onChange={(e) => setTokensDelta(e.target.value)}
              placeholder={t('Enter token change (can be negative)')}
            />
            <div className="text-xs text-muted-foreground">
              {t('Current')}: {currentTokens} → {t('Final')}: {finalTokens}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="counts-delta">{t('Count Delta')}</Label>
            <Input
              id="counts-delta"
              type="number"
              value={countsDelta}
              onChange={(e) => setCountsDelta(e.target.value)}
              placeholder={t('Enter count change (can be negative)')}
            />
            <div className="text-xs text-muted-foreground">
              {t('Current')}: {currentCounts} → {t('Final')}: {finalCounts}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="expires-date">{t('Expiration Date')}</Label>
            <Input
              id="expires-date"
              type="date"
              value={expiresDate}
              onChange={(e) => setExpiresDate(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            {t('Cancel')}
          </Button>
          <Button onClick={handleSave} disabled={saving || !expiresDate}>
            {saving ? (
              <>
                <IconLoader size={16} className="animate-spin mr-2" />
                {t('Saving...')}
              </>
            ) : (
              t('Save')
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
