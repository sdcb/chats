import { useEffect, useState } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import {
  IconArrowDown,
  IconBulb,
  IconKey,
  IconMoneybag,
  IconSettings,
  IconUser,
  IconRobot,
} from '@/components/Icons';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import AccountTab from '@/components/settings/tabs/AccountTab';
import ApiKeysTab from '@/components/settings/tabs/ApiKeysTab';
import GeneralTab from '@/components/settings/tabs/GeneralTab';
import PromptsTab from '@/components/settings/tabs/PromptsTab';
import McpTab from '@/components/settings/tabs/McpTab';
import UsageRecordsTab from '@/components/settings/tabs/UsageRecordsTab';

const SettingsPage = () => {
  const { t } = useTranslation();
  const router = useRouter();
  const { t: tabParam, tab } = router.query;

  const [activeTab, setActiveTab] = useState('general');

  useEffect(() => {
    if (tabParam && typeof tabParam === 'string') {
      setActiveTab(tabParam);
    } else if (tab && typeof tab === 'string') {
      setActiveTab(tab);
    }
  }, [tabParam, tab]);

  const handleTabChange = (value: string) => {
    setActiveTab(value);
    router.push(`/settings?t=${value}`, undefined, { shallow: true });
  };

  return (
    <div className="container max-w-screen-xl mx-auto py-6 px-4 sm:px-6 h-screen">
      <h1 className="text-2xl font-bold mb-6 flex items-center gap-2">
        <Link
          href="/"
          onClick={(e) => {
            e.preventDefault();
            router.back();
          }}
          className="inline-flex items-center justify-center whitespace-nowrap rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 hover:bg-accent hover:text-accent-foreground h-10 w-10"
        >
          <IconArrowDown className="rotate-90" size={20} />
        </Link>
        {t('Settings')}
      </h1>

      <Tabs
        value={activeTab}
        onValueChange={handleTabChange}
        className="flex flex-col w-full"
      >
        <TabsList className="flex w-full h-auto flex-row justify-start px-2 py-2 rounded-md bg-card overflow-x-auto">
          <TabsTrigger
            value="general"
            className="flex-1 justify-center items-center sm:flex-none gap-1"
          >
            <IconSettings /> {t('General')}
          </TabsTrigger>
          <TabsTrigger
            value="prompts"
            className="flex-1 justify-center items-center sm:flex-none gap-1"
          >
            <IconBulb /> {t('Prompts')}
          </TabsTrigger>
          <TabsTrigger
            value="mcp"
            className="flex-1 justify-center items-center sm:flex-none gap-1"
          >
            <IconRobot /> {t('MCP')}
          </TabsTrigger>
          <TabsTrigger
            value="api-keys"
            className="flex-1 items-center sm:flex-none gap-1"
          >
            <IconKey size={18} />
            {t('API Key')}
          </TabsTrigger>
          <TabsTrigger
            value="usage"
            className="flex-1 items-center sm:flex-none gap-1"
          >
            <IconMoneybag />
            {t('Usage Records')}
          </TabsTrigger>
          <TabsTrigger
            value="account"
            className="flex-1 items-center sm:flex-none gap-1"
          >
            <IconUser />
            {t('Password')}
          </TabsTrigger>
        </TabsList>
        <div className="flex-1 mt-4 overflow-auto">
          <TabsContent value="general" className="m-0 h-full">
            <GeneralTab />
          </TabsContent>
          <TabsContent value="prompts" className="m-0 h-full">
            <PromptsTab />
          </TabsContent>
          <TabsContent value="mcp" className="m-0 h-full">
            <McpTab />
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
