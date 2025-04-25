import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { StatisticsTimeParams } from '@/types/adminApis';

import { IconChartHistogram } from '@/components/Icons';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  ChartConfig,
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
} from '@/components/ui/chart';

import { getChatCountStatisticsByDate } from '@/apis/adminApis';
import { Bar, BarChart, CartesianGrid, XAxis } from 'recharts';

type ChatCountStatisticsByDate = {
  date: string;
  count: number;
};

interface ChatCountChartProps {
  timeParams: StatisticsTimeParams;
  updateTrigger?: number;
}

export default function ChatCountChart({
  timeParams,
  updateTrigger = 0,
}: ChatCountChartProps) {
  const { t } = useTranslation();
  const [isLoading, setIsLoading] = useState(false);
  const [chartData, setChartData] = useState<{
    config: ChartConfig;
    data: ChatCountStatisticsByDate[];
  }>({ config: {}, data: [] });

  const loadData = () => {
    setIsLoading(true);
    getChatCountStatisticsByDate(timeParams)
      .then((res) => {
        const data: ChatCountStatisticsByDate[] = [];
        const config: ChartConfig = {};
        res.forEach((item) => {
          data.push({ date: item.date, count: item.value });
        });
        config['views'] = { label: t('Chat Counts') };
        setChartData({
          config: config,
          data: data,
        });
      })
      .catch((err) => {
        console.error('Failed to fetch chat count data:', err);
      })
      .finally(() => {
        setIsLoading(false);
      });
  };

  useEffect(() => {
    loadData();
  }, [updateTrigger]);

  return (
    <Card className="border-none">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium flex gap-1 items-center">
          <IconChartHistogram size={18} /> {t('Chat Counts')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading && chartData.data.length === 0 ? (
          <div className="flex justify-center items-center h-[250px] text-gray-500 text-sm">
            {t('Loading...')}
          </div>
        ) : chartData.data.length > 0 ? (
          <ChartContainer
            config={chartData.config as any}
            className="aspect-auto h-[250px] w-full"
          >
            <BarChart
              accessibilityLayer
              data={chartData.data}
              margin={{
                left: 12,
                right: 12,
              }}
            >
              <CartesianGrid vertical={false} />
              <XAxis
                dataKey="date"
                tickLine={false}
                axisLine={false}
                tickMargin={8}
                minTickGap={32}
              />
              <ChartTooltip
                content={
                  <ChartTooltipContent className="w-[150px]" nameKey="views" />
                }
              />
              <Bar dataKey="count" fill={'hsl(var(--chart-1))'} />
            </BarChart>
          </ChartContainer>
        ) : (
          <div className="flex justify-center items-center h-[250px] text-gray-500 text-sm">
            {t('No data')}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
