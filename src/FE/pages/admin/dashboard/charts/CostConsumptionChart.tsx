import React, { useEffect, useState } from 'react';
import useTranslation from '@/hooks/useTranslation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { IconChartHistogram } from '@/components/Icons';
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  ChartConfig,
} from '@/components/ui/chart';
import {
  Area,
  AreaChart,
  CartesianGrid,
  XAxis,
} from 'recharts';
import { StatisticsTimeParams } from '@/types/adminApis';
import { getCostStatisticsByDate } from '@/apis/adminApis';

type CostStatisticsByDate = {
  date: string;
  inputCost: number;
  outputCost: number;
};

interface CostConsumptionChartProps {
  timeParams: StatisticsTimeParams;
  updateTrigger?: number;
}

export default function CostConsumptionChart({
  timeParams,
  updateTrigger = 0,
}: CostConsumptionChartProps) {
  const { t } = useTranslation();
  const [isLoading, setIsLoading] = useState(false);
  const [chartData, setChartData] = useState<{
    config: ChartConfig;
    data: CostStatisticsByDate[];
  }>({ config: {}, data: [] });

  const loadData = () => {
    setIsLoading(true);
    getCostStatisticsByDate(timeParams)
      .then((res) => {
        const data: CostStatisticsByDate[] = [];
        const config: ChartConfig = {};
        res.forEach((item) => {
          data.push({ date: item.date, ...item.value });
        });
        config['inputCost'] = { label: t('Input Cost') };
        config['outputCost'] = { label: t('Output Cost') };
        setChartData({
          config: config,
          data: data,
        });
      })
      .catch((err) => {
        console.error('Failed to fetch cost data:', err);
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
          <IconChartHistogram size={18} /> {t('Consumption Cost')}
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
            <AreaChart data={chartData.data}>
              <defs>
                <linearGradient
                  id="fillInputCost"
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
                    offset="95%"
                    stopColor="hsl(var(--chart-2))"
                    stopOpacity={0.1}
                  />
                </linearGradient>
                <linearGradient
                  id="fillOutputCost"
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
                    offset="95%"
                    stopColor="hsl(var(--chart-2))"
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
                dataKey="inputCost"
                type="linear"
                fill="url(#fillInputCost)"
                stroke="hsl(var(--chart-1))"
                stackId="a"
              />
              <Area
                dataKey="outputCost"
                type="linear"
                fill="url(#fillOutputCost)"
                stroke="hsl(var(--chart-2))"
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