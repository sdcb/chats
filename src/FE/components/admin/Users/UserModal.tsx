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
    {
      name: 'password',
      label: t('Password'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput
          type="password"
          hidden={!!user}
          options={options}
          field={field}
        />
      ),
    },
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
  ];

  const formSchema = z.object({
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
    password: !user
      ? z
          .string()
          .min(
            6,
            t('Must contain at least {{length}} character(s)', {
              length: 6,
            })!,
          )
          .max(
            18,
            t('Contain at most {{length}} character(s)', { length: 18 })!,
          )
      : z.string(),
    role: z.string().optional(),
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
    // fix bug https://github.com/react-hook-form/react-hook-form/issues/2755
    form.formState.isValid;
    if (user) {
      form.setValue('username', user.username);
      form.setValue('enabled', user.enabled);
      form.setValue('phone', user.phone);
      form.setValue('email', user.email);
      form.setValue('role', user.role);
    }
  }, [isOpen]);

  const onSubmit = (values: z.infer<typeof formSchema>) => {
    if (!form.formState.isValid) return;
    setSubmit(true);
    let p = null;
    let params = {
      ...values,
      username: values.username!,
      password: values.password!,
      role: values.role!,
    };
    if (user) {
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
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{user ? t('Edit User') : t('Add User')}</DialogTitle>
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
