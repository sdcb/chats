import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';

import { useRouter } from 'next/router';

import {
  clearKeycloakAttempts,
  clearPasswordAttempts,
  clearSmsAttempts,
  exportKeycloakAttempts,
  exportPasswordAttempts,
  exportSmsAttempts,
  getKeycloakAttempts,
  getPasswordAttempts,
  getSmsAttempts,
} from '@/apis/adminApis';
import DateTimePopover from '@/components/Popover/DateTimePopover';
import DeletePopover from '@/components/Popover/DeletePopover';
import {
  IconArrowDown,
  IconLoader,
  IconMessage,
  IconPasswordUser,
  IconShieldLock,
} from '@/components/Icons';
import PaginationContainer from '@/components/Pagiation/Pagiation';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/components/ui/tabs';
import useDebounce from '@/hooks/useDebounce';
import useTranslation from '@/hooks/useTranslation';
import {
  KeycloakAttemptLog,
  PasswordAttemptLog,
  SecurityLogExportParams,
  SecurityLogQueryParams,
  SmsAttemptLog,
} from '@/types/adminApis';
import { PageResult } from '@/types/page';
import { formatDateTime } from '@/utils/date';

type SecurityLogTab = 'password' | 'keycloak' | 'sms';

type SecurityLogRowMap = {
  password: PasswordAttemptLog;
  keycloak: KeycloakAttemptLog;
  sms: SmsAttemptLog;
};

const PAGE_SIZE = 20;

const listFetchers: Record<
  SecurityLogTab,
  (params: SecurityLogQueryParams) => Promise<PageResult<any>>
> = {
  password: getPasswordAttempts,
  keycloak: getKeycloakAttempts,
  sms: getSmsAttempts,
};

const exportFetchers: Record<
  SecurityLogTab,
  (params: SecurityLogExportParams) => Promise<Blob | null>
> = {
  password: exportPasswordAttempts,
  keycloak: exportKeycloakAttempts,
  sms: exportSmsAttempts,
};

const clearFetchers: Record<
  SecurityLogTab,
  (params: SecurityLogExportParams) => Promise<number>
> = {
  password: clearPasswordAttempts,
  keycloak: clearKeycloakAttempts,
  sms: clearSmsAttempts,
};

const formatDateParam = (date: Date) => date.toISOString().split('T')[0];

const isValidTab = (value: string | undefined): value is SecurityLogTab =>
  value === 'password' || value === 'keycloak' || value === 'sms';

const SecurityLogsPage = () => {
  const { t } = useTranslation();
  const router = useRouter();

  const [activeTab, setActiveTab] = useState<SecurityLogTab>('password');
  const [pagination, setPagination] = useState({ page: 1, pageSize: PAGE_SIZE });
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [filters, setFilters] = useState({ start: '', end: '', username: '' });
  const [tabData, setTabData] = useState<Record<SecurityLogTab, PageResult<any>>>(
    () => ({
      password: { rows: [], count: 0 },
      keycloak: { rows: [], count: 0 },
      sms: { rows: [], count: 0 },
    }),
  );
  const lastFetchKeyRef = useRef('');

  const { tab: tabQueryParam, page: pageQueryParam, start: startQueryParam, end: endQueryParam, username: usernameQueryParam } =
    router.query;

  const pushQuery = useCallback(
    (
      tabValue: SecurityLogTab,
      pageValue: number,
      startValue: string,
      endValue: string,
      usernameValue: string,
    ) => {
      if (!router.isReady) {
        return;
      }

      const query: Record<string, string> = {};
      if (tabValue !== 'password') {
        query.tab = tabValue;
      }
      if (pageValue > 1) {
        query.page = pageValue.toString();
      }
      if (startValue) {
        query.start = startValue;
      }
      if (endValue) {
        query.end = endValue;
      }
      if (usernameValue) {
        query.username = usernameValue;
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

  useEffect(() => {
    if (!router.isReady) {
      return;
    }

    const tabQueryValue = Array.isArray(tabQueryParam)
      ? tabQueryParam[0]
      : tabQueryParam;
    const resolvedTab = isValidTab(tabQueryValue) ? tabQueryValue : 'password';
    if (resolvedTab !== activeTab) {
      setActiveTab(resolvedTab);
    }

    const pageQueryValue = Array.isArray(pageQueryParam)
      ? pageQueryParam[0]
      : pageQueryParam;
    const pageNumber = pageQueryValue ? parseInt(pageQueryValue, 10) || 1 : 1;
    setPagination((prev) =>
      prev.page === pageNumber ? prev : { ...prev, page: pageNumber },
    );

    const startQuery =
      typeof startQueryParam === 'string' ? startQueryParam : '';
    const endQuery =
      typeof endQueryParam === 'string' ? endQueryParam : '';
    const usernameQuery =
      typeof usernameQueryParam === 'string' ? usernameQueryParam : '';

    setFilters((prev) =>
      prev.start === startQuery &&
      prev.end === endQuery &&
      prev.username === usernameQuery
        ? prev
        : { start: startQuery, end: endQuery, username: usernameQuery },
    );
  }, [
    activeTab,
    router.isReady,
    tabQueryParam,
    pageQueryParam,
    startQueryParam,
    endQueryParam,
    usernameQueryParam,
  ]);

  const refresh = useCallback(
    (options?: { force?: boolean }) => {
    if (!router.isReady) {
      return;
    }

    const params: SecurityLogQueryParams = {
      page: pagination.page,
      pageSize: pagination.pageSize,
      start: filters.start || undefined,
      end: filters.end || undefined,
      username: filters.username || undefined,
    };

    const fetchKey = JSON.stringify({
      tab: activeTab,
      page: params.page,
      pageSize: params.pageSize,
      start: params.start ?? '',
      end: params.end ?? '',
      username: params.username ?? '',
    });

    if (!options?.force && fetchKey === lastFetchKeyRef.current) {
      return;
    }

    lastFetchKeyRef.current = fetchKey;

    setLoading(true);
    listFetchers[activeTab](params)
      .then((result) => {
        setTabData((prev) => ({ ...prev, [activeTab]: result }));
      })
      .catch((error) => {
        console.error(error);
        toast.error(
          t('Operation failed, Please try again later, or contact technical personnel'),
        );
        lastFetchKeyRef.current = '';
      })
      .finally(() => {
        setLoading(false);
      });
  }, [
    activeTab,
    filters.end,
    filters.start,
    filters.username,
    pagination.page,
    pagination.pageSize,
    router.isReady,
    t,
  ]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const handleTabChange = (value: string) => {
    if (!isValidTab(value)) {
      return;
    }

    setActiveTab(value);
    const newPage = 1;
    setPagination((prev) => ({ ...prev, page: newPage }));
    pushQuery(value, newPage, filters.start, filters.end, filters.username);
  };

  const handleStartChange = (date: Date) => {
    const value = formatDateParam(date);
    const nextFilters = { ...filters, start: value };
    setFilters(nextFilters);
    const newPage = 1;
    setPagination((prev) => ({ ...prev, page: newPage }));
    pushQuery(activeTab, newPage, nextFilters.start, nextFilters.end, nextFilters.username);
  };

  const handleEndChange = (date: Date) => {
    const value = formatDateParam(date);
    const nextFilters = { ...filters, end: value };
    setFilters(nextFilters);
    const newPage = 1;
    setPagination((prev) => ({ ...prev, page: newPage }));
    pushQuery(activeTab, newPage, nextFilters.start, nextFilters.end, nextFilters.username);
  };

  const handleResetStart = () => {
    const nextFilters = { ...filters, start: '' };
    setFilters(nextFilters);
    const newPage = 1;
    setPagination((prev) => ({ ...prev, page: newPage }));
    pushQuery(activeTab, newPage, nextFilters.start, nextFilters.end, nextFilters.username);
  };

  const handleResetEnd = () => {
    const nextFilters = { ...filters, end: '' };
    setFilters(nextFilters);
    const newPage = 1;
    setPagination((prev) => ({ ...prev, page: newPage }));
    pushQuery(activeTab, newPage, nextFilters.start, nextFilters.end, nextFilters.username);
  };

  const handlePageChange = (pageValue: number) => {
    setPagination((prev) => ({ ...prev, page: pageValue }));
    pushQuery(activeTab, pageValue, filters.start, filters.end, filters.username);
  };

  const debouncedUsernameSync = useDebounce(
    (
      value: string,
      tabValue: SecurityLogTab,
      startValue: string,
      endValue: string,
    ) => {
      pushQuery(tabValue, 1, startValue, endValue, value);
    },
    600,
  );

  const handleUsernameChange = (value: string) => {
    const nextFilters = { ...filters, username: value };
    setFilters(nextFilters);
    setPagination((prev) => ({ ...prev, page: 1 }));
    debouncedUsernameSync(value, activeTab, nextFilters.start, nextFilters.end);
  };

  const handleExport = async () => {
    if (exporting) {
      return;
    }

    setExporting(true);
    const params: SecurityLogExportParams = {
      start: filters.start || undefined,
      end: filters.end || undefined,
      username: filters.username || undefined,
    };

    try {
      const blob = await exportFetchers[activeTab](params);
      if (!blob) {
        toast.error(t('No data'));
        return;
      }

      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${activeTab}-security-logs-${formatDateParam(new Date())}.xlsx`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (error) {
      console.error(error);
      toast.error(
        t('Operation failed, Please try again later, or contact technical personnel'),
      );
    } finally {
      setExporting(false);
    }
  };

  const handleClear = async () => {
    const params: SecurityLogExportParams = {
      start: filters.start || undefined,
      end: filters.end || undefined,
      username: filters.username || undefined,
    };

    try {
      await clearFetchers[activeTab](params);
      toast.success(t('Deleted successful'));
      refresh({ force: true });
    } catch (error) {
      console.error(error);
      toast.error(
        t('Operation failed, Please try again later, or contact technical personnel'),
      );
      throw error;
    }
  };

  const tabMeta = useMemo(
    () => [
      {
        value: 'password' as SecurityLogTab,
        label: t('Password Attempts'),
        icon: <IconPasswordUser size={16} />,
      },
      {
        value: 'keycloak' as SecurityLogTab,
        label: t('Keycloak Attempts'),
        icon: <IconShieldLock size={16} />,
      },
      {
        value: 'sms' as SecurityLogTab,
        label: t('SMS Attempts'),
        icon: <IconMessage size={16} />,
      },
    ],
    [t],
  );

  const renderFilters = () => (
    <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
      <div className="flex flex-wrap items-center gap-3">
        <DateTimePopover
          value={filters.start}
          placeholder={t('Start date')!}
          onSelect={handleStartChange}
          onReset={filters.start ? handleResetStart : undefined}
        />
        <DateTimePopover
          value={filters.end}
          placeholder={t('End date')!}
          onSelect={handleEndChange}
          onReset={filters.end ? handleResetEnd : undefined}
        />
        <Input
          className="w-[240px]"
          placeholder={t('Search by username')!}
          value={filters.username}
          onChange={(event) => handleUsernameChange(event.target.value)}
        />
      </div>
      <div className="flex items-center gap-2 self-end lg:self-auto">
        <Button
          variant="ghost"
          size="icon"
          className="h-9 w-9"
          onClick={handleExport}
          disabled={exporting || loading}
          title={t('Export to Excel') || undefined}
        >
          {exporting ? (
            <IconLoader className="animate-spin" size={18} />
          ) : (
            <IconArrowDown size={18} />
          )}
        </Button>
        <DeletePopover onDelete={handleClear} />
      </div>
    </div>
  );

  const renderTable = <T extends SecurityLogTab>(tab: T) => {
    const currentData = tabData[tab] as PageResult<SecurityLogRowMap[T][]>;
    const rows = (currentData?.rows ?? []) as SecurityLogRowMap[T][];
    const isActive = tab === activeTab;

    return (
      <Card className="mt-4">
        {isActive && loading ? (
          <div className="flex justify-center py-10">
            <IconLoader className="animate-spin" size={24} />
          </div>
        ) : rows.length === 0 ? (
          <div className="py-10 text-center text-sm text-muted-foreground">
            {t('No data')}
          </div>
        ) : tab === 'password' ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('Recorded Username')}</TableHead>
                <TableHead>{t('Bound User')}</TableHead>
                <TableHead>{t('Result')}</TableHead>
                <TableHead>{t('Failure Reason')}</TableHead>
                <TableHead>{t('IP Address')}</TableHead>
                <TableHead>{t('User Agent')}</TableHead>
                <TableHead>{t('Created Time')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(rows as PasswordAttemptLog[]).map((row) => (
                <TableRow key={row.id}>
                  <TableCell>{row.userName}</TableCell>
                  <TableCell>{row.matchedUserName || '-'}</TableCell>
                  <TableCell>
                    {row.isSuccessful ? t('Yes') : t('No')}
                  </TableCell>
                  <TableCell className="max-w-xs truncate">
                    {row.failureReason || '-'}
                  </TableCell>
                  <TableCell>{row.ip}</TableCell>
                  <TableCell className="max-w-sm truncate">
                    {row.userAgent}
                  </TableCell>
                  <TableCell>{formatDateTime(row.createdAt)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : tab === 'keycloak' ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('Provider')}</TableHead>
                <TableHead>{t('Subject')}</TableHead>
                <TableHead>{t('E-Mail')}</TableHead>
                <TableHead>{t('User Name')}</TableHead>
                <TableHead>{t('Result')}</TableHead>
                <TableHead>{t('Failure Reason')}</TableHead>
                <TableHead>{t('IP Address')}</TableHead>
                <TableHead>{t('User Agent')}</TableHead>
                <TableHead>{t('Created Time')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(rows as KeycloakAttemptLog[]).map((row) => (
                <TableRow key={row.id}>
                  <TableCell>{row.provider}</TableCell>
                  <TableCell className="max-w-xs truncate">
                    {row.sub || '-'}
                  </TableCell>
                  <TableCell className="max-w-xs truncate">
                    {row.email || '-'}
                  </TableCell>
                  <TableCell>{row.userName || '-'}</TableCell>
                  <TableCell>
                    {row.isSuccessful ? t('Yes') : t('No')}
                  </TableCell>
                  <TableCell className="max-w-xs truncate">
                    {row.failureReason || '-'}
                  </TableCell>
                  <TableCell>{row.ip}</TableCell>
                  <TableCell className="max-w-sm truncate">
                    {row.userAgent}
                  </TableCell>
                  <TableCell>{formatDateTime(row.createdAt)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('Phone Number')}</TableHead>
                <TableHead>{t('Code')}</TableHead>
                <TableHead>{t('User Name')}</TableHead>
                <TableHead>{t('Type')}</TableHead>
                <TableHead>{t('Status')}</TableHead>
                <TableHead>{t('IP Address')}</TableHead>
                <TableHead>{t('User Agent')}</TableHead>
                <TableHead>{t('Created Time')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(rows as SmsAttemptLog[]).map((row) => (
                <TableRow key={row.id}>
                  <TableCell>{row.phoneNumber}</TableCell>
                  <TableCell>{row.code}</TableCell>
                  <TableCell>{row.userName || '-'}</TableCell>
                  <TableCell>{row.type || '-'}</TableCell>
                  <TableCell>{row.status || '-'}</TableCell>
                  <TableCell>{row.ip}</TableCell>
                  <TableCell className="max-w-sm truncate">
                    {row.userAgent}
                  </TableCell>
                  <TableCell>{formatDateTime(row.createdAt)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        {rows.length > 0 && (
          <PaginationContainer
            page={pagination.page}
            pageSize={pagination.pageSize}
            currentCount={rows.length}
            totalCount={currentData?.count || 0}
            onPagingChange={(pageNumber) => handlePageChange(pageNumber)}
          />
        )}
      </Card>
    );
  };

  return (
    <div className="space-y-4">
      <Tabs
        value={activeTab}
        onValueChange={handleTabChange}
        orientation="horizontal"
        className="flex-col gap-3 border-none p-0 text-foreground"
      >
        <TabsList className="flex w-full flex-row flex-wrap gap-2 rounded-none bg-transparent p-0">
          {tabMeta.map((tab) => (
            <TabsTrigger
              key={tab.value}
              value={tab.value}
              className="flex items-center gap-2"
            >
              {tab.icon}
              <span>{tab.label}</span>
            </TabsTrigger>
          ))}
        </TabsList>

        <div>{renderFilters()}</div>

        <TabsContent value="password" className="ml-0 mt-4">
          {renderTable('password')}
        </TabsContent>
        <TabsContent value="keycloak" className="ml-0 mt-4">
          {renderTable('keycloak')}
        </TabsContent>
        <TabsContent value="sms" className="ml-0 mt-4">
          {renderTable('sms')}
        </TabsContent>
      </Tabs>
    </div>
  );
};

export default SecurityLogsPage;
