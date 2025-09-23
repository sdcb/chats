import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { toFixed } from '@/utils/common';

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

import { putUserBalance } from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  userId?: string;
  userBalance?: number;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
}

const EditUserBalanceModal = (props: IProps) => {
  const { t } = useTranslation();
  const { userId, userBalance, isOpen, onClose, onSuccessful } = props;
  const [loading, setLoading] = useState(false);
  const formFields: IFormFieldOption[] = [
    {
      name: 'value',
      label: `${t('Recharge Amount')}(${t('Yuan')})`,
      defaultValue: 0,
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput type="number" options={options} field={field} />
      ),
    },
  ];

  const formSchema = z.object({
    value: z.coerce
      .number()
      .min(
        -9999,
        t('The recharge amount must be greater than {{count}}', {
          count: -9999,
        })!,
      )
      .max(
        9999,
        t('The maximum recharge amount is {{count}}', { count: 9999 })!,
      ),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: formFields.reduce((obj: any, field) => {
      obj[field.name] = field.defaultValue;
      return obj;
    }, {}),
  });

  useEffect(() => {
    form.reset();
    form.formState.isValid;
  }, [isOpen]);

  const onSubmit = (values: z.infer<typeof formSchema>) => {
    if (!form.formState.isValid) return;
    setLoading(true);
    putUserBalance({ userId: userId!, value: values.value })
      .then(() => {
        toast.success(t('Recharged successfully'));
        onSuccessful();
      })
      .finally(() => {
        setLoading(false);
      });
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      {isOpen && (
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('User recharge')}</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            {t('Current Balance')}: {toFixed(+userBalance!)} {t('Yuan')}
          </p>
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
                <Button disabled={loading} type="submit">
                  {t('Confirm recharge')}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      )}
    </Dialog>
  );
};

export default EditUserBalanceModal;
