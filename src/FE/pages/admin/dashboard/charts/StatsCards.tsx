import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { formatNumberAsMoney } from '@/utils/common';

import { StatisticsTimeParams } from '@/types/adminApis';

import {
  IconMoneybag,
  IconSettingsCog,
  IconTokens,
  IconUser,
} from '@/components/Icons';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

import {
  getCostDuring,
  getEnabledModelCount,
  getEnabledUserCount,
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
  const [userCount, setUserCount] = useState(0);
  const [modelCount, setModelCount] = useState(0);
  const [tokensDuring, setTokensDuring] = useState(0);
  const [costDuring, setCostDuring] = useState(0);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    Promise.all([
      getEnabledUserCount().then(res => setUserCount(res)),
      getEnabledModelCount().then(res => setModelCount(res))
    ]).catch(err => {
      console.error('Failed to fetch stats data:', err);
    });
  }, []);

  const loadTimeBasedData = () => {
    setIsLoading(true);
    Promise.all([
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
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 p-4">
      <Card className="border-none">
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">
            {t('User Count')}
          </CardTitle>
          <IconUser />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{userCount}</div>
        </CardContent>
      </Card>
      <Card className="border-none">
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">
            {t('Model Count')}
          </CardTitle>
          <IconSettingsCog />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{modelCount}</div>
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
