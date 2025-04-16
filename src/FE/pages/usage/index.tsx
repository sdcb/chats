import React, { useEffect, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { formatDate, formatDateTime } from '@/utils/date';
import { getUserSession } from '@/utils/user';

import { UsageSource } from '@/types/chat';
import { GetUsageParams, GetUsageResult } from '@/types/clientApis';
import { GetUserApiKeyResult } from '@/types/clientApis';
import { feModelProviders } from '@/types/model';
import { PageResult } from '@/types/page';

import DateTimePopover from '@/pages/home/_components/Popover/DateTimePopover';

import ExportButton from '@/components/Button/ExportButtom';
import { IconArrowDown } from '@/components/Icons';
import PaginationContainer from '@/components/Pagiation/Pagiation';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import { getUsage, getUserApiKey, getUserModels } from '@/apis/clientApis';
import { useUserInfo } from '@/providers/UserProvider';

interface Provider {
  modelProviderId: number;
  name: string;
}

const UsagePage = () => {
  const { t } = useTranslation();
  const router = useRouter();
  const user = useUserInfo();
  const { source, kid, page, start, end, provider, tab } =
    router.query as any as {
      source: UsageSource;
      kid: string;
      page: string;
      start: string;
      end: string;
      provider: string;
      tab: string;
    };

  const [usageLogs, setUsageLogs] = useState<GetUsageResult[]>([]);
  const [loading, setLoading] = useState(true);
  const [totalCount, setTotalCount] = useState(0);
  const [providers, setProviders] = useState<Provider[]>([]);
  const [apiKeys, setApiKeys] = useState<GetUserApiKeyResult[]>([]);
  const [selectedProvider, setSelectedProvider] = useState<string>(
    (provider as string) || '',
  );
  const [selectedApiKey, setSelectedApiKey] = useState<string>(
    (kid as string) || '',
  );
  const [pagination, setPagination] = useState({
    page: parseInt(page as string) || 1,
    pageSize: 10,
  });

  const [startDate, setStartDate] = useState<string>((start as string) || '');
  const [endDate, setEndDate] = useState<string>((end as string) || '');

  useEffect(() => {
    getUserModels().then((data) => {
      const uniqueProviders = Array.from(
        new Set(data.map((model) => model.modelProviderId)),
      ).map((providerId) => {
        const provider = feModelProviders.find((p) => p.id === providerId);
        return {
          modelProviderId: providerId,
          name: provider?.name || `Provider ${providerId}`,
        };
      });
      setProviders(uniqueProviders);
    });

    getUserApiKey().then((data) => {
      setApiKeys(data);
    });
  }, []);

  useEffect(() => {
    setStartDate((start as string) || '');
    setEndDate((end as string) || '');
    setSelectedProvider((provider as string) || '');
    setSelectedApiKey((kid as string) || '');
    fetchUsageData();
  }, [
    router.query.kid,
    router.query.page,
    router.query.start,
    router.query.end,
    router.query.provider,
  ]);

  function getUsageParams(exportExcel: boolean = false) {
    const params: GetUsageParams = {
      kid: selectedApiKey || undefined,
      user: user?.username,
      page: pagination.page,
      pageSize: pagination.pageSize,
      tz: new Date().getTimezoneOffset(),
      source: source as any,
    };

    if (exportExcel) {
      delete params.page;
      delete params.pageSize;
    }

    if (router.query.start) {
      params.start = router.query.start as string;
    }

    if (router.query.end) {
      params.end = router.query.end as string;
    }

    if (router.query.provider) {
      params.provider = router.query.provider as string;
    }
    return params;
  }

  const fetchUsageData = () => {
    setLoading(true);
    const params: GetUsageParams = getUsageParams();

    getUsage(params)
      .then((data: PageResult<GetUsageResult[]>) => {
        setUsageLogs(data.rows);
        setTotalCount(data.count);
      })
      .finally(() => {
        setLoading(false);
      });
  };

  const handlePageChange = (page: number) => {
    const query: Record<string, string> = {
      ...(router.query as Record<string, string>),
      page: page.toString(),
    };
    router.push(
      {
        pathname: router.pathname,
        query,
      },
      undefined,
      { shallow: true },
    );
  };

  return (
    <div className="container max-w-screen-xl mx-auto py-6 px-4 sm:px-6 h-screen">
      <h1 className="text-2xl font-bold mb-6 flex items-center gap-2">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => router.push('/settings?tab=' + (tab || 'api-keys'))}
        >
          <IconArrowDown className="rotate-90" size={20} />
        </Button>
        {t('Usage Records')}
      </h1>

      <Card className="p-4 mb-4 border-none">
        <div className="flex flex-col sm:flex-row gap-4 items-end">
          <div className="w-full flex items-center gap-2 flex-wrap">
            <div className="flex items-center gap-2">
              <Select
                value={selectedApiKey}
                onValueChange={(value) => {
                  setSelectedApiKey(value);
                  const query = { ...router.query, kid: value };
                  router.push(
                    {
                      pathname: router.pathname,
                      query,
                    },
                    undefined,
                    { shallow: true },
                  );
                }}
              >
                <SelectTrigger
                  className="w-48"
                  value={selectedApiKey}
                  onReset={() => {
                    setSelectedApiKey('');
                    const query = { ...router.query };
                    delete query.kid;
                    router.push(
                      {
                        pathname: router.pathname,
                        query,
                      },
                      undefined,
                      { shallow: true },
                    );
                  }}
                >
                  <SelectValue placeholder={t('Select API Key')} />
                </SelectTrigger>
                <SelectContent>
                  {apiKeys.map((apiKey) => (
                    <SelectItem
                      key={apiKey.id.toString()}
                      value={apiKey.id.toString()}
                    >
                      {apiKey.key}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex items-center gap-2">
              <Select
                value={selectedProvider}
                onValueChange={(value) => {
                  setSelectedProvider(value);
                  const query = { ...router.query, provider: value };
                  router.push(
                    {
                      pathname: router.pathname,
                      query,
                    },
                    undefined,
                    { shallow: true },
                  );
                }}
              >
                <SelectTrigger
                  className="w-48"
                  value={selectedProvider}
                  onReset={() => {
                    setSelectedProvider('');
                    const query = { ...router.query };
                    delete query.provider;
                    router.push(
                      {
                        pathname: router.pathname,
                        query,
                      },
                      undefined,
                      { shallow: true },
                    );
                  }}
                >
                  <SelectValue placeholder={t('Select Provider')} />
                </SelectTrigger>
                <SelectContent>
                  {providers.map((provider) => (
                    <SelectItem key={provider.name} value={provider.name}>
                      {provider.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-center gap-2">
              <DateTimePopover
                className="w-48"
                placeholder={t('Start date')}
                value={startDate}
                onSelect={(date: Date) => {
                  setStartDate(formatDate(date.toLocaleDateString()));
                  const query = {
                    ...router.query,
                    start: formatDate(date.toLocaleDateString()),
                  };
                  router.push(
                    {
                      pathname: router.pathname,
                      query,
                    },
                    undefined,
                    { shallow: true },
                  );
                }}
                onReset={() => {
                  setStartDate('');
                  const query = { ...router.query };
                  delete query.start;
                  router.push(
                    {
                      pathname: router.pathname,
                      query,
                    },
                    undefined,
                    { shallow: true },
                  );
                }}
              />
            </div>
            <div className="flex items-center gap-2">
              <DateTimePopover
                className="w-48"
                placeholder={t('End date')}
                value={endDate}
                onSelect={(date: Date) => {
                  setEndDate(formatDate(date.toLocaleDateString()));
                  const query = {
                    ...router.query,
                    end: formatDate(date.toLocaleDateString()),
                  };
                  router.push(
                    {
                      pathname: router.pathname,
                      query,
                    },
                    undefined,
                    { shallow: true },
                  );
                }}
                onReset={() => {
                  setEndDate('');
                  const query = { ...router.query };
                  delete query.end;
                  router.push(
                    {
                      pathname: router.pathname,
                      query,
                    },
                    undefined,
                    { shallow: true },
                  );
                }}
              />
            </div>
            <div className="flex items-center gap-2">
              <ExportButton
                buttonText={t('Export to Excel')}
                exportUrl="/api/usage/export"
                params={{ ...getUsageParams(true), token: getUserSession() }}
              />
            </div>
          </div>
        </div>
      </Card>

      <div className="block sm:hidden">
        {loading ? (
          <div className="flex justify-center py-4">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-gray-900 dark:border-white"></div>
          </div>
        ) : usageLogs.length === 0 ? (
          <div className="text-center py-4 text-sm text-gray-500">
            {t('No data')}
          </div>
        ) : (
          <div className="space-y-2">
            {usageLogs.map((log, index) => (
              <Card key={index} className="p-2 px-4 border-none shadow-sm">
                <div className="flex items-center justify-between text-xs">
                  <div className="font-medium">{t('Date')}</div>
                  <div>{formatDateTime(log.usagedCreatedAt)}</div>
                </div>
                <div className="flex items-center justify-between text-xs mt-1">
                  <div className="font-medium">{t('Provider/Model')}</div>
                  <div className="overflow-hidden text-ellipsis whitespace-nowrap">
                    {log.modelProviderName}/{log.modelName}
                  </div>
                </div>
                <div className="flex items-center justify-between text-xs mt-1">
                  <div className="font-medium">{t('Input/Output Tokens')}</div>
                  <div>
                    {log.inputTokens}/{log.outputTokens}
                  </div>
                </div>
                <div className="flex items-center justify-between text-xs mt-1">
                  <div className="font-medium">
                    {t('Input/Output Cost(￥)')}
                  </div>
                  <div>
                    ￥{log.inputCost.toFixed(4)}/{log.outputCost.toFixed(4)}
                  </div>
                </div>
                <div className="flex items-center justify-between text-xs mt-1">
                  <div className="font-medium">{t('Total Cost')}</div>
                  <div>￥{(log.inputCost + log.outputCost).toFixed(4)}</div>
                </div>
                <div className="flex items-center justify-between text-xs mt-1">
                  <div className="font-medium">{t('IP')}</div>
                  <div>{log.ip}</div>
                </div>
                <div className="flex items-center justify-between text-xs mt-1">
                  <div className="font-medium">{t('Finish Reason')}</div>
                  <div>{log.finishReason}</div>
                </div>
                <div className="flex items-center justify-between text-xs mt-1">
                  <div className="font-medium">{t('Total Duration(ms)')}</div>
                  <div>{(log.totalDurationMs / 1000).toLocaleString()}</div>
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
                <TableHead>{t('Provider/Model')}</TableHead>
                <TableHead>{t('Input/Output Tokens')}</TableHead>
                <TableHead>{t('Input/Output Cost(￥)')}</TableHead>
                <TableHead>{t('Total Cost(￥)')}</TableHead>
                <TableHead>{t('IP')}</TableHead>
                <TableHead>{t('Finish Reason')}</TableHead>
                <TableHead>{t('Total Duration(ms)')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody isEmpty={usageLogs.length === 0} isLoading={loading}>
              {usageLogs.map((log, index) => (
                <TableRow key={index} className="cursor-pointer">
                  <TableCell>{formatDateTime(log.usagedCreatedAt)}</TableCell>
                  <TableCell>
                    {log.modelProviderName}/{log.modelName}
                  </TableCell>
                  <TableCell>
                    {log.inputTokens}/{log.outputTokens}
                  </TableCell>
                  <TableCell>
                    {log.inputCost.toFixed(4)}/{log.outputCost.toFixed(4)}
                  </TableCell>
                  <TableCell>
                    {(log.inputCost + log.outputCost).toFixed(4)}
                  </TableCell>
                  <TableCell>{log.ip}</TableCell>
                  <TableCell>{log.finishReason}</TableCell>
                  <TableCell>
                    {(log.totalDurationMs / 1000).toLocaleString()}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Card>
      </div>

      {totalCount !== 0 && (
        <PaginationContainer
          page={pagination.page}
          pageSize={pagination.pageSize}
          currentCount={usageLogs.length}
          totalCount={totalCount}
          onPagingChange={(page: number, pageSize: number) => {
            setPagination({ page, pageSize });
            handlePageChange(page);
          }}
        />
      )}
    </div>
  );
};

export default UsagePage;
