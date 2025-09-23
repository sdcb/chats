import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { saveUserInfo, setUserSession } from '@/utils/user';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Form,
  FormField,
} from '@/components/ui/form';
import FormInput from '@/components/ui/form/input';
import FormPasswordInput from '@/components/ui/form/passwordInput';
import { FormFieldType, IFormFieldOption } from '@/components/ui/form/type';

import { singIn } from '@/apis/clientApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

const AccountLoginCard = (props: {
  loginLoading: boolean;
  openLoading: Function;
  closeLoading: Function;
}) => {
  const { loginLoading, openLoading, closeLoading } = props;
  const { t } = useTranslation();
  const router = useRouter();

  useEffect(() => {
    form.formState.isValid;
  }, []);

  const formFields: IFormFieldOption[] = [
    {
      name: 'username',
      label: t('Your username'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormInput autocomplete="username" options={options} field={field} />
      ),
    },
    {
      name: 'password',
      label: t('Your password'),
      defaultValue: '',
      render: (options: IFormFieldOption, field: FormFieldType) => (
        <FormPasswordInput 
          autocomplete="current-password" 
          label={options.label} 
          field={field}
        />
      ),
    },
  ];

  const formSchema = z.object({
    username: z.string().min(1, `${t('Please enter you user name')}`),
    password: z.string().min(1, `${t('Please enter you password')}`),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: formFields.reduce((obj: any, field) => {
      obj[field.name] = field.defaultValue;
      return obj;
    }, {}),
  });

  async function onSubmit(values: z.infer<typeof formSchema>) {
    if (!form.formState.isValid) return;
    openLoading();
    const { username, password } = values;
    singIn({ username, password })
      .then((response) => {
        setUserSession(response.sessionId);
        saveUserInfo({
          role: response.role,
          username: response.username,
        });
        router.push('/');
      })
      .finally(() => {
        closeLoading();
      });
  }

  return (
    <Card>
      <CardContent className="space-y-2">
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className='mt-4'>
            {formFields.map((item) => (
              <FormField
                key={item.name}
                control={form.control}
                name={item.name as never}
                render={({ field }) => item.render(item, field)}
              />
            ))}
            <div className="w-full flex justify-center">
              <Button className="w-full" disabled={loginLoading} type="submit">
                {loginLoading ? t('Logging in...') : t('Login to your account')}
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
};

export default AccountLoginCard;
