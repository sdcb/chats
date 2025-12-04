import { useEffect, useState } from 'react';
import Image from 'next/image';

import useTranslation from '@/hooks/useTranslation';

import { redirectToGithub } from '@/utils/website';

import { LoginConfigsResult } from '@/types/clientApis';
import { SiteInfoConfig } from '@/types/config';
import { LoginType } from '@/types/user';

import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import AccountLoginCard from '@/components/login/AccountLoginCard';
import KeyCloakLogin from '@/components/login/KeyCloakLogin';
import PhoneLoginCard from '@/components/login/PhoneLoginCard';
import PhoneRegisterCard from '@/components/login/PhoneRegisterCard';
import WeChatLogin from '@/components/login/WeChatLogin';

import { getLoginProviders, getSiteInfo } from '@/apis/clientApis';

enum TabKeys {
  PHONE = 'phone',
  REGISTER = 'register',
  ACCOUNT = 'account',
}

type LoginHeader = {
  [key in TabKeys]: { title: string; description: string };
};

const getFeVersion = () =>
  typeof window !== 'undefined'
    ? (window as any).FE_VERSION as string
    : process.env.FE_VERSION as string;

export default function LoginPage() {
  const { t } = useTranslation();
  const [isClient, setIsClient] = useState(false);
  const [webSiteInfo, setWebSiteInfo] = useState<SiteInfoConfig>();
  const [feVersion, setFeVersion] = useState<string>(getFeVersion);
  const LoginHeaders: LoginHeader = {
    phone: {
      title: t('Sign in to Chats'),
      description: t(
        'Please enter your phone number and verification code below to complete the login',
      ),
    },
    register: {
      title: t('Welcome to register'),
      description: t(
        'Please enter your phone number and invitation code below to complete the register',
      ),
    },
    account: {
      title: t('Sign in to Chats'),
      description: t(
        'Please enter your account name and password below to complete the login',
      ),
    },
  };
  const [loginConfigs, setLoginConfigs] = useState<LoginConfigsResult[]>([]);
  const [loginLoading, setLoginLoading] = useState(false);
  const [currentTab, setCurrentTab] = useState<TabKeys>(TabKeys.ACCOUNT);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsClient(true);
    getLoginProviders()
      .then((data) => {
        let hasPhoneType = false;
        setLoginConfigs(
          (data || []).map((x) => {  // 添加空值检查
            if (x.key === LoginType.Phone) {
              hasPhoneType = true;
            }
            return {
              type: x.key,
              configs: x.config,
            };
          }),
        );
        setCurrentTab(hasPhoneType ? TabKeys.PHONE : TabKeys.ACCOUNT);
        setLoading(false);
      })
      .catch((error) => {  // 添加错误处理
        console.error('Failed to load login providers:', error);
        setLoginConfigs([]);
        setCurrentTab(TabKeys.ACCOUNT);
        setLoading(false);
    });
    getSiteInfo().then((data) => {
      setWebSiteInfo(data);
    });
    setFeVersion(getFeVersion());
  }, []);

  const openLoading = () => setLoginLoading(true);
  const closeLoading = () => setTimeout(() => setLoginLoading(false), 600);

  const hasLoginType = (type: LoginType) =>
    !!loginConfigs.find((x) => x.type === type);

  const renderTabsList = () => {
    return hasLoginType(LoginType.Phone) ? (
      <TabsList className="flex w-full flex-row justify-around">
        {hasLoginType(LoginType.Phone) && (
          <TabsTrigger
            value={TabKeys.PHONE}
            className="flex justify-center w-full"
          >
            {t('Mobile Login')}
          </TabsTrigger>
        )}
        {hasLoginType(LoginType.Phone) && (
          <TabsTrigger
            value={TabKeys.REGISTER}
            className="flex justify-center w-full"
          >
            {t('Register')}
          </TabsTrigger>
        )}
        <TabsTrigger
          value={TabKeys.ACCOUNT}
          className="flex justify-center w-full"
        >
          {t('Account Login')}
        </TabsTrigger>
      </TabsList>
    ) : (
      <></>
    );
  };

  return (
    <>
      {!loading && isClient && (
        <div className="container relative h-screen flex-col items-center justify-center md:grid lg:max-w-none lg:grid-cols-2 lg:px-0">
          <div className="relative hidden h-full flex-col  p-10 text-white dark:text-black lg:flex dark:border-r">
            <div className="absolute inset-0 dark:bg-gray-50 bg-zinc-900 bg-[url('/images/login_bg.avif')] bg-cover" />
            <div className="relative z-20 flex items-center text-lg font-medium text-white">
              <Image
                src="/icons/logo.png"
                width={32}
                height={32}
                className="mr-2 h-8 w-8"
                alt="logo"
                priority
              />
              Chats
            </div>
          </div>
          <div className="lg:px-8 lg:pt-8 pb-4 h-screen">
            <div className="mx-auto flex h-5/6 h- w-full flex-col justify-center space-y-6">
              <div
                className="flex flex-col space-y-2 text-center mt-12 md:mt-0 lg:mt-0"
                key={currentTab}
              >
                <h1 className="text-2xl font-semibold tracking-tight">
                  {LoginHeaders[currentTab].title}
                </h1>
                <p className="text-sm text-muted-foreground">
                  {LoginHeaders[currentTab].description}
                </p>
              </div>
              <>
                <div className="flex w-full justify-center">
                  <div className="relative w-full max-w-md max-h-full">
                    <div className="relative">
                      <Tabs
                        defaultValue={currentTab}
                        onValueChange={(value) => {
                          setCurrentTab(value as TabKeys);
                        }}
                        className="flex-col"
                      >
                        {renderTabsList()}
                        <TabsContent className="m-0 mt-4" value={TabKeys.PHONE}>
                          <PhoneLoginCard
                            openLoading={openLoading}
                            closeLoading={closeLoading}
                            loginLoading={loginLoading}
                          />
                        </TabsContent>
                        <TabsContent
                          className="m-0 mt-4"
                          value={TabKeys.REGISTER}
                        >
                          <PhoneRegisterCard
                            openLoading={openLoading}
                            closeLoading={closeLoading}
                            loginLoading={loginLoading}
                          />
                        </TabsContent>
                        <TabsContent
                          className="m-0 mt-4"
                          value={TabKeys.ACCOUNT}
                        >
                          <AccountLoginCard
                            openLoading={openLoading}
                            closeLoading={closeLoading}
                            loginLoading={loginLoading}
                          />
                        </TabsContent>
                      </Tabs>

                      {loginConfigs.length > 0 && (
                        <div className="relative mt-4">
                          <div className="absolute inset-0 flex items-center">
                            <span className="w-full border-t" />
                          </div>
                          <div className="relative flex justify-center text-xs uppercase">
                            <span className="bg-background p-4 text-muted-foreground">
                              {t('Or continue with')}
                            </span>
                          </div>
                        </div>
                      )}

                      <div className="flex justify-center gap-2">
                        {hasLoginType(LoginType.WeChat) && (
                          <WeChatLogin
                            configs={
                              loginConfigs.find(
                                (x) => x.type === LoginType.WeChat,
                              )?.configs
                            }
                            loading={loginLoading}
                          />
                        )}
                        {hasLoginType(LoginType.Keycloak) && (
                          <KeyCloakLogin loading={loginLoading} />
                        )}
                      </div>
                    </div>
                  </div>
                </div>
              </>
              <div className="flex flex-col justify-center text-center text-sm text-muted-foreground">
                <div className="flex text-sm justify-center items-center pb-[2px]">
                  {webSiteInfo?.customizedLine1}
                </div>
                <div className="flex text-sm justify-center items-center">
                  {webSiteInfo?.customizedLine2}
                </div>
              </div>
            </div>

            <div className="flex justify-between items-end h-1/6 text-sm text-muted-foreground">
              <div className="flex items-center text-center">
                © {new Date().getFullYear()}&nbsp;
                <Button
                  className="p-0 m-0 h-auto font-semibold text-sm text-muted-foreground"
                  variant="link"
                  onClick={redirectToGithub}
                >
                  Sdcb Chats
                </Button>
                . All Rights Reserved.
              </div>
              <div className="text-xs text-muted-foreground text-right font-mono">
                {feVersion}
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
