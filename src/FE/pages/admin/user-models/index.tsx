import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/router';
import useTranslation from '@/hooks/useTranslation';
import {
  IconUser,
  IconSettingsCog,
} from '@/components/Icons';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/components/ui/tabs';
import ByUserTab from '@/components/admin/user-models/ByUserTab';
import ByModelTab from '@/components/admin/user-models/ByModelTab';

export type UserModelTab = 'by-user' | 'by-model';

const isValidTab = (value: string | undefined): value is UserModelTab =>
  value === 'by-user' || value === 'by-model';

const UserModelsPage = () => {
  const { t } = useTranslation();
  const router = useRouter();

  const { tab: tabQueryParam, userId, modelId } = router.query;
  const tabQueryValue = Array.isArray(tabQueryParam)
    ? tabQueryParam[0]
    : tabQueryParam;
  const resolvedTabFromQuery = isValidTab(tabQueryValue) ? tabQueryValue : 'by-user';

  // 从 URL 中提取参数
  const focusUserId = useMemo(() => {
    if (typeof userId === 'string') return userId;
    if (Array.isArray(userId)) return userId[0];
    return undefined;
  }, [userId]);

  const focusModelId = useMemo(() => {
    if (typeof modelId === 'string') return parseInt(modelId, 10);
    if (Array.isArray(modelId)) return parseInt(modelId[0], 10);
    return undefined;
  }, [modelId]);

  const tabMeta = useMemo(
    () => [
      {
        value: 'by-user' as UserModelTab,
        label: t('By User'),
        icon: <IconUser size={16} />,
      },
      {
        value: 'by-model' as UserModelTab,
        label: t('By Model'),
        icon: <IconSettingsCog size={16} />,
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

    if (value !== 'by-user') {
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
        {/* Segmented control style tabs */}
        <div className="flex w-full justify-center">
          <TabsList
            className="inline-flex flex-row items-center justify-center rounded-full bg-muted p-1 gap-0 shadow-sm border border-border/60"
          >
            {tabMeta.map((tab) => (
              <TabsTrigger
                key={tab.value}
                value={tab.value}
                className="flex items-center gap-2 px-5 py-2 text-sm rounded-full data-[state=active]:bg-background data-[state=active]:text-foreground transition-colors focus-visible:ring-0 focus-visible:ring-offset-0 hover:text-foreground/90 first:rounded-l-full last:rounded-r-full"
              >
                {tab.icon}
                <span>{tab.label}</span>
              </TabsTrigger>
            ))}
          </TabsList>
        </div>

        {resolvedTabFromQuery === 'by-user' && (
          <TabsContent value="by-user" className="ml-0 mt-2">
            <ByUserTab focusUserId={focusUserId} />
          </TabsContent>
        )}
        {resolvedTabFromQuery === 'by-model' && (
          <TabsContent value="by-model" className="ml-0 mt-2">
            <ByModelTab focusModelId={focusModelId} />
          </TabsContent>
        )}
      </Tabs>
    </div>
  );
};

export default UserModelsPage;
