import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import Link from 'next/link';

import useDebounce from '@/hooks/useDebounce';
import useTranslation from '@/hooks/useTranslation';

import { toFixed } from '@/utils/common';

import { GetUsersResult } from '@/types/adminApis';
import { PageResult, Paging } from '@/types/page';

import PaginationContainer from '../../../components/Pagination/Pagination';
import { Badge } from '@/components/ui/badge';
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
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { IconPencil } from '@/components/Icons';

import EditUserBalanceModal from '@/components/admin/Users/EditUserBalanceModel';
import UserModal from '@/components/admin/Users/UserModal';

import { getUsers } from '@/apis/adminApis';

export default function Users() {
  const { t } = useTranslation();
  const [isOpenModal, setIsOpenModal] = useState({
    edit: false,
    create: false,
    recharge: false,
  });
  const [pagination, setPagination] = useState<Paging>({
    page: 1,
    pageSize: 50,
  });
  const [selectedUser, setSelectedUser] = useState<GetUsersResult | null>(null);
  const [users, setUsers] = useState<PageResult<GetUsersResult[]>>({
    count: 0,
    rows: [],
  });

  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState<string>('');

  useEffect(() => {
    init();
  }, [pagination]);

  const updateQueryWithDebounce = useDebounce((query: string) => {
    init(query);
  }, 1000);

  const init = (query: string = '') => {
    getUsers({ query, ...pagination }).then((data) => {
      setUsers(data);
      handleClose();
      setLoading(false);
    });
  };

  const handleShowAddModal = () => {
    setIsOpenModal({
      edit: false,
      create: true,
      recharge: false,
    });
  };

  const handleShowEditModal = (user: GetUsersResult) => {
    setSelectedUser(user);
    setIsOpenModal({
      edit: true,
      create: false,
      recharge: false,
    });
  };

  const handleShowReChargeModal = (user: GetUsersResult) => {
    setSelectedUser(user);
    setIsOpenModal({
      edit: false,
      create: false,
      recharge: true,
    });
  };

  const handleClose = () => {
    setIsOpenModal({
      edit: false,
      create: false,
      recharge: false,
    });
    setSelectedUser(null);
  };

  return (
    <>
      <div className="flex flex-warp gap-4 mb-4">
        <Input
          className="max-w-[238px] w-full"
          placeholder={t('Search...')!}
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            updateQueryWithDebounce(e.target.value);
          }}
        />
        <Button onClick={() => handleShowAddModal()} color="primary">
          {t('Add User')}
        </Button>
      </div>
      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('User Name')}</TableHead>
              <TableHead className="hidden sm:table-cell">{t('Account')}</TableHead>
              <TableHead className="hidden sm:table-cell">{t('Role')}</TableHead>
              <TableHead className="hidden sm:table-cell">{t('Phone')}</TableHead>
              <TableHead className="hidden sm:table-cell">{t('E-Mail')}</TableHead>
              <TableHead>
                {t('Balance')}({t('Yuan')})
              </TableHead>
              <TableHead>{t('Model Count')}</TableHead>
              <TableHead>{t('Actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody isLoading={loading} isEmpty={users.rows.length === 0}>
            {users.rows.map((item) => (
              <React.Fragment key={item.id}>
                <TableRow className="cursor-pointer">
                  <TableCell>
                    <div className="flex gap-1 items-center">
                      <div
                        className={`w-2 h-2 rounded-full ${
                          item.enabled ? 'bg-green-400' : 'bg-gray-400'
                        }`}
                      ></div>
                      {item.username}
                      {item.provider && (
                        <Badge className="capitalize">{item.provider}</Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="hidden sm:table-cell">{item.account}</TableCell>
                  <TableCell className="hidden sm:table-cell">{item.role}</TableCell>
                  <TableCell className="hidden sm:table-cell">{item.phone}</TableCell>
                  <TableCell className="hidden sm:table-cell">{item.email}</TableCell>
                  <TableCell
                    className="hover:underline cursor-pointer"
                    onClick={(e) => {
                      handleShowReChargeModal(item);
                      e.stopPropagation();
                    }}
                  >
                    {toFixed(+item.balance)}
                  </TableCell>
                  <TableCell>
                    <TooltipProvider>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <Link 
                            href={`/admin/user-models?tab=by-user&username=${encodeURIComponent(item.username)}`}
                            className="text-primary hover:text-primary/80 underline cursor-pointer"
                            onClick={(e) => e.stopPropagation()}
                          >
                            {item.userModelCount}
                          </Link>
                        </TooltipTrigger>
                        <TooltipContent>
                          <p>{t('Click to enter management page')}</p>
                        </TooltipContent>
                      </Tooltip>
                    </TooltipProvider>
                  </TableCell>
                  <TableCell>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="h-8 w-8 p-0"
                      title={t('Edit User')}
                      onClick={(e) => {
                        handleShowEditModal(item);
                        e.stopPropagation();
                      }}
                    >
                      <IconPencil size={16} />
                    </Button>
                  </TableCell>
                </TableRow>
              </React.Fragment>
            ))}
          </TableBody>
        </Table>
        {users.count !== 0 && (
          <PaginationContainer
            page={pagination.page}
            pageSize={pagination.pageSize}
            currentCount={users.rows.length}
            totalCount={users.count}
            onPagingChange={(page, pageSize) => {
              setPagination({ page, pageSize });
            }}
          />
        )}
      </Card>
      <UserModal
        user={selectedUser}
        onSuccessful={init}
        onClose={handleClose}
        isOpen={isOpenModal.create || isOpenModal.edit}
      />
      <EditUserBalanceModal
        onSuccessful={init}
        onClose={handleClose}
        userId={selectedUser?.id}
        userBalance={selectedUser?.balance}
        isOpen={isOpenModal.recharge}
      />
    </>
  );
}
