import { useEffect, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { IconArrowDown } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import AccountTab from './_components/tabs/AccountTab';
import ApiKeysTab from './_components/tabs/ApiKeysTab';
import PromptsTab from './_components/tabs/PromptsTab';
import UsageRecordsTab from './_components/tabs/UsageRecordsTab';

const SettingsPage = () => {
  const { t } = useTranslation();
  const router = useRouter();
  const { t: tabParam } = router.query;

  const [activeTab, setActiveTab] = useState('prompts');

  useEffect(() => {
    if (tabParam && typeof tabParam === 'string') {
      setActiveTab(tabParam);
    }
  }, [tabParam]);

  const handleTabChange = (value: string) => {
    setActiveTab(value);
    router.push(`/settings?t=${value}`, undefined, { shallow: true });
  };

  return (
    <div className="container max-w-screen-xl mx-auto py-6 px-4 sm:px-6 h-screen">
      <h1 className="text-2xl font-bold mb-6 flex items-center gap-2">
        <Button variant="ghost" size="icon" onClick={() => router.push('/')}>
          <IconArrowDown className="rotate-90" size={20} />
        </Button>{' '}
        {t('Settings')}
      </h1>

      <Tabs
        value={activeTab}
        onValueChange={handleTabChange}
        className="flex flex-col w-full"
      >
        <TabsList className="flex w-full h-auto flex-row justify-start px-2 py-2 rounded-md bg-card overflow-x-auto">
          <TabsTrigger
            value="prompts"
            className="flex-1 justify-center sm:flex-none"
          >
            {t('Prompt Management')}
          </TabsTrigger>
          <TabsTrigger
            value="api-keys"
            className="flex-1 justify-center sm:flex-none"
          >
            {t('API Key Management')}
          </TabsTrigger>
          <TabsTrigger
            value="usage"
            className="flex-1 justify-center sm:flex-none"
          >
            {t('Usage Records')}
          </TabsTrigger>
          <TabsTrigger
            value="account"
            className="flex-1 justify-center sm:flex-none"
          >
            {t('Account Management')}
          </TabsTrigger>
        </TabsList>
        <div className="flex-1 mt-4 overflow-auto"> 
          <TabsContent value="prompts" className="m-0 h-full">
            <PromptsTab />
          </TabsContent>
          <TabsContent value="api-keys" className="m-0 h-full">
            <ApiKeysTab />
          </TabsContent>
          <TabsContent value="usage" className="m-0 h-full">
            <UsageRecordsTab />
          </TabsContent>
          <TabsContent value="account" className="m-0 h-full">
            <AccountTab />
          </TabsContent>
        </div>
      </Tabs>
    </div>
  );
};

export default SettingsPage;
