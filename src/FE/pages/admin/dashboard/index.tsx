import React, { useCallback, useEffect, useMemo, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { formatDate, getTz } from '@/utils/date';

import { StatisticsTimeParams } from '@/types/adminApis';

import ChatCountChart from '@/components/admin/dashboard/charts/ChatCountChart';
import CostConsumptionChart from '@/components/admin/dashboard/charts/CostConsumptionChart';
import DateSelector from '@/components/admin/dashboard/charts/DateSelector';
import PieChartCard from '@/components/admin/dashboard/charts/PieChartCard';
import StatsCards from '@/components/admin/dashboard/charts/StatsCards';
import TokenConsumptionChart from '@/components/admin/dashboard/charts/TokenConsumptionChart';

import {
  getModelKeyStatistics,
  getModelProviderStatistics,
  getModelStatistics,
  getSourceStatistics,
} from '@/apis/adminApis';
import {
  endOfToday,
  endOfWeek,
  endOfMonth,
  startOfToday,
  startOfWeek,
  startOfMonth,
  subDays,
} from 'date-fns';

import { Button } from '@/components/ui/button';

export default function Dashboard() {
  const { t } = useTranslation();
  const router = useRouter();
  const [triggerUpdate, setTriggerUpdate] = useState(0);

  const defaultStart = useMemo(
    () => formatDate(subDays(endOfToday(), 7)),
    [],
  );
  const defaultEnd = useMemo(() => formatDate(endOfToday()), []);

  const startDate = useMemo(() => {
    const queryValue = router.query.start;
    return typeof queryValue === 'string' && queryValue
      ? queryValue
      : defaultStart;
  }, [router.query.start, defaultStart]);

  const endDate = useMemo(() => {
    const queryValue = router.query.end;
    return typeof queryValue === 'string' && queryValue ? queryValue : defaultEnd;
  }, [router.query.end, defaultEnd]);

  const timeParams = useMemo<StatisticsTimeParams>(
    () => ({
      start: startDate,
      end: endDate,
      tz: getTz(),
    }),
    [startDate, endDate],
  );

  useEffect(() => {
    if (!router.isReady) return;
    setTriggerUpdate((prev) => prev + 1);
  }, [router.isReady, startDate, endDate]);

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
    },
    [router],
  );

  const setQuickRange = useCallback(
    (range: 'today' | 'yesterday' | 'week' | 'month') => {
      const today = endOfToday();
      if (range === 'today') {
        const date = formatDate(startOfToday());
        handleDateChange(date, date);
        return;
      }

      if (range === 'yesterday') {
        const date = formatDate(subDays(today, 1));
        handleDateChange(date, date);
        return;
      }

      if (range === 'week') {
        handleDateChange(
          formatDate(startOfWeek(today, { weekStartsOn: 1 })),
          formatDate(endOfWeek(today, { weekStartsOn: 1 })),
        );
        return;
      }

      handleDateChange(formatDate(startOfMonth(today)), formatDate(endOfMonth(today)));
    },
    [handleDateChange],
  );

  return (
    <>
      <DateSelector
        startDate={startDate}
        endDate={endDate}
        onDateChange={handleDateChange}
        rightSlot={
          <div className="flex items-center gap-2">
            <Button
              size="xs"
              variant="outline"
              onClick={() => setQuickRange('today')}
            >
              {t('Today')}
            </Button>
            <Button
              size="xs"
              variant="outline"
              onClick={() => setQuickRange('yesterday')}
            >
              {t('Yesterday')}
            </Button>
            <Button
              size="xs"
              variant="outline"
              onClick={() => setQuickRange('week')}
            >
              {t('This week')}
            </Button>
            <Button
              size="xs"
              variant="outline"
              onClick={() => setQuickRange('month')}
            >
              {t('This month')}
            </Button>
          </div>
        }
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
