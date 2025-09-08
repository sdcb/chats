import React, { useCallback, useEffect, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { formatDate, getTz } from '@/utils/date';

import { StatisticsTimeParams } from '@/types/adminApis';

import ChatCountChart from './charts/ChatCountChart';
import CostConsumptionChart from './charts/CostConsumptionChart';
import DateSelector from './charts/DateSelector';
import PieChartCard from './charts/PieChartCard';
import StatsCards from './charts/StatsCards';
import TokenConsumptionChart from './charts/TokenConsumptionChart';

import {
  getModelKeyStatistics,
  getModelProviderStatistics,
  getModelStatistics,
  getSourceStatistics,
} from '@/apis/adminApis';
import { endOfToday, subDays, subMonths } from 'date-fns';

export default function Dashboard() {
  const { t } = useTranslation();
  const router = useRouter();
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [timeParams, setTimeParams] = useState<StatisticsTimeParams>({
    start: formatDate(subDays(endOfToday(), 7)),
    end: formatDate(endOfToday()),
    tz: getTz(),
  });
  const [triggerUpdate, setTriggerUpdate] = useState(0);

  useEffect(() => {
    if (router.isReady) {
      const start =
        (router.query.start as string) ||
        formatDate(subDays(endOfToday(), 7));
      const end = (router.query.end as string) || formatDate(endOfToday());
      setStartDate(start);
      setEndDate(end);

      setTimeParams({
        start,
        end,
        tz: getTz(),
      });
    }
  }, [router.isReady]);

  const handleDateChange = useCallback(
    (newStartDate: string, newEndDate: string) => {
      const query: Record<string, string> = {
        ...(router.query as Record<string, string>),
      };
      if (newStartDate) {
        query.start = newStartDate;
      } else {
        delete query.start;
      }

      if (newEndDate) {
        query.end = newEndDate;
      } else {
        delete query.end;
      }

      router.push(
        {
          pathname: router.pathname,
          query,
        },
        undefined,
        { shallow: true },
      );

      setTimeParams({
        start: newStartDate,
        end: newEndDate,
        tz: getTz(),
      });

      setTriggerUpdate((prev) => prev + 1);
    },
    [router],
  );

  return (
    <>
      <DateSelector
        startDate={startDate}
        endDate={endDate}
        setStartDate={setStartDate}
        setEndDate={setEndDate}
        onDateChange={handleDateChange}
      />

      <div className='pm-2'>
        <StatsCards timeParams={timeParams} updateTrigger={triggerUpdate} />
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 pt-4 py-2">
        <PieChartCard
          title={t('Model Provider Usage Counts')}
          timeParams={timeParams}
          dataFetcher={getModelProviderStatistics}
          updateTrigger={triggerUpdate}
        />
        <PieChartCard
          title={t('Model Usage Counts')}
          timeParams={timeParams}
          dataFetcher={getModelStatistics}
          updateTrigger={triggerUpdate}
        />
        <PieChartCard
          title={t('Model Key Usage Counts')}
          timeParams={timeParams}
          dataFetcher={getModelKeyStatistics}
          updateTrigger={triggerUpdate}
        />
        <PieChartCard
          title={t('Source Usage Counts')}
          timeParams={timeParams}
          dataFetcher={getSourceStatistics}
          updateTrigger={triggerUpdate}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 py-2">
        <TokenConsumptionChart
          timeParams={timeParams}
          updateTrigger={triggerUpdate}
        />
        <CostConsumptionChart
          timeParams={timeParams}
          updateTrigger={triggerUpdate}
        />
      </div>

      <div className="grid grid-cols-1 gap-4 py-2">
        <ChatCountChart timeParams={timeParams} updateTrigger={triggerUpdate} />
      </div>
    </>
  );
}
