import { useEffect, useState } from 'react';

import { useRouter } from 'next/router';

import useDebounce from '@/hooks/useDebounce';
import useTranslation from '@/hooks/useTranslation';

import { toFixed } from '@/utils/common';
import { formatDate, formatDateTime, getTz } from '@/utils/date';
import { getUserSession } from '@/utils/user';

import { UsageSource } from '@/types/chat';
import {
  GetUsageParams,
  GetUsageResult,
  GetUsageStatResult,
} from '@/types/clientApis';
import { feModelProviders } from '@/types/model';
import { PageResult } from '@/types/page';

import DateTimePopover from '@/pages/home/_components/Popover/DateTimePopover';

import ExportButton from '@/components/Button/ExportButtom';
import PaginationContainer from '@/components/Pagiation/Pagiation';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
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

import { getUsage, getUsageStat, getUserModels } from '@/apis/clientApis';

interface Provider {
  modelProviderId: number;
  name: string;
}

interface QueryParams {
  [key: string]: string | string[] | undefined;
  source?: string;
  page?: string;
  start?: string;
  end?: string;
  provider?: string;
  user?: string;
}

const UsageRecords = () => {
  const { t } = useTranslation();
  const router = useRouter();

  const { source, page, start, end, provider, user } = router.query;

  const [usageLogs, setUsageLogs] = useState<GetUsageResult[]>([]);
  const [usageStat, setUsageStat] = useState<GetUsageStatResult>(
    {} as GetUsageStatResult,
  );
  const [loading, setLoading] = useState(true);
  const [totalCount, setTotalCount] = useState(0);
  const [providers, setProviders] = useState<Provider[]>([]);
  const [selectedProvider, setSelectedProvider] = useState<string>(
    (provider as string) || '',
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
  const [userFilter, setUserFilter] = useState<string>((user as string) || '');

  const updateQueryWithDebounce = useDebounce((user: string) => {
    const query: Record<string, string> = {
      ...(router.query as Record<string, string>),
    };

    if (user) {
      query.user = user;
    } else {
      delete query.user;
    }

    router.push(
      {
        pathname: router.pathname,
        query,
      },
      undefined,
      { shallow: true },
    );
  }, 1000);

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
  }, []);

  useEffect(() => {
    if (router.isReady) {
      setStartDate((start as string) || '');
      setEndDate((end as string) || '');
      setSelectedProvider((provider as string) || '');
      setSelectedSource((source as string) || '');
      setUserFilter((user as string) || '');
      fetchUsageData();
      fetchUsageStat();
    }
  }, [
    router.query.page,
    router.query.start,
    router.query.end,
    router.query.provider,
    router.query.source,
    router.query.user,
    router.isReady,
  ]);

  function getUsageParams(exportExcel: boolean = false) {
    const params: GetUsageParams = {
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

    if (userFilter) {
      params.user = userFilter;
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
      page: page.toString(),
    };

    if (selectedSource) query.source = selectedSource;
    if (startDate) query.start = startDate;
    if (endDate) query.end = endDate;
    if (selectedProvider) query.provider = selectedProvider;
    if (userFilter) query.user = userFilter;

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
      <div className="mb-4 border-none">
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
                value={selectedProvider}
                onValueChange={(value) => {
                  setSelectedProvider(value);
                  const query: Record<string, string> = {
                    ...(router.query as Record<string, string>),
                    provider: value,
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
              <Input
                className="w-48 placeholder:text-neutral-400"
                placeholder={t('User Name')}
                value={userFilter}
                onChange={(e) => {
                  const value = e.target.value;
                  setUserFilter(value);
                  updateQueryWithDebounce(value);
                }}
              />
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
      </div>

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
                  <div className="font-medium">{t('User Name')}</div>
                  <div>{log.userName}</div>
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
                <TableHead>{t('User Name')}</TableHead>
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
                  <TableCell>{log.userName}</TableCell>
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
          </Table>
        </Card>
      </div>

      {totalCount > 0 && (
        <div className="mt-4 flex flex-col items-center">
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
        </div>
      )}
    </div>
  );
};

export default UsageRecords;
