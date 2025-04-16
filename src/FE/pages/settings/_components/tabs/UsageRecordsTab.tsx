import { useEffect, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { formatDate } from '@/utils/date';

import { GetBalance7DaysUsageResult } from '@/types/clientApis';

import { Card } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import { getBalance7DaysUsage } from '@/apis/clientApis';

const UsageRecordsTab = () => {
  const { t } = useTranslation();
  const router = useRouter();
  const [balanceLogs, setBalanceLogs] = useState<GetBalance7DaysUsageResult[]>(
    [],
  );
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    getBalance7DaysUsage().then((data) => {
      setBalanceLogs(data);
      setLoading(false);
    });
  }, []);

  const viewWebUsage = (date: string) => {
    router.push(`/usage?start=${date}&end=${date}&page=1&tab=usage`);
  };

  return (
    <div className="flex flex-col">
      <h2 className="text-base font-semibold mb-2">
        {t('Recent 7 day consumption records')}
      </h2>

      <div className="block sm:hidden">
        {loading ? (
          <div className="flex justify-center py-4">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-gray-900 dark:border-white"></div>
          </div>
        ) : balanceLogs.length === 0 ? (
          <div className="text-center py-4 text-sm text-gray-500">
            {t('No data')}
          </div>
        ) : (
          <div className="space-y-2">
            {balanceLogs.map((x) => (
              <Card key={x.date} className="p-2 px-4 border-none shadow-sm">
                <div className="flex items-center justify-between text-xs">
                  <div className="font-medium">{t('Date')}</div>
                  <div>{formatDate(x.date)}</div>
                </div>
                <div className="flex items-center justify-between text-xs mt-1">
                  <div className="font-medium">{t('Amount')}</div>
                  <div>￥{(+(x.costAmount || 0)).toFixed(2)}</div>
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>

      <div className="hidden sm:block">
        <Card className="overflow-x-auto border-none">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('Date')}</TableHead>
                <TableHead>{t('Amount')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody isLoading={loading}>
              {balanceLogs.map((x) => (
                <TableRow key={x.date} className="cursor-pointer">
                  <TableCell>
                    <span
                      className="truncate cursor-pointer text-blue-600 hover:underline"
                      onClick={() => viewWebUsage(x.date)}
                    >
                      {formatDate(x.date)}
                    </span>
                  </TableCell>
                  <TableCell>￥{(+(x.costAmount || 0)).toFixed(2)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Card>
      </div>
    </div>
  );
};

export default UsageRecordsTab;
