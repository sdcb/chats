import { useEffect, useMemo } from 'react';

import { useRouter } from 'next/router';

import {
  IconMessage,
  IconPasswordUser,
  IconShieldLock,
} from '@/components/Icons';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/components/ui/tabs';
import useTranslation from '@/hooks/useTranslation';
import KeycloakAttemptsPanel from './components/KeycloakAttemptsPanel';
import PasswordAttemptsPanel from './components/PasswordAttemptsPanel';
import SmsAttemptsPanel from './components/SmsAttemptsPanel';
import { SecurityLogTab } from './components/SecurityLogPanel';

const isValidTab = (value: string | undefined): value is SecurityLogTab =>
  value === 'password' || value === 'keycloak' || value === 'sms';

const SecurityLogsPage = () => {
  const { t } = useTranslation();
  const router = useRouter();

  const { tab: tabQueryParam } = router.query;
  const tabQueryValue = Array.isArray(tabQueryParam)
    ? tabQueryParam[0]
    : tabQueryParam;
  const resolvedTabFromQuery = isValidTab(tabQueryValue) ? tabQueryValue : 'password';

  useEffect(() => {
    if (!router.isReady) {
      return;
    }
  }, [resolvedTabFromQuery, router.isReady]);

  const tabMeta = useMemo(
    () => [
      {
        value: 'password' as SecurityLogTab,
        label: t('Password Attempts'),
        icon: <IconPasswordUser size={16} />,
      },
      {
        value: 'keycloak' as SecurityLogTab,
        label: t('Keycloak Attempts'),
        icon: <IconShieldLock size={16} />,
      },
      {
        value: 'sms' as SecurityLogTab,
        label: t('SMS Attempts'),
        icon: <IconMessage size={16} />,
      },
    ],
    [t],
  );

  const handleTabChange = (value: string) => {
    if (!isValidTab(value)) {
      return;
    }

    if (!router.isReady) {
      return;
    }

    const nextQuery: Record<string, string> = {};

    Object.entries(router.query).forEach(([key, current]) => {
      if (key === 'tab') {
        return;
      }

      if (typeof current === 'string' && current) {
        nextQuery[key] = current;
      }

      if (Array.isArray(current) && current.length > 0) {
        nextQuery[key] = current[0];
      }
    });

    if (value !== 'password') {
      nextQuery.tab = value;
    }

    router.push(
      {
        pathname: router.pathname,
        query: nextQuery,
      },
      undefined,
      { shallow: true },
    );
  };

  return (
    <div className="space-y-4">
      <Tabs
  value={resolvedTabFromQuery}
        onValueChange={handleTabChange}
        orientation="horizontal"
        className="flex-col gap-3 border-none p-0 text-foreground"
      >
        <TabsList className="flex w-full flex-row flex-wrap gap-2 rounded-none bg-transparent p-0">
          {tabMeta.map((tab) => (
            <TabsTrigger
              key={tab.value}
              value={tab.value}
              className="flex items-center gap-2"
            >
              {tab.icon}
              <span>{tab.label}</span>
            </TabsTrigger>
          ))}
        </TabsList>
        {resolvedTabFromQuery === 'password' && (
          <TabsContent value="password" className="ml-0 mt-2">
            <PasswordAttemptsPanel />
          </TabsContent>
        )}
        {resolvedTabFromQuery === 'keycloak' && (
          <TabsContent value="keycloak" className="ml-0 mt-2">
            <KeycloakAttemptsPanel />
          </TabsContent>
        )}
        {resolvedTabFromQuery === 'sms' && (
          <TabsContent value="sms" className="ml-0 mt-2">
            <SmsAttemptsPanel />
          </TabsContent>
        )}
      </Tabs>
    </div>
  );
};

export default SecurityLogsPage;
