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
import FormSelect from '@/components/ui/form/select';
import FormSwitch from '@/components/ui/form/switch';

import { putUser } from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  user: GetUsersResult;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
}
const ROLES = [
  { name: '-', value: '-' },
  { name: 'Admin', value: 'admin' },
];

const schema = z.object({
  username: z.string().min(2).max(20),
  enabled: z.boolean().optional(),
  role: z.string().optional(),
  phone: z.string().nullable(),
  email: z.string().nullable(),
  sub: z.string().optional(),
  apiKeyEnabled: z.boolean().optional(),
});
type Values = z.infer<typeof schema>;

export default function EditUserModal({
  user,
  isOpen,
  onClose,
  onSuccessful,
}: IProps) {
  const { t } = useTranslation();
  const [submit, setSubmit] = useState(false);
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      username: '',
      enabled: true,
      role: '-',
      phone: '',
      email: '',
      sub: '',
      apiKeyEnabled: true,
    },
  });
  useEffect(() => {
    if (isOpen)
      form.reset({
        username: user.username,
        enabled: user.enabled,
        role: user.role,
        phone: user.phone,
        email: user.email,
        sub: user.sub || '',
        apiKeyEnabled: user.apiKeyEnabled,
      });
  }, [form, isOpen, user]);
  const onSubmit = (values: Values) => {
    setSubmit(true);
    const params: any = { ...values, id: user.id };
    if (user.provider?.toLowerCase() !== 'keycloak') delete params.sub;
    putUser(params)
      .then(() => {
        toast.success(t('Save successful'));
        onSuccessful();
      })
      .finally(() => setSubmit(false));
  };
  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="flex h-[calc(100dvh-2rem)] max-h-[calc(100dvh-2rem)] flex-col overflow-hidden sm:h-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('Edit User')}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form
            className="flex min-h-0 flex-1 flex-col"
            onSubmit={form.handleSubmit(onSubmit)}
          >
            <div className="min-h-0 flex-1 overflow-y-auto pr-1">
              <FormField
                control={form.control}
                name="username"
                render={({ field }) => (
                  <FormInput label={t('User Name')} field={field} />
                )}
              />
              <FormField
                control={form.control}
                name="enabled"
                render={({ field }) => (
                  <FormSwitch label={t('Is it enabled')} field={field} />
                )}
              />
              <FormField
                control={form.control}
                name="role"
                render={({ field }) => (
                  <FormSelect label={t('Role')} items={ROLES} field={field} />
                )}
              />
              <FormField
                control={form.control}
                name="phone"
                render={({ field }) => (
                  <FormInput label={t('Phone')} field={field} />
                )}
              />
              <FormField
                control={form.control}
                name="email"
                render={({ field }) => (
                  <FormInput label={t('E-Mail')} field={field} />
                )}
              />
              <FormField
                control={form.control}
                name="sub"
                render={({ field }) => (
                  <FormInput
                    label="SSO Sub"
                    field={field}
                    disabled={user.provider?.toLowerCase() !== 'keycloak'}
                  />
                )}
              />
              <FormField
                control={form.control}
                name="apiKeyEnabled"
                render={({ field }) => (
                  <FormSwitch label={t('Allow API Key')} field={field} />
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
}
