import React, { useEffect, useState } from 'react';
import { useRouter } from 'next/router';

import useDebounce from '@/hooks/useDebounce';
import useTranslation from '@/hooks/useTranslation';

import { formatDateTime } from '@/utils/date';

import { AdminChatsDto } from '@/types/adminApis';
import { PageResult, Paging } from '@/types/page';

import PaginationContainer from '../../../components/Pagination/Pagination';
import ModelProviderIcon from '@/components/common/ModelProviderIcon';
import Tips from '@/components/Tips/Tips';
import { Badge } from '@/components/ui/badge';
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

import { getMessages } from '@/apis/adminApis';
import { cn } from '@/lib/utils';

export default function Messages() {
  const { t } = useTranslation();
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [messages, setMessages] = useState<PageResult<AdminChatsDto[]>>({
    count: 0,
    rows: [],
  });
  
  // 本地搜索输入状态（用于防抖）
  const [userInput, setUserInput] = useState('');
  const [contentInput, setContentInput] = useState('');

  // 从 URL 中读取状态
  const user = (router.query.user as string) || '';
  const content = (router.query.content as string) || '';
  const page = parseInt((router.query.p as string) || '1', 10);
  const pageSize = parseInt((router.query.pageSize as string) || '12', 10);
  const defaultPageSize = 12;

  // 更新 URL 的函数
  const updateUrl = (params: { user?: string; content?: string; page?: number; pageSize?: number }) => {
    const newQuery: Record<string, string> = {};
    
    // 复制现有的 query 参数（只保留 string 类型）
    Object.keys(router.query).forEach(key => {
      const value = router.query[key];
      if (typeof value === 'string') {
        newQuery[key] = value;
      }
    });
    
    // 更新或删除 user 参数
    if (params.user !== undefined) {
      if (params.user) {
        newQuery.user = params.user;
      } else {
        delete newQuery.user;
      }
    }
    
    // 更新或删除 content 参数
    if (params.content !== undefined) {
      if (params.content) {
        newQuery.content = params.content;
      } else {
        delete newQuery.content;
      }
    }
    
    // 更新 p (page)，第一页时不显示
    if (params.page !== undefined) {
      if (params.page === 1) {
        delete newQuery.p;
      } else {
        newQuery.p = params.page.toString();
      }
    }
    
    // 更新 pageSize，默认值 12 时不显示
    if (params.pageSize !== undefined) {
      if (params.pageSize === defaultPageSize) {
        delete newQuery.pageSize;
      } else {
        newQuery.pageSize = params.pageSize.toString();
      }
    }

    router.push(
      {
        pathname: router.pathname,
        query: newQuery,
      },
      undefined,
      { shallow: true }
    );
  };

  const updateUserWithDebounce = useDebounce((searchUser: string) => {
    updateUrl({ user: searchUser, page: 1 }); // 搜索时重置到第一页
  }, 1000);

  const updateContentWithDebounce = useDebounce((searchContent: string) => {
    updateUrl({ content: searchContent, page: 1 }); // 搜索时重置到第一页
  }, 1000);

  const init = () => {
    setLoading(true);
    getMessages({ page, pageSize, user, content }).then((data) => {
      setMessages(data);
      setLoading(false);
    });
  };

  useEffect(() => {
    // 只有当 router 准备好时才执行
    if (router.isReady) {
      init();
    }
  }, [router.isReady, router.query]);

  // 同步 URL 中的参数到本地搜索输入状态
  useEffect(() => {
    setUserInput(user);
    setContentInput(content);
  }, [user, content]);

  return (
    <>
      <div className="flex flex-wrap gap-4 mb-4">
        <Input
          className="max-w-[238px] w-full"
          placeholder={t('User Name') + '...'}
          value={userInput}
          onChange={(e) => {
            setUserInput(e.target.value);
            updateUserWithDebounce(e.target.value);
          }}
        />
        <Input
          className="max-w-[238px] w-full"
          placeholder={t('Message Content') + '...'}
          value={contentInput}
          onChange={(e) => {
            setContentInput(e.target.value);
            updateContentWithDebounce(e.target.value);
          }}
        />
      </div>
      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('Title')}</TableHead>
              <TableHead>{t('Model')}</TableHead>
              <TableHead>{t('User Name')}</TableHead>
              <TableHead>{t('Created Time')}</TableHead>
              <TableHead>{t('Status')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody isLoading={loading} isEmpty={messages.count === 0}>
            {messages?.rows.map((item) => (
              <TableRow key={item.id}>
                <TableCell
                  onClick={() => {
                    window.open('/message/' + item.id, '_blank');
                  }}
                  className="truncate cursor-pointer"
                >
                  {item.title}
                </TableCell>
                <TableCell>
                  <div className="flex overflow-hidden">
                    {item.spans.map((x, index) => (
                      <div
                        key={'message-chat-icon-wrapper-' + x.modelId}
                        className={cn(
                          "flex-shrink-0 relative",
                          index > 0 && "-ml-2.5"
                        )}
                        style={{ zIndex: item.spans.length - index }}
                      >
                        <Tips
                          trigger={
                            <div>
                              <ModelProviderIcon
                                className="cursor-pointer"
                                providerId={x.modelProviderId}
                              />
                            </div>
                          }
                          side="bottom"
                          content={x.modelName}
                        />
                      </div>
                    ))}
                  </div>
                </TableCell>
                <TableCell>{item.username}</TableCell>
                <TableCell>{formatDateTime(item.createdAt)}</TableCell>
                <TableCell>
                  {item.isDeleted && (
                    <Badge variant="destructive">{t('Deleted')}</Badge>
                  )}
                  {item.isShared && (
                    <Badge className=" bg-green-600">{t('Shared')}</Badge>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        {messages.count !== 0 && (
          <PaginationContainer
            page={page}
            pageSize={pageSize}
            currentCount={messages.rows.length}
            totalCount={messages.count}
            onPagingChange={(newPage, newPageSize) => {
              updateUrl({ page: newPage, pageSize: newPageSize });
            }}
          />
        )}
      </Card>
    </>
  );
}
