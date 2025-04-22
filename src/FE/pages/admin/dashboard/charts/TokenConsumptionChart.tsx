import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { StatisticsTimeParams } from '@/types/adminApis';

import { IconChartHistogram } from '@/components/Icons';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  ChartConfig,
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
} from '@/components/ui/chart';

import { getTokenStatisticsByDate } from '@/apis/adminApis';
import { Area, AreaChart, CartesianGrid, XAxis } from 'recharts';

type TokenStatisticsByDate = {
  date: string;
  inputTokens: number;
  outputTokens: number;
  reasoningTokens: number;
  totalTokens: number;
};

interface TokenConsumptionChartProps {
  timeParams: StatisticsTimeParams;
}

export default function TokenConsumptionChart({
  timeParams,
}: TokenConsumptionChartProps) {
  const { t } = useTranslation();
  const [chartData, setChartData] = useState<{
    config: ChartConfig;
    data: TokenStatisticsByDate[];
  }>({ config: {}, data: [] });

  useEffect(() => {
    getTokenStatisticsByDate(timeParams).then((res) => {
      const data: TokenStatisticsByDate[] = [];
      const config: ChartConfig = {};
      res.forEach((item) => {
        data.push({ date: item.date, ...item.value });
      });
      config['inputTokens'] = { label: 'inputTokens' };
      config['outputTokens'] = { label: 'outputTokens' };
      config['reasoningTokens'] = { label: 'reasoningTokens' };
      config['totalTokens'] = { label: 'totalTokens' };
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
          <IconChartHistogram size={18} />
          {t('Consumption Tokens')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        {chartData.data.length > 0 ? (
          <ChartContainer
            config={chartData.config as any}
            className="aspect-auto h-[250px] w-full"
          >
            <AreaChart data={chartData.data}>
              <defs>
                <linearGradient
                  id="fillInputTokens"
                  x1="0"
                  y1="0"
                  x2="0"
                  y2="1"
                >
                  <stop
                    offset="5%"
                    stopColor="hsl(var(--chart-1))"
                    stopOpacity={0.8}
                  />
                  <stop
                    offset="25%"
                    stopColor="hsl(var(--chart-2))"
                    stopOpacity={0.6}
                  />
                  <stop
                    offset="55%"
                    stopColor="hsl(var(--chart-3))"
                    stopOpacity={0.4}
                  />
                  <stop
                    offset="95%"
                    stopColor="hsl(var(--chart-4))"
                    stopOpacity={0.1}
                  />
                </linearGradient>
                <linearGradient
                  id="fillOutputTokens"
                  x1="0"
                  y1="0"
                  x2="0"
                  y2="1"
                >
                  <stop
                    offset="5%"
                    stopColor="hsl(var(--chart-1))"
                    stopOpacity={0.8}
                  />
                  <stop
                    offset="25%"
                    stopColor="hsl(var(--chart-2))"
                    stopOpacity={0.6}
                  />
                  <stop
                    offset="55%"
                    stopColor="hsl(var(--chart-3))"
                    stopOpacity={0.4}
                  />
                  <stop
                    offset="95%"
                    stopColor="hsl(var(--chart-4))"
                    stopOpacity={0.1}
                  />
                </linearGradient>
                <linearGradient
                  id="fillReasoningTokens"
                  x1="0"
                  y1="0"
                  x2="0"
                  y2="1"
                >
                  <stop
                    offset="5%"
                    stopColor="hsl(var(--chart-1))"
                    stopOpacity={0.8}
                  />
                  <stop
                    offset="25%"
                    stopColor="hsl(var(--chart-2))"
                    stopOpacity={0.6}
                  />
                  <stop
                    offset="55%"
                    stopColor="hsl(var(--chart-3))"
                    stopOpacity={0.4}
                  />
                  <stop
                    offset="95%"
                    stopColor="hsl(var(--chart-4))"
                    stopOpacity={0.1}
                  />
                </linearGradient>
                <linearGradient
                  id="fillTotalTokens"
                  x1="0"
                  y1="0"
                  x2="0"
                  y2="1"
                >
                  <stop
                    offset="5%"
                    stopColor="hsl(var(--chart-1))"
                    stopOpacity={0.8}
                  />
                  <stop
                    offset="25%"
                    stopColor="hsl(var(--chart-2))"
                    stopOpacity={0.6}
                  />
                  <stop
                    offset="55%"
                    stopColor="hsl(var(--chart-3))"
                    stopOpacity={0.4}
                  />
                  <stop
                    offset="95%"
                    stopColor="hsl(var(--chart-4))"
                    stopOpacity={0.1}
                  />
                </linearGradient>
              </defs>
              <CartesianGrid vertical={false} />
              <XAxis
                dataKey="date"
                tickLine={false}
                axisLine={false}
                tickMargin={8}
                minTickGap={32}
              />
              <ChartTooltip
                cursor={false}
                content={<ChartTooltipContent indicator="dot" />}
              />
              <Area
                dataKey="inputTokens"
                type="linear"
                fill="url(#fillInputTokens)"
                stroke="hsl(var(--chart-1))"
                stackId="a"
              />
              <Area
                dataKey="outputTokens"
                type="linear"
                fill="url(#fillOutputTokens)"
                stroke="hsl(var(--chart-2))"
                stackId="a"
              />
              <Area
                dataKey="reasoningTokens"
                type="linear"
                fill="url(#fillReasoningTokens)"
                stroke="hsl(var(--chart-3))"
                stackId="a"
              />
              <Area
                dataKey="totalTokens"
                type="linear"
                fill="url(#fillTotalTokens)"
                stroke="hsl(var(--chart-4))"
                stackId="a"
              />
              <ChartLegend content={<ChartLegendContent />} />
            </AreaChart>
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
