import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';
import { useRouter } from 'next/router';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import useTranslation from '@/hooks/useTranslation';
import { clearUserInfo, clearUserSession, getLoginUrl } from '@/utils/user';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Form, FormField } from '@/components/ui/form';
import FormPasswordInput from '@/components/ui/form/passwordInput';
import { FormFieldType, IFormFieldOption } from '@/components/ui/form/type';

import { changeUserPassword } from '@/apis/clientApis';

const AccountTab = () => {
  const { t } = useTranslation();
  const router = useRouter();
  const [loading, setLoading] = useState(false);

  const formSchema = z.object({
    oldPassword: z.string(),
    newPassword: z
      .string()
      .regex(
        /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)|(?=.*[a-z])(?=.*[A-Z])(?=.*\W)|(?=.*[a-z])(?=.*\d)(?=.*\W)|(?=.*[A-Z])(?=.*\d)(?=.*\W).{6,}$/,
        t(
          'It must contain at least 6 characters, and 3 of the 4 must be upper case, lower case, special characters, and numbers.',
        )!,
      )
      .min(
        6,
        t('Must contain at least {{length}} character(s)', {
          length: 6,
        })!,
      )
      .max(18, t('Contain at most {{length}} character(s)', { length: 18 })!),
    confirmPassword: z
      .string()
      .regex(
        /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)|(?=.*[a-z])(?=.*[A-Z])(?=.*\W)|(?=.*[a-z])(?=.*\d)(?=.*\W)|(?=.*[A-Z])(?=.*\d)(?=.*\W).{6,}$/,
        t(
          'It must contain at least 6 characters, and 3 of the 4 must be upper case, lower case, special characters, and numbers.',
        )!,
      )
      .min(
        6,
        t('Must contain at least {{length}} character(s)', {
          length: 6,
        })!,
      )
      .max(18, t('Contain at most {{length}} character(s)', { length: 18 })!),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      oldPassword: '',
      newPassword: '',
      confirmPassword: '',
    },
  });

  const onSubmit = (values: z.infer<typeof formSchema>) => {
    const { newPassword, confirmPassword } = values;
    if (confirmPassword !== newPassword) {
      toast.error(t('The two password inputs are inconsistent'));
      return;
    }
    setLoading(true);
    changeUserPassword(values)
      .then(() => {
        toast.success(t('Modified successfully'));
        clearUserSession();
        clearUserInfo();
        router.push(getLoginUrl());
        setLoading(false);
      })
      .finally(() => {
        setLoading(false);
      });
  };

  return (
    <div className="w-full">
      <h2 className="text-base font-semibold mb-2">{t('Change Password')}</h2>
      <Card className="border-none">
        <CardContent>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-1">
              <FormField
                control={form.control}
                name="oldPassword"
                render={({ field }) => (
                  <FormPasswordInput 
                    label={t('Old Password')} 
                    field={field}
                    autocomplete="current-password"
                  />
                )}
              />
              <FormField
                control={form.control}
                name="newPassword"
                render={({ field }) => (
                  <FormPasswordInput 
                    label={t('New Password')} 
                    field={field}
                    autocomplete="new-password"
                  />
                )}
              />
              <FormField
                control={form.control}
                name="confirmPassword"
                render={({ field }) => (
                  <FormPasswordInput 
                    label={t('Confirm Password')} 
                    field={field}
                    autocomplete="new-password"
                  />
                )}
              />
              <div className="pt-4">
                <Button disabled={loading} type="submit" className="w-full sm:w-auto">
                  {t('Confirm')}
                </Button>
              </div>
            </form>
          </Form>
        </CardContent>
      </Card>
    </div>
  );
};

export default AccountTab; 