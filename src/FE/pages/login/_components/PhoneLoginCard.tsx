import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { PhoneRegExp, SmsExpirationSeconds } from '@/utils/common';
import { saveUserInfo, setUserSession } from '@/utils/user';

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

import { sendLoginSmsCode, signByPhone } from '@/apis/clientApis';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

const PhoneLoginCard = (props: {
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
    phone: z.string().regex(PhoneRegExp, {
      message: t('Please enter the correct phone number')!,
    }),
  });

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    mode: 'all',
    defaultValues: {
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

  const sendCode = () => {
    setSending(true);
    if (form.formState.isValid) {
      const phone = form.getValues('phone');
      sendLoginSmsCode(phone)
        .then(() => {
          toast.success(t('SMS sent successfully'));
          setIsSendCode(true);
        })
        .finally(() => {
          setSending(false);
        });
    }
  };

  const sign = () => {
    form.trigger();
    if (form.formState.isValid && smsCode.length === 6) {
      const phone = form.getValues('phone');
      openLoading();
      signByPhone(phone, smsCode)
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
              name="phone"
              render={({ field }) => (
                <FormItem className="flex flex-col items-start">
                  <FormControl className="w-full">
                    <div className='mt-4'>
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

export default PhoneLoginCard;
