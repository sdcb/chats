import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { PhoneRegExp, SmsExpirationSeconds } from '@/utils/common';
import { saveUserInfo, setUserSession } from '@/utils/user';
import { redirectToGithub } from '@/utils/website';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';

import { registerByPhone, sendRegisterSmsCode } from '@/apis/clientApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

const PhoneRegisterCard = (props: {
  loginLoading: boolean;
  openLoading: Function;
  closeLoading: Function;
}) => {
  const { loginLoading, openLoading, closeLoading } = props;
  const { t } = useTranslation();
  const router = useRouter();
  const [seconds, setSeconds] = useState(SmsExpirationSeconds - 1);
  const [isSendCode, setIsSendCode] = useState(false);
  const [smsCode, setSmsCode] = useState('');
  const [sending, setSending] = useState(false);

  const formSchema = z.object({
    invitationCode: z
      .string()
      .min(1, t('Please enter the correct invitation code')!)
      .optional(),
    phone: z.string().regex(PhoneRegExp, {
      message: t('Please enter the correct phone number')!,
    }),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    mode: 'all',
    defaultValues: {
      invitationCode: '',
      phone: '',
    },
  });

  useEffect(() => {
    form.formState.isValid;
  }, []);

  useEffect(() => {
    let timer: any;
    if (isSendCode && seconds > 0) {
      timer = setInterval(() => {
        setSeconds((prevSeconds) => prevSeconds - 1);
      }, 1000);
    }

    if (seconds === 0) {
      setIsSendCode(false);
      setSeconds(SmsExpirationSeconds);
    }

    return () => clearInterval(timer);
  }, [isSendCode, seconds]);

  const sendCode = async () => {
    setSending(true);
    if (form.formState.isValid) {
      const invitationCode = form.getValues('invitationCode');
      try {
        const phone = form.getValues('phone');
        await sendRegisterSmsCode(phone, invitationCode);
        toast.success(t('SMS sent successfully'));
        setIsSendCode(true);
      } catch (resp: any) {
        const body = await resp.json();
        toast.error(
          body.message || t('SMS send failed, please try again later'),
        );
      } finally {
        setSending(false);
      }
    }
  };

  const sign = () => {
    form.trigger();
    if (form.formState.isValid && smsCode.length === 6) {
      const phone = form.getValues('phone');
      const invitationCode = form.getValues('invitationCode')!;
      openLoading();
      registerByPhone(phone, smsCode, invitationCode)
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
  };
  return (
    <Card>
      <CardContent className="space-y-2">
        <Form {...form}>
          <form>
            <FormField
              control={form.control}
              name="invitationCode"
              render={({ field }) => (
                <FormItem className="flex flex-col items-start">
                  <FormControl className="w-full">
                    <div className='mt-4'>
                      <div className="py-2.5 text-sm font-medium leading-none">
                        {t('Invitation Code')}
                      </div>
                      <div className="flex border rounded-md">
                        <Input
                          className="w-full m-0 border-none outline-none bg-transparent rounded-md"
                          {...field}
                        />
                        <Button
                          type="button"
                          className="absolute right-10 text-center px-0"
                          variant="link"
                          onClick={redirectToGithub}
                        >
                          {t('No Invitation Code?')}
                        </Button>
                      </div>
                    </div>
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="phone"
              render={({ field }) => (
                <FormItem className="flex flex-col items-start">
                  <FormControl className="w-full">
                    <div>
                      <div className="py-2.5 text-sm font-medium leading-none">
                        {t('Phone Number')}
                      </div>
                      <div className="flex border rounded-md">
                        <Button
                          type="button"
                          variant="ghost"
                          className="absolute font-semibold"
                        >
                          +86
                        </Button>
                        <Input
                          className="w-full m-0 border-none outline-none bg-transparent rounded-md p-0 pl-14"
                          {...field}
                        />
                      </div>
                    </div>
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </form>
        </Form>
        <div className="pt-2">
          <div className="py-2.5 text-sm font-medium leading-none">
            {t('Code')}
          </div>
          <div className="flex border rounded-md">
            <Input
              value={smsCode}
              onChange={(e) => {
                setSmsCode(e.target.value);
              }}
              className="m-0 border-none outline-none bg-transparent rounded-md p-0 pr-[102px] pl-4"
            />
            <Button
              className="absolute right-10 text-center px-0"
              disabled={!form.formState.isValid || isSendCode || sending}
              variant="link"
              onClick={sendCode}
            >
              {isSendCode ? seconds + ' s' : t('Send code')}
            </Button>
          </div>
        </div>
        <div className="pt-4">
          <Button
            className="w-full"
            type="submit"
            onClick={sign}
            disabled={loginLoading}
          >
            {loginLoading ? t('Logging in...') : t('Login to your account')}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
};

export default PhoneRegisterCard;
