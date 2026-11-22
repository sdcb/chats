import { useMemo } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import {
  IconDesktop,
  IconKey,
  IconNotes,
  IconWorld,
} from '@/components/Icons';
import DnsProvidersTab from '@/components/admin/websites/DnsProvidersTab';
import DomainsTab from '@/components/admin/websites/DomainsTab';
import GatewaysTab from '@/components/admin/websites/GatewaysTab';
import PublishedSitesTab from '@/components/admin/websites/PublishedSitesTab';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

const TABS = ['sites', 'domains', 'gateways', 'dns'] as const;
type WebsiteTab = (typeof TABS)[number];

const TRIGGER_CLASS =
  'flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-colors hover:text-foreground/90 focus-visible:ring-0 focus-visible:ring-offset-0 data-[state=active]:bg-background data-[state=active]:text-foreground';

export default function AdminWebsitesPage() {
  const { t } = useTranslation();
  const router = useRouter();
  const activeTab = useMemo<WebsiteTab>(() => {
    const tab = router.query.tab;
    return typeof tab === 'string' && TABS.includes(tab as WebsiteTab)
      ? (tab as WebsiteTab)
      : 'sites';
  }, [router.query.tab]);

  const handleTabChange = (value: string) => {
    router.replace(
      { pathname: router.pathname, query: { tab: value } },
      undefined,
      { shallow: true },
    );
  };

  return (
    <main className="w-full space-y-5 p-4 sm:p-6">
      <Tabs
        value={activeTab}
        onValueChange={handleTabChange}
        className="flex-col gap-4 border-none p-0 text-foreground"
      >
        <div className="flex w-full justify-center overflow-x-auto">
          <TabsList className="inline-flex h-auto flex-row items-center justify-center gap-0 rounded-full border border-border/60 bg-muted p-1 shadow-sm">
            <TabsTrigger value="sites" className={TRIGGER_CLASS}>
              <IconWorld size={16} />
              <span>{t('Published websites')}</span>
            </TabsTrigger>
            <TabsTrigger value="domains" className={TRIGGER_CLASS}>
              <IconNotes size={16} />
              <span>{t('Domains')}</span>
            </TabsTrigger>
            <TabsTrigger value="gateways" className={TRIGGER_CLASS}>
              <IconDesktop size={16} />
              <span>{t('Gateways')}</span>
            </TabsTrigger>
            <TabsTrigger value="dns" className={TRIGGER_CLASS}>
              <IconKey size={16} />
              <span>{t('DNS providers')}</span>
            </TabsTrigger>
          </TabsList>
        </div>
        <TabsContent value="sites">
          {activeTab === 'sites' && <PublishedSitesTab />}
        </TabsContent>
        <TabsContent value="domains">
          {activeTab === 'domains' && <DomainsTab />}
        </TabsContent>
        <TabsContent value="gateways">
          {activeTab === 'gateways' && <GatewaysTab />}
        </TabsContent>
        <TabsContent value="dns">
          {activeTab === 'dns' && <DnsProvidersTab />}
        </TabsContent>
      </Tabs>
    </main>
  );
}
