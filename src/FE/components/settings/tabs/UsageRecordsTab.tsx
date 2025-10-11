import { useEffect, useState } from 'react';

import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { formatNumberAsMoney, toFixed } from '@/utils/common';
import { formatDate, formatDateTime, getTz } from '@/utils/date';
import { getUserSession } from '@/utils/user';

import { UsageSource } from '@/types/chat';
import {
  GetUsageParams,
  GetUsageResult,
  GetUsageStatResult,
} from '@/types/clientApis';
import { GetUserApiKeyResult } from '@/types/clientApis';
import { feModelProviders } from '@/types/model';
import { PageResult } from '@/types/page';

import DateTimePopover from '@/components/Popover/DateTimePopover';

import ExportButton from '@/components/Button/ExportButtom';
import PaginationContainer from '@/components/Pagination/Pagination';
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
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import {
  getUsage,
  getUsageStat,
  getUserApiKey,
  getUserModels,
} from '@/apis/clientApis';
import { useUserInfo } from '@/providers/UserProvider';

interface Provider {
  modelProviderId: number;
  name: string;
}

interface QueryParams {
  [key: string]: string | string[] | undefined;
  tab?: string;
  source?: string;
  kid?: string;
  page?: string;
  start?: string;
  end?: string;
  provider?: string;
}

const UsageRecordsTab = () => {
  const { t } = useTranslation();
  const router = useRouter();
  const user = useUserInfo();

  const { source, kid, page, start, end, provider } = router.query;

  const [usageLogs, setUsageLogs] = useState<GetUsageResult[]>([]);
  const [usageStat, setUsageStat] = useState<GetUsageStatResult>(
    {} as GetUsageStatResult,
  );
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
    page: parseInt((page as string) || '1'),
    pageSize: 10,
  });

  const [startDate, setStartDate] = useState<string>((start as string) || '');
  const [endDate, setEndDate] = useState<string>((end as string) || '');
  const [selectedSource, setSelectedSource] = useState<string>(
    (source as string) || '',
  );

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
    if (router.isReady) {
      setStartDate((start as string) || '');
      setEndDate((end as string) || '');
      setSelectedProvider((provider as string) || '');
      setSelectedApiKey((kid as string) || '');
      setSelectedSource((source as string) || '');
      fetchUsageData();
    }
  }, [
    router.query.kid,
    router.query.page,
    router.query.start,
    router.query.end,
    router.query.provider,
    router.query.source,
    router.isReady,
  ]);

  useEffect(() => {
    if (router.isReady) {
      fetchUsageStat();
    }
  }, [
    router.query.kid,
    router.query.start,
    router.query.end,
    router.query.provider,
    router.query.source,
    router.isReady,
  ]);

  function getUsageParams(exportExcel: boolean = false) {
    const params: GetUsageParams = {
      kid: selectedApiKey || undefined,
      user: user?.username,
      page: pagination.page,
      pageSize: pagination.pageSize,
      tz: getTz(),
    };

    if (selectedSource) {
      params.source = Number(selectedSource) as UsageSource;
    }

    if (exportExcel) {
      delete params.page;
      delete params.pageSize;
    }

    if (startDate) {
      params.start = startDate;
    }

    if (endDate) {
      params.end = endDate;
    }

    if (selectedProvider) {
      params.provider = selectedProvider;
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

  const fetchUsageStat = () => {
    const params: GetUsageParams = getUsageParams();
    getUsageStat(params).then((data: GetUsageStatResult) => {
      setUsageStat(data);
    });
  };

  const handlePageChange = (page: number) => {
    setPagination({ ...pagination, page });

    const query: Record<string, string> = {
      tab: 'usage',
      page: page.toString(),
    };

    if (selectedSource) query.source = selectedSource;
    if (selectedApiKey) query.kid = selectedApiKey;
    if (startDate) query.start = startDate;
    if (endDate) query.end = endDate;
    if (selectedProvider) query.provider = selectedProvider;

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
    <div className="flex flex-col">
      <Card className="p-3 mb-4 border-none">
        <div className="flex flex-col sm:flex-row gap-4 items-end">
          <div className="w-full flex items-center gap-2 flex-wrap">
            <div className="flex items-center gap-2">
              <Select
                value={selectedSource}
                onValueChange={(value) => {
                  setSelectedSource(value);
                  const query: Record<string, string> = {
                    ...(router.query as Record<string, string>),
                    source: value,
                    tab: 'usage',
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
              >
                <SelectTrigger
                  className="w-48"
                  value={selectedSource}
                  onReset={() => {
                    setSelectedSource('');
                    const query: Record<string, string> = {
                      ...(router.query as Record<string, string>),
                      tab: 'usage',
                    };
                    delete query.source;
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
                  <SelectValue placeholder={t('Select Source')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={String(UsageSource.Web)}>
                    {t('Web')}
                  </SelectItem>
                  <SelectItem value={String(UsageSource.API)}>
                    {t('API')}
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="flex items-center gap-2">
              <Select
                value={selectedApiKey}
                onValueChange={(value) => {
                  setSelectedApiKey(value);
                  const query: Record<string, string> = {
                    ...(router.query as Record<string, string>),
                    kid: value,
                    tab: 'usage',
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
              >
                <SelectTrigger
                  className="w-48"
                  value={selectedApiKey}
                  onReset={() => {
                    setSelectedApiKey('');
                    const query: Record<string, string> = {
                      ...(router.query as Record<string, string>),
                      tab: 'usage',
                    };
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
                  const query: Record<string, string> = {
                    ...(router.query as Record<string, string>),
                    provider: value,
                    tab: 'usage',
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
              >
                <SelectTrigger
                  className="w-48"
                  value={selectedProvider}
                  onReset={() => {
                    setSelectedProvider('');
                    const query: Record<string, string> = {
                      ...(router.query as Record<string, string>),
                      tab: 'usage',
                    };
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
                  const formattedDate = formatDate(date.toLocaleDateString());
                  setStartDate(formattedDate);
                  const query: Record<string, string> = {
                    ...(router.query as Record<string, string>),
                    start: formattedDate,
                    tab: 'usage',
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
                  const query: Record<string, string> = {
                    ...(router.query as Record<string, string>),
                    tab: 'usage',
                  };
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
                  const formattedDate = formatDate(date.toLocaleDateString());
                  setEndDate(formattedDate);
                  const query: Record<string, string> = {
                    ...(router.query as Record<string, string>),
                    end: formattedDate,
                    tab: 'usage',
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
                  const query: Record<string, string> = {
                    ...(router.query as Record<string, string>),
                    tab: 'usage',
                  };
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
                exportUrl="/api/usage/excel"
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
            <Card className="p-2 px-4 border-none shadow-sm">
              <div className="flex items-center justify-between text-xs">
                <div className="font-medium">{t('Input/Output Tokens')}</div>
                <div>
                  {usageStat?.sumInputTokens}/{usageStat?.sumOutputTokens}
                </div>
              </div>
              <div className="flex items-center justify-between text-xs">
                <div className="font-medium">{t('Input/Output Cost(￥)')}</div>
                <div>
                  ￥{toFixed(usageStat?.sumInputCost)}/
                  {toFixed(usageStat?.sumOutputCost)}
                </div>
              </div>
              <div className="flex items-center justify-between text-xs">
                <div className="font-medium">{t('Total Cost')}</div>
                <div>￥{toFixed(usageStat?.sumTotalCost)}</div>
              </div>
            </Card>
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
                    ￥{toFixed(log.inputCost)}/{toFixed(log.outputCost)}
                  </div>
                </div>
                <div className="flex items-center justify-between text-xs mt-1">
                  <div className="font-medium">{t('Total Cost')}</div>
                  <div>￥{toFixed(log.inputCost + log.outputCost)}</div>
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
                    ￥{toFixed(log.inputCost)}/￥{toFixed(log.outputCost)}
                  </TableCell>
                  <TableCell>
                    ￥{toFixed(log.inputCost + log.outputCost)}
                  </TableCell>
                  <TableCell>{log.ip}</TableCell>
                  <TableCell>{log.finishReason}</TableCell>
                  <TableCell>
                    {(log.totalDurationMs / 1000).toLocaleString()}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
            {totalCount > 0 && (
              <TableFooter className="bg-card">
                <TableRow>
                  <TableCell colSpan={2}>{t('Total')}</TableCell>
                  <TableCell>
                    {formatNumberAsMoney(usageStat?.sumInputTokens)}/
                    {formatNumberAsMoney(usageStat?.sumOutputTokens)}
                  </TableCell>
                  <TableCell>
                    ￥{toFixed(usageStat?.sumInputCost)}/ ￥
                    {toFixed(usageStat?.sumOutputCost)}
                  </TableCell>
                  <TableCell>￥{toFixed(usageStat?.sumTotalCost)}</TableCell>
                  <TableCell colSpan={3}></TableCell>
                </TableRow>
              </TableFooter>
            )}
          </Table>

          {totalCount > 0 && (
            <PaginationContainer
              showPageNumbers={true}
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
        </Card>
      </div>
    </div>
  );
};

export default UsageRecordsTab;
