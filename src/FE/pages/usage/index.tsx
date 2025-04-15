import React, { useEffect, useState } from 'react';
import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import DateTimePopover from '@/pages/home/_components/Popover/DateTimePopover';
import { GetUsageParams, GetUsageResult } from '@/types/clientApis';
import { PageResult } from '@/types/page';

import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
    Pagination,
    PaginationContent,
    PaginationItem,
    PaginationLink,
    PaginationNext,
    PaginationPrevious,
} from '@/components/ui/pagination';
import { IconArrowDown } from '@/components/Icons';

import { getUsage } from '@/apis/clientApis';
import { useUserInfo } from '@/providers/UserProvider';

const UsagePage = () => {
    const { t } = useTranslation();
    const router = useRouter();
    const user = useUserInfo();
    const { apiKeyId, key, page, start, end } = router.query;

    const [usageLogs, setUsageLogs] = useState<GetUsageResult[]>([]);
    const [loading, setLoading] = useState(true);
    const [totalCount, setTotalCount] = useState(0);
    const pageSize = 10;

    const [startDate, setStartDate] = useState<string>(start as string || '');
    const [endDate, setEndDate] = useState<string>(end as string || '');

    const currentPage = page ? parseInt(page as string) : 1;

    useEffect(() => {
        if (apiKeyId) {
            fetchUsageData();
        }
    }, [apiKeyId, router.query.page, router.query.start, router.query.end]);

    useEffect(() => {
        setStartDate(start as string || '');
        setEndDate(end as string || '');
    }, [router.query.start, router.query.end]);

    const fetchUsageData = () => {
        setLoading(true);
        const params: GetUsageParams = {
            ApiKeyId: String(apiKeyId),
            User: '',
            Page: currentPage,
            PageSize: pageSize
        };

        if (router.query.start) {
            params.Start = router.query.start as string;
        }

        if (router.query.end) {
            params.End = router.query.end as string;
        }

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
            page: page.toString()
        };
        router.push({
            pathname: router.pathname,
            query
        }, undefined, { shallow: true });
    };

    const handleFilter = () => {
        const query: Record<string, string> = {
            ...router.query as Record<string, string>,
            page: '1'
        };

        if (startDate) {
            query.start = startDate;
        } else {
            delete query.start;
        }

        if (endDate) {
            query.end = endDate;
        } else {
            delete query.end;
        }

        router.push({
            pathname: router.pathname,
            query
        }, undefined, { shallow: true });
    };

    const clearFilter = () => {
        setStartDate('');
        setEndDate('');

        const query: Record<string, string> = { ...router.query as Record<string, string> };
        delete query.start;
        delete query.end;
        query.page = '1';

        router.push({
            pathname: router.pathname,
            query
        }, undefined, { shallow: true });
    };

    const formatDateTime = (dateStr: string) => {
        const date = new Date(dateStr);
        return date.toLocaleString();
    };

    const totalPages = Math.ceil(totalCount / pageSize);
    const pagesArray = Array.from({ length: totalPages }, (_, i) => i + 1);

    return (
        <div className="container max-w-screen-xl mx-auto py-6 px-4 sm:px-6 h-screen">
            <h1 className="text-2xl font-bold mb-6 flex items-center gap-2">
                <Button variant="ghost" size="icon" onClick={() => router.push('/settings?tab=api-keys')}>
                    <IconArrowDown className="rotate-90" size={20} />
                </Button>
                {t('API Key Usage Records')}: {key}
            </h1>

            <Card className="p-4 mb-4 border-none">
                <div className="flex flex-col sm:flex-row gap-4 items-end">
                    <div className="w-full sm:w-1/ flex items-center gap-2">

                        <DateTimePopover
                            value={startDate}
                            onSelect={(date: Date) => {
                                setStartDate(date.toLocaleDateString())
                            }}
                        />
                        -
                        <DateTimePopover
                            value={endDate}
                            onSelect={(date: Date) => {
                                setEndDate(date.toLocaleDateString())
                            }}
                        />
                    </div>
                    <div className="flex gap-2">
                        <Button onClick={handleFilter} variant="default">
                            {t('Apply Filter')}
                        </Button>
                        <Button onClick={clearFilter} variant="outline">
                            {t('Clear')}
                        </Button>
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
                                    <div className="font-medium">{t('Time')}</div>
                                    <div>{formatDateTime(log.usagedCreatedAt)}</div>
                                </div>
                                <div className="flex items-center justify-between text-xs mt-1">
                                    <div className="font-medium">{t('Model')}</div>
                                    <div>{log.modelName}</div>
                                </div>
                                <div className="flex items-center justify-between text-xs mt-1">
                                    <div className="font-medium">{t('Input Tokens')}</div>
                                    <div>{log.inputTokens}</div>
                                </div>
                                <div className="flex items-center justify-between text-xs mt-1">
                                    <div className="font-medium">{t('Output Tokens')}</div>
                                    <div>{log.outputTokens}</div>
                                </div>
                                <div className="flex items-center justify-between text-xs mt-1">
                                    <div className="font-medium">{t('Total Cost')}</div>
                                    <div>￥{(log.inputCost + log.outputCost).toFixed(4)}</div>
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
                                <TableHead>{t('Time')}</TableHead>
                                <TableHead>{t('Model')}</TableHead>
                                <TableHead>{t('Input Tokens')}</TableHead>
                                <TableHead>{t('Output Tokens')}</TableHead>
                                <TableHead>{t('Input Cost')}</TableHead>
                                <TableHead>{t('Output Cost')}</TableHead>
                                <TableHead>{t('Total Cost')}</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody isEmpty={usageLogs.length === 0} isLoading={loading}>
                            {usageLogs.map((log, index) => (
                                <TableRow key={index} className="cursor-pointer">
                                    <TableCell>{formatDateTime(log.usagedCreatedAt)}</TableCell>
                                    <TableCell>{log.modelName}</TableCell>
                                    <TableCell>{log.inputTokens}</TableCell>
                                    <TableCell>{log.outputTokens}</TableCell>
                                    <TableCell>￥{log.inputCost.toFixed(4)}</TableCell>
                                    <TableCell>￥{log.outputCost.toFixed(4)}</TableCell>
                                    <TableCell>￥{(log.inputCost + log.outputCost).toFixed(4)}</TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </Card>
            </div>

            {totalPages > 1 && (
                <Pagination className="mt-4">
                    <PaginationContent>
                        <PaginationItem>
                            <PaginationPrevious
                                onClick={() => handlePageChange(Math.max(currentPage - 1, 1))}
                            />
                        </PaginationItem>
                        {pagesArray.slice(
                            Math.max(0, currentPage - 3),
                            Math.min(currentPage + 2, totalPages)
                        ).map((page) => (
                            <PaginationItem key={page}>
                                <PaginationLink
                                    isActive={page === currentPage}
                                    onClick={() => handlePageChange(page)}
                                >
                                    {page}
                                </PaginationLink>
                            </PaginationItem>
                        ))}
                        <PaginationItem>
                            <PaginationNext
                                onClick={() => handlePageChange(Math.min(currentPage + 1, totalPages))}
                            />
                        </PaginationItem>
                    </PaginationContent>
                </Pagination>
            )}
        </div>
    );
};

export default UsagePage; 