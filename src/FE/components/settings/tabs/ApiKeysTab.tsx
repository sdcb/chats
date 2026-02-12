import { useEffect, useState, useRef } from 'react';
import toast from 'react-hot-toast';

import Link from 'next/link';
import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { getApiUrl } from '@/utils/common';
import { formatDate } from '@/utils/date';

import { UsageSource } from '@/types/chat';
import { GetUserApiKeyResult } from '@/types/clientApis';

import DateTimePopover from '@/components/Popover/DateTimePopover';
import DeletePopover from '@/components/Popover/DeletePopover';

import CopyButton from '@/components/Button/CopyButton';
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
  deleteUserApiKey,
  getUserApiKey,
  postUserApiKey,
  putUserApiKey,
} from '@/apis/clientApis';

const ApiKeysTab = () => {
  const { t } = useTranslation();
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [apiKeys, setApiKeys] = useState<GetUserApiKeyResult[]>([]);
  const debounceRef = useRef<NodeJS.Timeout | null>(null);
  type GetUserApiKeyType = keyof GetUserApiKeyResult;

  const initData = () => {
    getUserApiKey()
      .then((data) => {
        setApiKeys(data);
      })
      .finally(() => {
        setLoading(false);
      });
  };

  useEffect(() => {
    setLoading(true);
    initData();

    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
  }, []);

  const createApiKey = () => {
    const now = new Date();
    const yyyy = now.getFullYear();
    const mm = `${now.getMonth() + 1}`.padStart(2, '0');
    const dd = `${now.getDate()}`.padStart(2, '0');
    const comment = `${yyyy}${mm}${dd}`;
    const expires = new Date(
      now.getFullYear() + 1,
      now.getMonth(),
      now.getDate(),
    ).toISOString();

    postUserApiKey({ comment, expires }).then(() => {
      initData();
    });
  };

  const changeApiKeyBy = <K extends GetUserApiKeyType>(
    index: number,
    type: K,
    value: GetUserApiKeyResult[K],
  ) => {
    const apiKey = apiKeys[index];
    setApiKeys((prev) => {
      const data = [...prev];
      data[index][type] = value;
      return data;
    });
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }
    debounceRef.current = setTimeout(() => {
      putUserApiKey(apiKey.id, { [type]: value }).then(() => {
        toast.success(t('Save successful'));
      });
    }, 1000);
  };

  const removeApiKey = (id: string) => {
    deleteUserApiKey(id).then(() => {
      setApiKeys((prev) =>
        prev.filter((x) => {
          return x.id !== id;
        }),
      );
      toast.success(t('Delete successful'));
    });
  };

  const viewApiKeyUsage = (id: string) => {
    router.push(
      `/settings?t=usage&kid=${id}&source=${UsageSource.API}&page=1`,
    );
  };

  const apiUrl = (getApiUrl() || location.origin) + '/v1';
  const docUrl = 'https://platform.openai.com/docs/guides/chat-completions';
  return (
    <div className="w-full">
      <div className="flex justify-between items-center gap-4 px-2 bg-card rounded-md mb-4">
        <div className="relative w-[70%]">
          <div className="p-3 text-xs sm:text-[12.5px]">
            <div className="flex items-center overflow-hidden text-ellipsis whitespace-nowrap">
              API URL: {apiUrl}
              <CopyButton value={apiUrl} />
            </div>
            <div className="flex items-center overflow-hidden text-ellipsis whitespace-nowrap">
              {t('API documentation URL')}:&nbsp;
              <Link
                target="_blank"
                className="text-blue-600 dark:text-blue-500 hover:underline"
                href={docUrl}
              >
                {docUrl}
              </Link>
              <CopyButton value={docUrl} />
            </div>
          </div>
        </div>
        <div>
          <Button variant="default" onClick={createApiKey} size="sm">
            {t('New Key')}
          </Button>
        </div>
      </div>

      <div className="block sm:hidden">
        {loading ? (
          <div className="flex justify-center py-4">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-gray-900 dark:border-white"></div>
          </div>
        ) : apiKeys.length === 0 ? (
          <div className="text-center py-4 text-sm text-gray-500">
            {t('No data')}
          </div>
        ) : (
          <div className="space-y-2">
            {apiKeys.map((x, index) => (
              <Card key={x.id} className="p-2 border-none shadow-sm">
                <div className="flex items-center justify-between mb-2 text-xs">
                  <div className="font-medium">{t('Key')}</div>
                  <div className="flex items-center">
                    <span
                      className="max-w-[180px] truncate cursor-pointer text-blue-600 hover:underline"
                      onClick={() => viewApiKeyUsage(x.id)}
                    >
                      {x.key}
                    </span>
                    <CopyButton value={x.key} />
                  </div>
                </div>
                <div className="flex items-center justify-between mb-2 text-xs">
                  <div className="font-medium">{t('Comment')}</div>
                  <div className="w-3/4">
                    <Input
                      className="h-9 text-xs px-3 border-none"
                      value={x.comment}
                      onChange={(e) => {
                        changeApiKeyBy(index, 'comment', e.target.value);
                      }}
                    />
                  </div>
                </div>
                <div className="flex items-center justify-between mb-2 text-xs">
                  <div className="font-medium">{t('Expires')}</div>
                  <div className="h-9 flex items-center">
                    <DateTimePopover
                      value={x.expires}
                      onSelect={(date: Date) => {
                        changeApiKeyBy(index, 'expires', date as any);
                      }}
                    />
                  </div>
                </div>
                <div className="flex items-center justify-between mb-2 text-xs">
                  <div className="font-medium">{t('LastUsedAt')}</div>
                  <div className="h-9 flex items-center">
                    {x.lastUsedAt ? formatDate(x.lastUsedAt) : '-'}
                  </div>
                </div>
                <div className="flex justify-end mt-1">
                  <DeletePopover
                    onDelete={() => {
                      removeApiKey(x.id);
                    }}
                  />
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>

      <div className="hidden sm:block">
        <Card className="overflow-x-auto w-full rounded-md border-none">
          <Table>
            <TableHeader>
              <TableRow className="pointer-events-none">
                <TableHead>{t('Key')}</TableHead>
                <TableHead>{t('Comment')}</TableHead>
                <TableHead>{t('Expires')}</TableHead>
                <TableHead>{t('LastUsedAt')}</TableHead>
                <TableHead>{t('Actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody isEmpty={apiKeys.length === 0} isLoading={loading}>
              {apiKeys.map((x, index) => {
                return (
                  <TableRow key={x.id} className="cursor-pointer">
                    <TableCell className="py-2">
                      <div className="flex items-center">
                        <div
                          className="overflow-hidden text-ellipsis whitespace-nowrap text-blue-600 hover:underline cursor-pointer"
                          onClick={() => viewApiKeyUsage(x.id)}
                        >
                          {x.key}
                        </div>
                        <CopyButton value={x.key} />
                      </div>
                    </TableCell>
                    <TableCell className="py-2 min-w-[128px] max-w-[200px]">
                      <Input
                        className="border-none h-9 px-3"
                        value={x.comment}
                        onChange={(e) => {
                          changeApiKeyBy(index, 'comment', e.target.value);
                        }}
                      />
                    </TableCell>
                    <TableCell className="py-2">
                      <DateTimePopover
                        className="w-[128px]"
                        placeholder={t('Pick a date')}
                        value={x.expires}
                        onSelect={(date: Date) => {
                          changeApiKeyBy(index, 'expires', date as any);
                        }}
                      />
                    </TableCell>
                    <TableCell className="py-2 min-w-[128px] max-w-[150px]">
                      {x.lastUsedAt ? formatDate(x.lastUsedAt) : '-'}
                    </TableCell>
                    <TableCell className="py-2 max-w-[64px]">
                      <DeletePopover
                        onDelete={() => {
                          removeApiKey(x.id);
                        }}
                      />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </Card>
      </div>
    </div>
  );
};
export default ApiKeysTab;
