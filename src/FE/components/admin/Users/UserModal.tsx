import React, { useEffect, useState } from 'react';
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
import { FormFieldType, IFormFieldOption } from '@/components/ui/form/type';

import { postUser, putUser } from '@/apis/adminApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

interface IProps {
  user?: GetUsersResult | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
}

const ROLES = [
  {
    name: '-',
    value: '-',
  },
  {
    name: 'Admin',
    value: 'admin',
  },
];

const UserModal = (props: IProps) => {
  const { t } = useTranslation();
  const { user, isOpen, onClose, onSuccessful } = props;
  const [submit, setSubmit] = useState(false);
  const formFields: IFormFieldOption[] = [
    ...(user
      ? [
          {
            name: 'account',
            label: t('Account'),
            defaultValue: '',
            render: (options: IFormFieldOption, field: FormFieldType) => (
              <FormInput options={options} field={field} disabled />
            ),
          },
          {
            name: 'provider',
            label: t('Login Type'),
            defaultValue: '',
            render: (options: IFormFieldOption, field: FormFieldType) => (
              <FormInput options={options} field={field} disabled />
            ),
          },
        ]
      : []),
    {
      name: 'username',
      label: t('User Name'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput options={options} field={field} />
      ),
    },
    {
      name: 'enabled',
      label: t('Is it enabled'),
      defaultValue: true,
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormSwitch options={options} field={field} />
      ),
    },
    ...(!user
      ? [
          {
            name: 'password',
            label: t('Password'),
            defaultValue: '',
            render: (options: IFormFieldOption, field: FormFieldType) => (
              <FormInput type="password" options={options} field={field} />
            ),
          },
        ]
      : []),
    {
      name: 'role',
      label: t('Role'),
      defaultValue: '-',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormSelect items={ROLES} options={options} field={field} />
      ),
    },
    {
      name: 'phone',
      label: t('Phone'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput options={options} field={field} />
      ),
    },
    {
      name: 'email',
      label: t('E-Mail'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput options={options} field={field} />
      ),
    },
    ...(user
      ? [
          {
            name: 'sub',
            label: 'SSO Sub',
            defaultValue: '',
            render: (options: IFormFieldOption, field: FormFieldType) => (
              <FormInput
                options={options}
                field={field}
                disabled={user.provider?.toLowerCase() !== 'keycloak'}
              />
            ),
          },
          {
            name: 'apiKeyEnabled',
            label: t('Allow API Key'),
            defaultValue: true,
            render: (options: IFormFieldOption, field: FormFieldType) => (
              <FormSwitch options={options} field={field} />
            ),
          },
        ]
      : []),
  ];

  const formSchema = z.object({
    account: z.string().optional(),
    provider: z.string().optional(),
    username: z
      .string()
      .min(
        2,
        t('Must contain at least {{length}} character(s)', {
          length: 2,
        })!,
      )
      .max(20, t('Contain at most {{length}} character(s)', { length: 20 })!),
    enabled: z.boolean().optional(),
    phone: z.string().nullable().default(null),
    email: z.string().nullable().default(null),
    password: z.string().optional(),
    sub: z.string().optional(),
    apiKeyEnabled: z.boolean().optional(),
    role: z.string().optional(),
  });

  const createFormSchema = formSchema.extend({
    password: z
      .string()
      .min(
        6,
        t('Must contain at least {{length}} character(s)', {
          length: 6,
        })!,
      )
      .max(18, t('Contain at most {{length}} character(s)', { length: 18 })!),
  });

  // Password changes are handled by ChangeUserPasswordModal. The optional
  // empty value only accommodates the stale default retained by
  // react-hook-form when switching from create to edit.
  const editFormSchema = formSchema.extend({
    password: z.literal('').optional(),
  });

  const activeFormSchema = user ? editFormSchema : createFormSchema;

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(activeFormSchema),
    defaultValues: formFields.reduce((obj: any, field) => {
      obj[field.name] = field.defaultValue;
      return obj;
    }, {}),
  });

  useEffect(() => {
    form.reset();
    // fix bug https://github.com/react-hook-form/react-hook-form/issues/2755
    form.formState.isValid;
    if (user) {
      form.setValue('username', user.username);
      form.setValue('enabled', user.enabled);
      form.setValue('phone', user.phone);
      form.setValue('email', user.email);
      form.setValue('role', user.role);
      form.setValue('account', user.account);
      form.setValue('provider', user.provider || t('Account password login'));
      form.setValue('sub', user.sub || '');
      form.setValue('apiKeyEnabled', user.apiKeyEnabled);
    }
  }, [isOpen]);

  const onSubmit = (values: z.infer<typeof formSchema>) => {
    setSubmit(true);
    let p = null;
    const params: any = {
      ...values,
      username: values.username!,
      password: values.password!,
      role: values.role!,
    };
    if (user) {
      delete params.account;
      delete params.provider;
      if (!params.password) {
        delete params.password;
        delete params.confirmPassword;
      }
      if (user.provider?.toLowerCase() !== 'keycloak') {
        delete params.sub;
      }
      p = putUser({
        id: user.id,
        ...params,
      });
    } else {
      p = postUser(params);
    }
    p.then(() => {
      toast.success(t('Save successful'));
      onSuccessful();
    }).finally(() => {
      setSubmit(false);
    });
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="flex h-[calc(100dvh-2rem)] max-h-[calc(100dvh-2rem)] flex-col overflow-hidden sm:h-auto sm:max-w-lg">
        <DialogHeader className="shrink-0">
          <DialogTitle>{user ? t('Edit User') : t('Add User')}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form
            className="flex min-h-0 flex-1 flex-col"
            onSubmit={form.handleSubmit(onSubmit)}
          >
            <div className="min-h-0 flex-1 overflow-y-auto pr-1">
              {formFields.map((item) => (
                <FormField
                  key={item.name}
                  control={form.control}
                  name={item.name as never}
                  render={({ field }) => item.render(item, field)}
                />
              ))}
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
export default UserModal;
