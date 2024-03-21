import { createUser, putUser } from '@/apis/adminService';
import { GetUsersResult } from '@/types/admin';
import { z } from 'zod';
import { useTranslation } from 'next-i18next';
import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../ui/dialog';
import { Form, FormField } from '../../ui/form';
import { FormFieldType, IFormFieldOption } from '../../ui/form/type';
import FormInput from '../../ui/form/input';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '../../ui/button';
import FormSelect from '../../ui/form/select';

interface IProps {
  user?: GetUsersResult | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
  saveLoading?: boolean;
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

export const UserModal = (props: IProps) => {
  const { t } = useTranslation('admin');
  const { user, isOpen, onClose, onSuccessful } = props;
  const [loading, setLoading] = useState(false);
  const formFields: IFormFieldOption[] = [
    {
      name: 'username',
      label: t('User Name'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput disabled={!!user} options={options} field={field} />
      ),
    },
    {
      name: 'password',
      label: t('Password'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput
          type='password'
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
  ];

  const formSchema = z.object({
    username: z
      .string()
      .min(
        2,
        t('Must contain at least {{length}} character(s)', {
          length: 2,
        })!
      )
      .max(10, t('Contain at most {{length}} character(s)', { length: 10 })!),
    password: !user
      ? z
          .string()
          .min(
            6,
            t('Must contain at least {{length}} character(s)', {
              length: 6,
            })!
          )
          .max(
            18,
            t('Contain at most {{length}} character(s)', { length: 18 })!
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
      form.setValue('role', user.role);
    }
  }, [isOpen]);

  const onSubmit = (values: z.infer<typeof formSchema>) => {
    if (!form.formState.isValid) return;
    setLoading(true);
    let p = null;
    let params = {
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
      p = createUser(params);
    }
    p.then(() => {
      toast.success(t('Save successful!'));
      onSuccessful();
    })
      .catch(() => {
        toast.error(
          t(
            'Operation failed! Please try again later, or contact technical personnel.'
          )
        );
      })
      .finally(() => {
        setLoading(false);
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
            <DialogFooter className='pt-4'>
              <Button disabled={loading} type='submit'>
                {t('Save')}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
};