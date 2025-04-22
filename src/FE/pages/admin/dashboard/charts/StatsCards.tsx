import React, { useEffect, useState } from 'react';
import { formatNumberAsMoney } from '@/utils/common';
import {
  IconChartHistogram,
  IconChartPie,
  IconMoneybag,
  IconSettingsCog,
  IconTokens,
  IconUser,
} from '@/components/Icons';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import useTranslation from '@/hooks/useTranslation';
import { StatisticsTimeParams } from '@/types/adminApis';
import { getCostDuring, getEnabledModelCount, getEnabledUserCount, getTokensDuring } from '@/apis/adminApis';

interface StatsCardsProps {
  timeParams: StatisticsTimeParams;
}

export default function StatsCards({ timeParams }: StatsCardsProps) {
  const { t } = useTranslation();
  const [userCount, setUserCount] = useState(0);
  const [modelCount, setModelCount] = useState(0);
  const [tokensDuring, setTokensDuring] = useState(0);
  const [costDuring, setCostDuring] = useState(0);

  useEffect(() => {
    getEnabledUserCount().then((res) => {
      setUserCount(res);
    });
    getEnabledModelCount().then((res) => {
      setModelCount(res);
    });
  }, []);

  useEffect(() => {
    getTokensDuring(timeParams).then((res) => {
      setTokensDuring(res);
    });
    getCostDuring(timeParams).then((res) => {
      setCostDuring(res);
    });
  }, [timeParams]);

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
            {formatNumberAsMoney(tokensDuring, 2)}
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
            {formatNumberAsMoney(costDuring, 2)}
          </div>
        </CardContent>
      </Card>
    </div>
  );
} 