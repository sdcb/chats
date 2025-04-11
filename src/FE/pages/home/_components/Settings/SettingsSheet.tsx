import { useContext, useEffect, useState } from 'react';

import { Sheet, SheetContent } from '@/components/ui/sheet';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import useTranslation from '@/hooks/useTranslation';

import { setShowSetting } from '../../_actions/setting.actions';
import HomeContext from '../../_contexts/home.context';
import ApiKeysTab from './tabs/ApiKeysTab';
import PromptsTab from './tabs/PromptsTab';
import UsageRecordsTab from './tabs/UsageRecordsTab';
import AccountTab from './tabs/AccountTab';

type Props = {
  isOpen: boolean;
  defaultTab?: string;
};
const SettingsSheet = (props: Props) => {
  const { isOpen, defaultTab = 'prompts' } = props;
  const { t } = useTranslation();
  const { settingDispatch } = useContext(HomeContext);
  const [activeTab, setActiveTab] = useState(defaultTab);

  useEffect(() => {
    if (defaultTab) {
      setActiveTab(defaultTab);
    }
  }, [defaultTab]);

  return (
    <Sheet
      open={isOpen}
      onOpenChange={() => {
        settingDispatch(setShowSetting(false));
      }}
    >
      <SheetContent className="max-w-full sm:max-w-full w-full p-0 sm:p-2">
        <div className="h-full flex flex-col">
          <Tabs value={activeTab} onValueChange={setActiveTab} className="flex flex-col h-full w-full">
            <TabsList className="flex w-full h-auto flex-row justify-start px-2 py-2 rounded-md">
              <TabsTrigger value="prompts" className="flex-1 justify-center sm:flex-none">{t('Prompt Management')}</TabsTrigger>
              <TabsTrigger value="api-keys" className="flex-1 justify-center sm:flex-none">{t('API Key Management')}</TabsTrigger>
              <TabsTrigger value="usage" className="flex-1 justify-center sm:flex-none">{t('Usage Records')}</TabsTrigger>
              <TabsTrigger value="account" className="flex-1 justify-center sm:flex-none">{t('Account')}</TabsTrigger>
            </TabsList>
            <div className="flex-1 p-2 sm:p-4 overflow-auto">
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
      </SheetContent>
    </Sheet>
  );
};

export default SettingsSheet;
