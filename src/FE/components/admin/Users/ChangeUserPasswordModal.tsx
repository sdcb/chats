import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { GetUsersResult } from '@/types/adminApis';

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

import { putUser } from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  user?: GetUsersResult | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
}

const isStrongPassword = (value: string) => {
  if (value.length < 8) return false;
  const types = [
    /[a-z]/.test(value),
    /[A-Z]/.test(value),
    /\d/.test(value),
    /[^A-Za-z0-9]/.test(value),
  ];
  return types.filter(Boolean).length >= 3;
};

const ChangeUserPasswordModal = ({
  user,
  isOpen,
  onClose,
  onSuccessful,
}: IProps) => {
  const { t } = useTranslation();
  const [submit, setSubmit] = useState(false);
  const formSchema = z
    .object({
      password: z
        .string()
        .refine(
          isStrongPassword,
          t(
            'Password should be at least 8 characters and contain at least three character types.',
          )!,
        ),
      confirmPassword: z.string(),
    })
    .refine((values) => values.password === values.confirmPassword, {
      path: ['confirmPassword'],
      message: t('The two password inputs are inconsistent')!,
    });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      password: '',
      confirmPassword: '',
    },
  });

  useEffect(() => {
    if (isOpen) {
      form.reset();
    }
  }, [form, isOpen]);

  const onSubmit = (values: z.infer<typeof formSchema>) => {
    if (!user || !form.formState.isValid) return;

    setSubmit(true);
    putUser({
      id: user.id,
      password: values.password,
      confirmPassword: values.confirmPassword,
    })
      .then(() => {
        toast.success(t('Save successful'));
        onSuccessful();
      })
      .finally(() => {
        setSubmit(false);
      });
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="flex max-h-[calc(100dvh-2rem)] flex-col overflow-hidden sm:max-w-md">
        <DialogHeader className="shrink-0">
          <DialogTitle>{t('Change Password')}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form
            className="flex min-h-0 flex-1 flex-col"
            onSubmit={form.handleSubmit(onSubmit)}
          >
            <div className="min-h-0 flex-1 overflow-y-auto pr-1">
              <FormField
                control={form.control}
                name="password"
                render={({ field }) => (
                  <FormInput
                    type="password"
                    label={t('New Password')!}
                    field={field}
                    autocomplete="new-password"
                  />
                )}
              />
              <FormField
                control={form.control}
                name="confirmPassword"
                render={({ field }) => (
                  <FormInput
                    type="password"
                    label={t('Confirm Password')!}
                    field={field}
                    autocomplete="new-password"
                  />
                )}
              />
            </div>
            <DialogFooter className="shrink-0 border-t bg-background pt-4">
              <Button disabled={submit} type="submit">
                {t('Save')}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};

export default ChangeUserPasswordModal;
