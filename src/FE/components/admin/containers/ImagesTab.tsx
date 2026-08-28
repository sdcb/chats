import { Dispatch, SetStateAction } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconEdit, IconFiles, IconPlus, IconTrash } from '@/components/Icons';
import CatalogCardField from '@/components/admin/containers/CatalogCardField';
import IconActionButton from '@/components/common/IconActionButton';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { LabelSwitch } from '@/components/ui/label-switch';

import { DeleteTarget, EMPTY_VALUE, ImageEntry, ImageForm } from './types';

type Props = {
  images: ImageEntry[];
  loading: boolean;
  saving: boolean;
  dialog: number | 'new' | null;
  form: ImageForm;
  setForm: Dispatch<SetStateAction<ImageForm>>;
  onDialogChange: (dialog: number | 'new' | null) => void;
  onNew: () => void;
  onEdit: (image: ImageEntry) => void;
  onSave: () => Promise<void>;
  onDeleteRequest: (target: DeleteTarget) => void;
};

export default function ImagesTab({
  images,
  loading,
  saving,
  dialog,
  form,
  setForm,
  onDialogChange,
  onNew,
  onEdit,
  onSave,
  onDeleteRequest,
}: Props) {
  const { t } = useTranslation();

  return (
    <>
      <section className="space-y-4">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold">{t('Image catalog')}</h2>
            <p className="text-sm text-muted-foreground">
              {t(
                'Images must be enabled in the catalog before templates can use them.',
              )}
            </p>
          </div>
          <IconActionButton
            label={t('Add image')}
            icon={<IconPlus size={18} />}
            onClick={onNew}
          />
        </div>

        {loading ? (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {[1, 2, 3].map((item) => (
              <Card key={item} className="animate-pulse">
                <CardHeader className="h-20" />
                <CardContent className="h-28" />
              </Card>
            ))}
          </div>
        ) : images.length === 0 ? (
          <div className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
            {t('No images found.')}
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {images.map((image) => (
              <Card key={image.id}>
                <CardHeader className="flex flex-row items-start justify-between space-y-0 p-4">
                  <div className="flex min-w-0 items-start gap-2">
                    <IconFiles size={18} className="mt-0.5 shrink-0" />
                    <div className="min-w-0">
                      <h3 className="break-all font-semibold">{image.image}</h3>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {image.isEnabled ? t('Enabled') : t('Disabled')}
                      </p>
                    </div>
                  </div>
                  <div className="flex shrink-0 items-center gap-1">
                    <IconActionButton
                      label={t('Edit')}
                      icon={<IconEdit size={15} />}
                      className="h-8 w-8"
                      onClick={() => onEdit(image)}
                    />
                    <IconActionButton
                      label={t('Delete')}
                      icon={<IconTrash size={15} />}
                      className="h-8 w-8"
                      onClick={() =>
                        onDeleteRequest({
                          kind: 'image',
                          id: image.id,
                          label: image.image,
                        })
                      }
                    />
                  </div>
                </CardHeader>
                <CardContent className="p-4 pt-0">
                  <dl className="grid gap-y-3">
                    <CatalogCardField label={t('Status')}>
                      <Badge
                        variant={image.isEnabled ? 'default' : 'secondary'}
                      >
                        {image.isEnabled ? t('Enabled') : t('Disabled')}
                      </Badge>
                    </CatalogCardField>
                    <CatalogCardField label={t('Description')}>
                      {image.description || EMPTY_VALUE}
                    </CatalogCardField>
                    <CatalogCardField label={t('ID')}>
                      {image.id}
                    </CatalogCardField>
                  </dl>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </section>

      <Dialog
        open={dialog !== null}
        onOpenChange={(open) => !open && onDialogChange(null)}
      >
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {dialog === 'new' ? t('Add image') : t('Edit image')}
            </DialogTitle>
            <DialogDescription>
              {dialog === 'new'
                ? t(
                    'Images must be enabled in the catalog before templates can use them.',
                  )
                : t(
                    'Renaming an image updates references in resource templates.',
                  )}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <Label>
              {t('Image')}
              <Input
                value={form.image}
                placeholder="registry.example.com/image:tag"
                onChange={(e) => setForm({ ...form, image: e.target.value })}
              />
            </Label>
            <Label>
              {t('Description')}
              <textarea
                className="min-h-24 w-full rounded-md border bg-background px-3 py-2 text-sm"
                value={form.description}
                onChange={(e) =>
                  setForm({ ...form, description: e.target.value })
                }
              />
            </Label>
            <LabelSwitch
              checked={form.isEnabled}
              onCheckedChange={(checked) =>
                setForm({ ...form, isEnabled: checked })
              }
              label={t('Enabled')}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => onDialogChange(null)}>
              {t('Cancel')}
            </Button>
            <Button
              disabled={saving}
              onClick={() => onSave().catch(() => null)}
            >
              {t('Save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
