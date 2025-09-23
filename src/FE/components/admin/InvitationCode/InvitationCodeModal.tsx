import React, { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { GetInvitationCodeResult } from '@/types/adminApis';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Form, FormField } from '@/components/ui/form';
import FormInput from '@/components/ui/form/input';
import { FormFieldType, IFormFieldOption } from '@/components/ui/form/type';

import {
  deleteInvitationCode,
  postInvitationCode,
  putInvitationCode,
} from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  selected: GetInvitationCodeResult | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
}

const InvitationCodeModal = (props: IProps) => {
  const { t } = useTranslation();
  const { selected, isOpen, onClose, onSuccessful } = props;
  const formFields: IFormFieldOption[] = [
    {
      name: 'value',
      label: t('Invitation Code'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput disabled={!!selected} options={options} field={field} />
      ),
    },
    {
      name: 'count',
      label: t('Use count'),
      defaultValue: 0,
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput type="number" options={options} field={field} />
      ),
    },
  ];

  const formSchema = z.object({
    value: z.string().min(1, `${t('This field is require')}`),
    count: z.union([z.string(), z.number()]).optional(),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: formFields.reduce((obj: any, field) => {
      obj[field.name] = field.defaultValue;
      return obj;
    }, {}),
  });

  function onSubmit(values: z.infer<typeof formSchema>) {
    if (!form.formState.isValid) return;
    const { value, count } = values;
    let p = null;
    if (selected) {
      p = putInvitationCode({ id: selected.id, count: +count! });
    } else {
      p = postInvitationCode({ value: value!, count: +count! });
    }
    p.then(() => {
      onSuccessful();
      toast.success(t('Save successful'));
    });
  }

  const onDelete = () => {
    deleteInvitationCode(selected!.id).then(() => {
      onSuccessful();
      toast.success(t('Delete successful'));
    });
  };

  useEffect(() => {
    if (isOpen) {
      form.reset();
      form.formState.isValid;
      if (selected) {
        form.setValue('value', selected.value);
        form.setValue('count', selected.count);
      }
    }
  }, [isOpen]);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {selected ? t('Edit Invitation Code') : t('Add Invitation Code')}
          </DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)}>
            {formFields.map((item) => (
              <FormField
                key={item.name}
                control={form.control}
                name={item.name as never}
                render={({ field }) => item.render(item, field)}
              />
            ))}
            <DialogFooter className="pt-4">
              {selected && (
                <Button type="button" variant="destructive" onClick={onDelete}>
                  {t('Delete')}
                </Button>
              )}
              <Button type="submit">{t('Save')}</Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};
export default InvitationCodeModal;
