import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatNumberAsMoney } from '@/utils/common';

import { StatisticsTimeParams } from '@/types/adminApis';

import {
  IconMessages,
  IconMoneybag,
  IconTokens,
  IconUser,
} from '@/components/Icons';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

import {
  getActiveUserCountDuring,
  getChatCountDuring,
  getCostDuring,
  getTokensDuring,
} from '@/apis/adminApis';

interface StatsCardsProps {
  timeParams: StatisticsTimeParams;
  updateTrigger?: number;
}

export default function StatsCards({ 
  timeParams,
  updateTrigger = 0
}: StatsCardsProps) {
  const { t } = useTranslation();
  const [activeUserCount, setActiveUserCount] = useState(0);
  const [chatCount, setChatCount] = useState(0);
  const [tokensDuring, setTokensDuring] = useState(0);
  const [costDuring, setCostDuring] = useState(0);
  const [isLoading, setIsLoading] = useState(false);

  const loadTimeBasedData = () => {
    setIsLoading(true);
    Promise.all([
      getActiveUserCountDuring(timeParams).then(res => setActiveUserCount(res)),
      getChatCountDuring(timeParams).then(res => setChatCount(res)),
      getTokensDuring(timeParams).then(res => setTokensDuring(res)),
      getCostDuring(timeParams).then(res => setCostDuring(res))
    ])
    .catch(err => {
      console.error('Failed to fetch time-based stats:', err);
    })
    .finally(() => {
      setIsLoading(false);
    });
  };

  useEffect(() => {
    loadTimeBasedData();
  }, [updateTrigger]);

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <Card className="border-none">
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">
            {t('Active User Count')}
          </CardTitle>
          <IconUser />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">
            {isLoading ? t('Loading...') : activeUserCount}
          </div>
        </CardContent>
      </Card>
      <Card className="border-none">
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">
            {t('Chat Counts')}
          </CardTitle>
          <IconMessages />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">
            {isLoading ? t('Loading...') : chatCount}
          </div>
        </CardContent>
      </Card>
      <Card className="border-none">
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">
            {t('Consumption Tokens')}
          </CardTitle>
          <IconTokens />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">
            {isLoading ? t('Loading...') : formatNumberAsMoney(tokensDuring, 2)}
          </div>
        </CardContent>
      </Card>
      <Card className="border-none">
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">
            {t('Consumption Cost')}
          </CardTitle>
          <IconMoneybag />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">
            {isLoading ? t('Loading...') : formatNumberAsMoney(costDuring, 2)}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
