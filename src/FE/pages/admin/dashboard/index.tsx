import React, { useEffect, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { getTz } from '@/utils/date';

import { StatisticsTimeParams } from '@/types/adminApis';

import {
  ChatCountChart,
  CostConsumptionChart,
  DateSelector,
  PieChartCard,
  StatsCards,
  TokenConsumptionChart,
} from './charts';

import {
  getModelKeyStatistics,
  getModelProviderStatistics,
  getModelStatistics,
  getSourceStatistics,
} from '@/apis/adminApis';

export default function Dashboard() {
  const { t } = useTranslation();
  const router = useRouter();
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [timeParams, setTimeParams] = useState<StatisticsTimeParams>({
    tz: getTz(),
  });

  useEffect(() => {
    setStartDate(router.query.start as string);
    setEndDate(router.query.end as string);
    if (router.isReady) {
      setTimeParams({
        start: router.query.start as string,
        end: router.query.end as string,
        tz: getTz(),
      });
    }
  }, [router.isReady, router.query]);

  return (
    <>
      <DateSelector
        startDate={startDate}
        endDate={endDate}
        setStartDate={setStartDate}
        setEndDate={setEndDate}
      />

      <StatsCards timeParams={timeParams} />

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 p-4">
        <PieChartCard
          title={t('Model Provider Usage Counts')}
          timeParams={timeParams}
          dataFetcher={getModelProviderStatistics}
        />
        <PieChartCard
          title={t('Model Usage Counts')}
          timeParams={timeParams}
          dataFetcher={getModelStatistics}
        />
        <PieChartCard
          title={t('Model Key Usage Counts')}
          timeParams={timeParams}
          dataFetcher={getModelKeyStatistics}
        />
        <PieChartCard
          title={t('Source Usage Counts')}
          timeParams={timeParams}
          dataFetcher={getSourceStatistics}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 p-4">
        <TokenConsumptionChart timeParams={timeParams} />
        <CostConsumptionChart timeParams={timeParams} />
      </div>

      <div className="grid grid-cols-1 gap-4 p-4">
        <ChatCountChart timeParams={timeParams} />
      </div>
    </>
  );
}
