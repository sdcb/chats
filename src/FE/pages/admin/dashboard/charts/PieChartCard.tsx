import React, { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { StatisticsTimeParams } from '@/types/adminApis';
import { IKeyValue } from '@/types/common';

import { IconChartPie } from '@/components/Icons';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
} from '@/components/ui/chart';
import { ChartConfig } from '@/components/ui/chart';

import { Cell, LabelList, Pie, PieChart } from 'recharts';

interface PieChartCardProps {
  title: string;
  timeParams: StatisticsTimeParams;
  dataFetcher: (params: StatisticsTimeParams) => Promise<any>;
  formatData?: (
    data: any,
    t: (key: string) => string,
  ) => { config: ChartConfig; data: IKeyValue[] };
}

export default function PieChartCard({
  title,
  timeParams,
  dataFetcher,
  formatData = defaultFormatData,
}: PieChartCardProps) {
  const { t } = useTranslation();
  const [chartData, setChartData] = useState<{
    config: ChartConfig;
    data: IKeyValue[];
  }>({ config: {}, data: [] });

  useEffect(() => {
    dataFetcher(timeParams).then((res) => {
      const formattedData = formatData(res, t);
      setChartData(formattedData);
    });
  }, [timeParams, dataFetcher, formatData, t]);

  const COLORS = [
    'hsl(var(--chart-1))',
    'hsl(var(--chart-2))',
    'hsl(var(--chart-3))',
    'hsl(var(--chart-4))',
    'hsl(var(--chart-5))',
  ];

  return (
    <Card className="border-none">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium flex gap-1 items-center">
          <IconChartPie size={18} />
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent>
        {chartData.data.length > 0 ? (
          <ChartContainer
            config={chartData.config as any}
            className="mx-auto aspect-square max-h-[320px] [&_.recharts-text]:fill-background"
          >
            <PieChart>
              <ChartTooltip
                cursor={false}
                content={<ChartTooltipContent hideLabel />}
              />
              <Pie data={chartData.data} dataKey="value" nameKey="key">
                {chartData.data.map((_, index) => (
                  <Cell
                    key={`cell-${index}`}
                    fill={COLORS[index % COLORS.length]}
                  />
                ))}
              </Pie>
            </PieChart>
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

// 默认数据格式化函数
function defaultFormatData(data: any, t: (key: string) => string) {
  const result: IKeyValue[] = [];
  const config: ChartConfig = {};

  if (Array.isArray(data)) {
    // 处理数组类型的数据
    data.forEach((item) => {
      result.push({ key: item.key, value: item.count });
      config[item.key] = { label: item.key };
    });
  } else {
    // 处理对象类型的数据
    Object.entries(data).forEach(([key, value]) => {
      result.push({ key, value: value as number });
      config[key] = { label: key };
    });
  }

  const sortedData = result.sort((a, b) => b.value - a.value);
  const topFourData = sortedData.slice(0, 4);
  const otherData = sortedData.slice(4);
  const topFiveConfig = topFourData.reduce((acc, curr) => {
    acc[curr.key] = { label: curr.key };
    return acc;
  }, {} as ChartConfig);

  if (otherData.length > 0) {
    topFiveConfig[t('Other')] = { label: t('Other') };
  }

  return {
    config: topFiveConfig,
    data: [
      ...topFourData,
      ...(otherData.length > 0
        ? [
            {
              key: t('Other'),
              value: otherData.reduce((acc, curr) => acc + curr.value, 0),
            },
          ]
        : []),
    ],
  };
}
