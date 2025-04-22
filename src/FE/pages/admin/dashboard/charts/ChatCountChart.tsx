import React, { useEffect, useState } from 'react';
import useTranslation from '@/hooks/useTranslation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { IconChartHistogram } from '@/components/Icons';
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  ChartConfig,
} from '@/components/ui/chart';
import {
  Bar,
  BarChart,
  CartesianGrid,
  XAxis,
} from 'recharts';
import { StatisticsTimeParams } from '@/types/adminApis';
import { getChatCountStatisticsByDate } from '@/apis/adminApis';

type ChatCountStatisticsByDate = {
  date: string;
  count: number;
};

interface ChatCountChartProps {
  timeParams: StatisticsTimeParams;
}

export default function ChatCountChart({
  timeParams,
}: ChatCountChartProps) {
  const { t } = useTranslation();
  const [chartData, setChartData] = useState<{
    config: ChartConfig;
    data: ChatCountStatisticsByDate[];
  }>({ config: {}, data: [] });

  useEffect(() => {
    getChatCountStatisticsByDate(timeParams).then((res) => {
      const data: ChatCountStatisticsByDate[] = [];
      const config: ChartConfig = {};
      res.forEach((item) => {
        data.push({ date: item.date, count: item.value });
      });
      config['count'] = { label: 'count' };
      setChartData({
        config: config,
        data: data,
      });
    });
  }, [timeParams]);

  return (
    <Card className="border-none">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium flex gap-1 items-center">
          <IconChartHistogram size={18} /> {t('Chat Counts')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        {chartData.data.length > 0 ? (
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
                tickFormatter={(value) => {
                  const date = new Date(value);
                  return date.toLocaleDateString('en-US', {
                    month: 'short',
                    day: 'numeric',
                  });
                }}
              />
              <ChartTooltip
                content={
                  <ChartTooltipContent
                    className="w-[150px]"
                    nameKey="views"
                    labelFormatter={(value) => {
                      return new Date(value).toLocaleDateString('en-US', {
                        month: 'short',
                        day: 'numeric',
                        year: 'numeric',
                      });
                    }}
                  />
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